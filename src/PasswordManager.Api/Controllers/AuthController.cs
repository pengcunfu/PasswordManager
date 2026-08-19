using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PasswordManager.Api.Dtos;
using PasswordManager.Api.Services;

namespace PasswordManager.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    private const string RefreshCookie = "pm_refresh";

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
            SetRefreshCookie(auth.LastRefreshToken!);
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
            SetRefreshCookie(auth.LastRefreshToken!);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ErrorResponse { Error = ex.Message });
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        var token = Request.Cookies[RefreshCookie];
        if (string.IsNullOrEmpty(token))
            return Unauthorized(new ErrorResponse { Error = "未登录" });

        try
        {
            var (response, refresh) = await auth.RefreshAsync(token, ct);
            SetRefreshCookie(refresh);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            ClearRefreshCookie();
            return Unauthorized(new ErrorResponse { Error = ex.Message });
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var token = Request.Cookies[RefreshCookie];
        if (!string.IsNullOrEmpty(token))
            await auth.RevokeAsync(token, ct);

        ClearRefreshCookie();
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

    private void SetRefreshCookie(string token)
    {
        Response.Cookies.Append(RefreshCookie, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = DateTimeOffset.UtcNow.AddDays(14)
        });
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookie, new CookieOptions
        {
            Path = "/api/auth"
        });
    }
}
