using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PasswordManager.Api.Data;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(88)]
    public string KdfSalt { get; set; } = string.Empty;

    /// <summary>Entire credential vault as JSON (irregular items + multi-account).</summary>
    public string VaultJson { get; set; } = """{"version":"4.0","groups":[],"items":[]}""";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime VaultUpdatedAt { get; set; } = DateTime.UtcNow;

    public UserSettings? Settings { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}

public class UserSettings
{
    public Guid UserId { get; set; }

    [MaxLength(16)]
    public string Theme { get; set; } = "light";

    public int AutoLockMinutes { get; set; } = 15;
    public int ClearClipboardSeconds { get; set; } = 30;

    [MaxLength(512)]
    public string AiApiEndpoint { get; set; } = "https://api.openai.com/v1";

    public string AiApiKey { get; set; } = string.Empty;

    [MaxLength(128)]
    public string AiModel { get; set; } = "gpt-4o-mini";

    public int AiMaxTokens { get; set; } = 2048;
    public double AiTemperature { get; set; } = 0.7;

    public User User { get; set; } = null!;
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    [MaxLength(88)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    [NotMapped]
    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;

    public User User { get; set; } = null!;
}
