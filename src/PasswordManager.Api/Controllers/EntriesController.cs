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
[Route("api/entries")]
public class EntriesController(AppDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<List<EntryDto>>> List([FromQuery] string? keyword, [FromQuery] Guid? groupId, CancellationToken ct)
    {
        var query = db.Entries.AsNoTracking().Where(e => e.UserId == UserId);

        if (groupId.HasValue)
            query = groupId.Value == Guid.Empty
                ? query.Where(e => e.GroupId == null)
                : query.Where(e => e.GroupId == groupId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim().ToLower();
            query = query.Where(e =>
                e.Title.ToLower().Contains(k) ||
                e.Username.ToLower().Contains(k) ||
                e.Url.ToLower().Contains(k) ||
                e.Category.ToLower().Contains(k));
        }

        var rows = await query.OrderByDescending(e => e.UpdatedAt).ToListAsync(ct);
        return Ok(rows.Select(Map).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EntryDto>> Get(Guid id, CancellationToken ct)
    {
        var entry = await db.Entries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == UserId, ct);
        if (entry is null)
            return NotFound(new ErrorResponse { Error = "未找到条目" });
        return Ok(Map(entry));
    }

    [HttpPost]
    public async Task<ActionResult<EntryDto>> Create([FromBody] UpsertEntryRequest request, CancellationToken ct)
    {
        if (request.GroupId is { } gid)
        {
            var exists = await db.Groups.AnyAsync(g => g.Id == gid && g.UserId == UserId, ct);
            if (!exists)
                return BadRequest(new ErrorResponse { Error = "分组不存在" });
        }

        var entry = new EntryEntity
        {
            UserId = UserId,
            Title = request.Title.Trim(),
            Username = request.Username ?? "",
            Password = request.Password ?? "",
            Url = request.Url ?? "",
            Notes = request.Notes ?? "",
            Category = request.Category ?? "",
            GroupId = request.GroupId,
            CustomFieldsJson = JsonSerializer.Serialize(request.CustomFields ?? [])
        };

        db.Entries.Add(entry);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = entry.Id }, Map(entry));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EntryDto>> Update(Guid id, [FromBody] UpsertEntryRequest request, CancellationToken ct)
    {
        var entry = await db.Entries.FirstOrDefaultAsync(e => e.Id == id && e.UserId == UserId, ct);
        if (entry is null)
            return NotFound(new ErrorResponse { Error = "未找到条目" });

        if (request.GroupId is { } gid)
        {
            var exists = await db.Groups.AnyAsync(g => g.Id == gid && g.UserId == UserId, ct);
            if (!exists)
                return BadRequest(new ErrorResponse { Error = "分组不存在" });
        }

        entry.Title = request.Title.Trim();
        entry.Username = request.Username ?? "";
        entry.Password = request.Password ?? "";
        entry.Url = request.Url ?? "";
        entry.Notes = request.Notes ?? "";
        entry.Category = request.Category ?? "";
        entry.GroupId = request.GroupId;
        entry.CustomFieldsJson = JsonSerializer.Serialize(request.CustomFields ?? []);
        entry.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(Map(entry));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entry = await db.Entries.FirstOrDefaultAsync(e => e.Id == id && e.UserId == UserId, ct);
        if (entry is null)
            return NotFound(new ErrorResponse { Error = "未找到条目" });

        db.Entries.Remove(entry);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static EntryDto Map(EntryEntity e)
    {
        List<CustomFieldDto> fields;
        try
        {
            fields = JsonSerializer.Deserialize<List<CustomFieldDto>>(e.CustomFieldsJson) ?? [];
        }
        catch
        {
            fields = [];
        }

        return new EntryDto(
            e.Id, e.Title, e.Username, e.Password, e.Url, e.Notes, e.Category,
            e.GroupId, fields, e.CreatedAt, e.UpdatedAt);
    }
}
