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
            groups.Select(g => new GroupDto(g.Id, g.Name, g.Description, g.Color, g.SortOrder, g.CreatedAt, g.UpdatedAt)).ToList(),
            entries.Select(MapEntry).ToList());

        Response.Headers.ContentDisposition = $"attachment; filename=\"password-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json\"";
        return Ok(dto);
    }

    [HttpGet("about")]
    [AllowAnonymous]
    public IActionResult About() => Ok(new
    {
        name = "密码管家",
        version = "3.0.0",
        description = "自托管多端密码管理服务",
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
