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

    [HttpGet]
    public async Task<ActionResult<VaultDocumentDto>> Get(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == UserId, ct);
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(user.VaultJson)
            ? """{"version":"4.0","groups":[],"items":[]}"""
            : user.VaultJson);
        return Ok(new VaultDocumentDto(doc.RootElement.Clone(), user.VaultUpdatedAt));
    }

    [HttpPut]
    public async Task<ActionResult<VaultDocumentDto>> Save([FromBody] SaveVaultRequest request, CancellationToken ct)
    {
        if (request.Document.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return BadRequest(new ErrorResponse { Error = "凭据库内容无效" });

        var user = await db.Users.FirstAsync(u => u.Id == UserId, ct);
        user.VaultJson = request.Document.GetRawText();
        user.VaultUpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        using var doc = JsonDocument.Parse(user.VaultJson);
        return Ok(new VaultDocumentDto(doc.RootElement.Clone(), user.VaultUpdatedAt));
    }

    [HttpGet("backup")]
    public async Task<IActionResult> Backup(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == UserId, ct);
        using var vault = JsonDocument.Parse(string.IsNullOrWhiteSpace(user.VaultJson)
            ? """{"version":"4.0","groups":[],"items":[]}"""
            : user.VaultJson);

        var payload = new VaultBackupDto(
            "4.0",
            DateTime.UtcNow,
            user.Username,
            user.KdfSalt,
            vault.RootElement.Clone());

        Response.Headers.ContentDisposition = $"attachment; filename=\"vault-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json\"";
        return Ok(payload);
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
