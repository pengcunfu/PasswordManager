using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasswordManager.Api.Data;
using PasswordManager.Api.Dtos;

namespace PasswordManager.Api.Controllers;

/// <summary>
/// Thin OpenAI-compatible proxy so the browser can call the configured AI endpoint without CORS issues.
/// Tool execution stays on the client (zero-knowledge vault).
/// </summary>
[ApiController]
[Authorize]
[Route("api/ai")]
public class AiController(AppDbContext db, IHttpClientFactory httpFactory) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] AiTestRequest request, CancellationToken ct)
    {
        var settings = await db.Settings.FirstOrDefaultAsync(s => s.UserId == UserId, ct);
        var endpoint = (request.ApiEndpoint ?? settings?.AiApiEndpoint ?? "https://api.openai.com/v1").TrimEnd('/');
        var apiKey = request.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey) || (settings is not null && apiKey == SettingsController.MaskKey(settings.AiApiKey)))
            apiKey = settings?.AiApiKey ?? "";
        var model = request.Model ?? settings?.AiModel ?? "gpt-4o-mini";

        if (string.IsNullOrWhiteSpace(apiKey))
            return Ok(new { success = false, error = "未配置 API 密钥" });

        try
        {
            var client = httpFactory.CreateClient("ai");
            using var msg = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/chat/completions");
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            msg.Content = new StringContent(
                JsonContent(model),
                Encoding.UTF8,
                "application/json");

            using var resp = await client.SendAsync(msg, ct);
            return Ok(new
            {
                success = resp.IsSuccessStatusCode,
                error = resp.IsSuccessStatusCode ? (string?)null : $"HTTP {(int)resp.StatusCode}"
            });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, error = ex.Message });
        }
    }

    [HttpPost("completions")]
    public async Task ProxyCompletions(CancellationToken ct)
    {
        var settings = await db.Settings.FirstOrDefaultAsync(s => s.UserId == UserId, ct);
        if (settings is null || string.IsNullOrWhiteSpace(settings.AiApiKey))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new ErrorResponse { Error = "请先配置 AI 设置" }, ct);
            return;
        }

        var endpoint = settings.AiApiEndpoint.TrimEnd('/');
        await using var body = new MemoryStream();
        await Request.Body.CopyToAsync(body, ct);
        var bytes = body.ToArray();

        var client = httpFactory.CreateClient("ai");
        using var msg = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/chat/completions");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AiApiKey);
        msg.Content = new ByteArrayContent(bytes);
        msg.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var resp = await client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
        Response.StatusCode = (int)resp.StatusCode;
        if (resp.Content.Headers.ContentType?.MediaType is { } media)
            Response.ContentType = media;

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        await stream.CopyToAsync(Response.Body, ct);
    }

    private static string JsonContent(string model) =>
        $$"""{"model":"{{model}}","messages":[{"role":"user","content":"Hi"}],"max_tokens":5}""";
}
