using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PasswordManager.Api.Auth;
using PasswordManager.Api.Data;
using PasswordManager.Api.Dtos;

namespace PasswordManager.Api.Services;

public class AuthService(AppDbContext db, JwtTokenService tokens)
{
    private readonly PasswordHasher<User> _hasher = new();

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim();
        if (await db.Users.AnyAsync(u => u.Username == username, ct))
            throw new InvalidOperationException("用户名已存在");

        if (string.IsNullOrWhiteSpace(request.KdfSalt))
            throw new InvalidOperationException("缺少密钥派生盐值");

        var user = new User
        {
            Username = username,
            KdfSalt = request.KdfSalt
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        db.Settings.Add(new UserSettings { UserId = user.Id });
        await db.SaveChangesAsync(ct);

        return await IssueAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var username = request.Username.Trim();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username, ct)
            ?? throw new UnauthorizedAccessException("用户名或密码错误");

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            throw new UnauthorizedAccessException("用户名或密码错误");

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(ct);
        }

        return await IssueAsync(user, ct);
    }

    public async Task<string?> PreloginSaltAsync(string username, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username.Trim(), ct);
        return user?.KdfSalt;
    }

    public async Task<(AuthResponse Response, string RefreshToken)> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = JwtTokenService.HashToken(refreshToken);
        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null || !stored.IsActive)
            throw new UnauthorizedAccessException("刷新令牌无效");

        stored.RevokedAt = DateTime.UtcNow;
        var response = await IssueAsync(stored.User, ct, skipSave: true);
        await db.SaveChangesAsync(ct);
        return (response, _lastRefresh!);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct)
    {
        var hash = JwtTokenService.HashToken(refreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null) return;
        stored.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private string? _lastRefresh;

    private async Task<AuthResponse> IssueAsync(User user, CancellationToken ct, bool skipSave = false)
    {
        var access = tokens.CreateAccessToken(user);
        var refresh = JwtTokenService.CreateRefreshToken();
        _lastRefresh = refresh;

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = JwtTokenService.HashToken(refresh),
            ExpiresAt = tokens.RefreshExpiresAt
        });

        if (!skipSave)
            await db.SaveChangesAsync(ct);

        return new AuthResponse(access, tokens.AccessTokenSeconds, user.Username, user.KdfSalt, user.Id);
    }

    public string? LastRefreshToken => _lastRefresh;
}
