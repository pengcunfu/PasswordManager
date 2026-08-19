using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasswordManager.Api.Data;
using PasswordManager.Api.Dtos;

namespace PasswordManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/settings")]
public class SettingsController(AppDbContext db) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<SettingsDto>> Get(CancellationToken ct)
    {
        var settings = await GetOrCreateAsync(ct);
        return Ok(Map(settings, maskKey: true));
    }

    [HttpPut]
    public async Task<ActionResult<SettingsDto>> Update([FromBody] UpdateSettingsRequest request, CancellationToken ct)
    {
        var settings = await GetOrCreateAsync(ct);

        if (request.Theme is not null) settings.Theme = request.Theme;
        if (request.AutoLockMinutes is not null) settings.AutoLockMinutes = request.AutoLockMinutes.Value;
        if (request.ClearClipboardSeconds is not null) settings.ClearClipboardSeconds = request.ClearClipboardSeconds.Value;
        if (request.AiApiEndpoint is not null) settings.AiApiEndpoint = request.AiApiEndpoint.TrimEnd('/');
        if (request.AiModel is not null) settings.AiModel = request.AiModel;
        if (request.AiMaxTokens is not null) settings.AiMaxTokens = request.AiMaxTokens.Value;
        if (request.AiTemperature is not null) settings.AiTemperature = request.AiTemperature.Value;
        if (request.AiApiKey is not null && request.AiApiKey != MaskKey(settings.AiApiKey))
            settings.AiApiKey = request.AiApiKey;

        await db.SaveChangesAsync(ct);
        return Ok(Map(settings, maskKey: true));
    }

    private async Task<UserSettings> GetOrCreateAsync(CancellationToken ct)
    {
        var settings = await db.Settings.FirstOrDefaultAsync(s => s.UserId == UserId, ct);
        if (settings is not null) return settings;

        settings = new UserSettings { UserId = UserId };
        db.Settings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings;
    }

    internal static SettingsDto Map(UserSettings s, bool maskKey) => new(
        s.Theme,
        s.AutoLockMinutes,
        s.ClearClipboardSeconds,
        s.AiApiEndpoint,
        maskKey ? MaskKey(s.AiApiKey) : s.AiApiKey,
        s.AiModel,
        s.AiMaxTokens,
        s.AiTemperature);

    internal static string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 8) return key;
        return key[..4] + "****" + key[^4..];
    }
}
