using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PasswordManager.Api.Auth;
using PasswordManager.Api.Data;
using PasswordManager.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt 配置缺失");

if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey 必须至少 32 个字符，请通过环境变量 Jwt__SigningKey 注入");

if (!builder.Environment.IsDevelopment() &&
    jwt.SigningKey.Contains("CHANGE-ME", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("生产环境必须通过 Jwt__SigningKey 设置随机签名密钥");

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=data/vault.db";

EnsureSqliteDirectory(connectionString);

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddHttpClient("ai", client => client.Timeout = TimeSpan.FromMinutes(2));
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
builder.Services.AddOpenApi();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("spa", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
            ?? ["http://localhost:5173"];
        policy.SetIsOriginAllowed(origin => IsAllowedSpaOrigin(origin, origins))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    VaultMigrator.EnsureAsync(db).GetAwaiter().GetResult();
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseRateLimiter();
app.UseCors("spa");
app.UseAuthentication();
app.UseAuthorization();

var wwwroot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var hasSpa = Directory.Exists(wwwroot) && Directory.EnumerateFileSystemEntries(wwwroot).Any();
if (hasSpa)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapControllers();
if (hasSpa)
{
    app.MapFallback("/api/{**path}", () => Results.NotFound(new { error = "Not found" }));
    app.MapFallbackToFile("index.html");
}

app.Run();

static bool IsAllowedSpaOrigin(string origin, string[] configured)
{
    if (configured.Contains(origin, StringComparer.OrdinalIgnoreCase))
        return true;
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        return false;
    if (uri.Scheme is not ("http" or "https"))
        return false;

    var host = uri.Host;
    if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host is "127.0.0.1" or "::1")
        return true;
    if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        return true;
    return IPAddress.TryParse(host, out var ip) && IsPrivateOrLinkLocal(ip);
}

static bool IsPrivateOrLinkLocal(IPAddress ip)
{
    if (IPAddress.IsLoopback(ip)) return true;
    if (ip.IsIPv6LinkLocal || ip.IsIPv6UniqueLocal) return true;
    if (ip.AddressFamily != AddressFamily.InterNetwork) return false;
    var b = ip.GetAddressBytes();
    return b[0] == 10
        || (b[0] == 172 && b[1] is >= 16 and <= 31)
        || (b[0] == 192 && b[1] == 168)
        || (b[0] == 169 && b[1] == 254);
}

static void EnsureSqliteDirectory(string connectionString)
{
    const string prefix = "Data Source=";
    var idx = connectionString.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
    if (idx < 0) return;

    var path = connectionString[(idx + prefix.Length)..].Trim();
    var semi = path.IndexOf(';');
    if (semi >= 0) path = path[..semi];
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrWhiteSpace(dir))
        Directory.CreateDirectory(dir);
}
