using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using PasswordManager.Api.Dtos;
using PasswordManager.Api.Services;

namespace PasswordManager.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    private const string ActiveRefreshCookie = "pm_refresh";
    private const string RefreshCookiePrefix = "pm_refresh_";

    [HttpGet("prelogin")]
    [AllowAnonymous]
    public async Task<ActionResult<PreloginResponse>> Prelogin([FromQuery] string username, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username))
            return BadRequest(new ErrorResponse { Error = "请提供用户名" });

        var salt = await auth.PreloginSaltAsync(username, ct);
        if (salt is null)
            return NotFound(new ErrorResponse { Error = "用户不存在" });

        return Ok(new PreloginResponse(salt));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var response = await auth.RegisterAsync(request, ct);
            SetRefreshCookies(response.UserId, auth.LastRefreshToken!);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ErrorResponse { Error = ex.Message });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var response = await auth.LoginAsync(request, ct);
            SetRefreshCookies(response.UserId, auth.LastRefreshToken!);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ErrorResponse { Error = ex.Message });
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshRequest? request,
        CancellationToken ct)
    {
        var token = ReadRefreshCookie(request?.UserId);
        if (string.IsNullOrEmpty(token))
            return Unauthorized(new ErrorResponse { Error = "未登录" });

        try
        {
            var (response, refresh) = await auth.RefreshAsync(token, ct);
            SetRefreshCookies(response.UserId, refresh);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            if (request?.UserId is Guid id)
                DeleteRefreshCookies(id);
            else
                DeleteCookie(ActiveRefreshCookie);
            return Unauthorized(new ErrorResponse { Error = ex.Message });
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] LogoutRequest? request,
        CancellationToken ct)
    {
        if (request?.All == true)
        {
            foreach (var name in RefreshCookieNames())
            {
                if (!string.IsNullOrEmpty(Request.Cookies[name]))
                    await auth.RevokeAsync(Request.Cookies[name]!, ct);
                DeleteCookie(name);
            }
            return NoContent();
        }

        var token = ReadRefreshCookie(request?.UserId);
        if (!string.IsNullOrEmpty(token))
            await auth.RevokeAsync(token, ct);

        if (request?.UserId is Guid userId)
            DeleteRefreshCookies(userId);
        else
            DeleteCookie(ActiveRefreshCookie);

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new
        {
            userId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            username = User.Identity?.Name
        });
    }

    private string? ReadRefreshCookie(Guid? userId)
    {
        if (userId is Guid id)
        {
            var named = Request.Cookies[CookieName(id)];
            if (!string.IsNullOrEmpty(named))
                return named;
        }
        return Request.Cookies[ActiveRefreshCookie];
    }

    private void SetRefreshCookies(Guid userId, string token)
    {
        var options = CookieOptions();
        options.Expires = DateTimeOffset.UtcNow.AddDays(14);
        Response.Cookies.Append(CookieName(userId), token, options);
        Response.Cookies.Append(ActiveRefreshCookie, token, options);
    }

    private void DeleteRefreshCookies(Guid userId)
    {
        DeleteCookie(CookieName(userId));
        DeleteCookie(ActiveRefreshCookie);
    }

    private void DeleteCookie(string name)
    {
        Response.Cookies.Delete(name, new CookieOptions { Path = "/api/auth" });
    }

    private CookieOptions CookieOptions() => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/api/auth"
    };

    private static string CookieName(Guid userId) => $"{RefreshCookiePrefix}{userId:N}";

    private IEnumerable<string> RefreshCookieNames()
    {
        yield return ActiveRefreshCookie;
        foreach (var key in Request.Cookies.Keys)
        {
            if (key.StartsWith(RefreshCookiePrefix, StringComparison.OrdinalIgnoreCase))
                yield return key;
        }
    }
}
