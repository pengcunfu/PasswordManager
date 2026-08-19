using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasswordManager.Api.Data;
using PasswordManager.Api.Dtos;

namespace PasswordManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/groups")]
public class GroupsController(AppDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<List<GroupDto>>> List(CancellationToken ct)
    {
        var groups = await db.Groups.AsNoTracking()
            .Where(g => g.UserId == UserId)
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Name)
            .ToListAsync(ct);

        return Ok(groups.Select(Map).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<GroupDto>> Create([FromBody] UpsertGroupRequest request, CancellationToken ct)
    {
        var group = new GroupEntity
        {
            UserId = UserId,
            Name = request.Name.Trim(),
            Description = request.Description ?? "",
            Color = string.IsNullOrWhiteSpace(request.Color) ? "#4A90E2" : request.Color,
            SortOrder = request.SortOrder
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync(ct);
        return Ok(Map(group));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GroupDto>> Update(Guid id, [FromBody] UpsertGroupRequest request, CancellationToken ct)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == id && g.UserId == UserId, ct);
        if (group is null)
            return NotFound(new ErrorResponse { Error = "未找到分组" });

        group.Name = request.Name.Trim();
        group.Description = request.Description ?? "";
        group.Color = string.IsNullOrWhiteSpace(request.Color) ? group.Color : request.Color;
        group.SortOrder = request.SortOrder;
        group.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(Map(group));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == id && g.UserId == UserId, ct);
        if (group is null)
            return NotFound(new ErrorResponse { Error = "未找到分组" });

        var entries = await db.Entries.Where(e => e.UserId == UserId && e.GroupId == id).ToListAsync(ct);
        foreach (var entry in entries)
            entry.GroupId = null;

        db.Groups.Remove(group);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static GroupDto Map(GroupEntity g) =>
        new(g.Id, g.Name, g.Description, g.Color, g.SortOrder, g.CreatedAt, g.UpdatedAt);
}
