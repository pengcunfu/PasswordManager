using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasswordManager.Api.Data;
using PasswordManager.Api.Dtos;

namespace PasswordManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/vault")]
public class VaultController(AppDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("backup")]
    public async Task<ActionResult<VaultBackupDto>> Backup(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == UserId, ct);
        var groups = await db.Groups.AsNoTracking()
            .Where(g => g.UserId == UserId)
            .OrderBy(g => g.SortOrder)
            .ToListAsync(ct);
        var entries = await db.Entries.AsNoTracking()
            .Where(e => e.UserId == UserId)
            .OrderBy(e => e.Title)
            .ToListAsync(ct);

        var dto = new VaultBackupDto(
            "3.0",
            DateTime.UtcNow,
            user.Username,
            user.KdfSalt,
            groups.Select(g => new GroupDto(g.Id, g.Name, g.Description, g.Color, g.SortOrder, g.CreatedAt, g.UpdatedAt)).ToList(),
            entries.Select(MapEntry).ToList());

        Response.Headers.ContentDisposition = $"attachment; filename=\"password-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json\"";
        return Ok(dto);
    }

    [HttpPost("import")]
    public async Task<ActionResult<ImportResultDto>> Import([FromBody] ImportVaultRequest request, CancellationToken ct)
    {
        var incoming = request.Entries ?? [];
        if (incoming.Count > 5000)
            return BadRequest(new ErrorResponse { Error = "单次最多导入 5000 条" });

        var existingGroups = await db.Groups.Where(g => g.UserId == UserId).ToListAsync(ct);
        var groupByName = existingGroups.ToDictionary(g => g.Name, g => g, StringComparer.OrdinalIgnoreCase);

        var groupsCreated = 0;
        foreach (var item in request.Groups ?? [])
        {
            var name = item.Name.Trim();
            if (string.IsNullOrEmpty(name) || groupByName.ContainsKey(name))
                continue;

            var group = new GroupEntity
            {
                UserId = UserId,
                Name = name,
                Description = item.Description ?? "",
                Color = string.IsNullOrWhiteSpace(item.Color) ? "#4A90E2" : item.Color,
                SortOrder = item.SortOrder
            };
            db.Groups.Add(group);
            groupByName[name] = group;
            groupsCreated++;
        }

        if (groupsCreated > 0)
            await db.SaveChangesAsync(ct);

        var existingKeys = new HashSet<string>(
            (await db.Entries.AsNoTracking()
                .Where(e => e.UserId == UserId)
                .Select(e => new { e.Title, e.Username })
                .ToListAsync(ct))
            .Select(e => e.Title.ToLowerInvariant() + "\n" + e.Username.ToLowerInvariant()));

        var imported = 0;
        var skipped = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in incoming)
        {
            var title = item.Title.Trim();
            if (string.IsNullOrEmpty(title))
            {
                skipped++;
                continue;
            }

            var username = item.Username ?? "";
            var dupKey = title.ToLowerInvariant() + "\n" + username.ToLowerInvariant();
            if (request.SkipDuplicates && (existingKeys.Contains(dupKey) || !seen.Add(dupKey)))
            {
                skipped++;
                continue;
            }

            Guid? groupId = null;
            if (!string.IsNullOrWhiteSpace(item.GroupName) &&
                groupByName.TryGetValue(item.GroupName.Trim(), out var group))
                groupId = group.Id;

            db.Entries.Add(new EntryEntity
            {
                UserId = UserId,
                Title = title,
                Username = username,
                Password = item.Password ?? "",
                Url = item.Url ?? "",
                Notes = item.Notes ?? "",
                Category = item.Category ?? "",
                GroupId = groupId,
                CustomFieldsJson = JsonSerializer.Serialize(item.CustomFields ?? [])
            });
            existingKeys.Add(dupKey);
            imported++;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new ImportResultDto(groupsCreated, imported, skipped));
    }

    [HttpGet("about")]
    [AllowAnonymous]
    public IActionResult About() => Ok(new
    {
        name = "凭据管理器",
        version = "3.0.0",
        description = "自托管多端凭据管理服务",
        author = "FNSoftware"
    });

    private static EntryDto MapEntry(EntryEntity e)
    {
        List<CustomFieldDto> fields;
        try { fields = JsonSerializer.Deserialize<List<CustomFieldDto>>(e.CustomFieldsJson) ?? []; }
        catch { fields = []; }

        return new EntryDto(e.Id, e.Title, e.Username, e.Password, e.Url, e.Notes, e.Category,
            e.GroupId, fields, e.CreatedAt, e.UpdatedAt);
    }
}

[ApiController]
[Route("api/health")]
public class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var canConnect = await db.Database.CanConnectAsync(ct);
        return Ok(new
        {
            status = canConnect ? "ok" : "degraded",
            time = DateTime.UtcNow
        });
    }
}
