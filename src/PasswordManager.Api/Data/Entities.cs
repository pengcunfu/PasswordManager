using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PasswordManager.Api.Data;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Client-side KDF salt (Base64, 32 bytes).</summary>
    [MaxLength(88)]
    public string KdfSalt { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<GroupEntity> Groups { get; set; } = [];
    public List<EntryEntity> Entries { get; set; } = [];
    public UserSettings? Settings { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}

public class GroupEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(512)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(16)]
    public string Color { get; set; } = "#4A90E2";

    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public List<EntryEntity> Entries { get; set; } = [];
}

public class EntryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? GroupId { get; set; }

    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(256)]
    public string Username { get; set; } = string.Empty;

    /// <summary>AES-GCM ciphertext of the password (client-encrypted).</summary>
    public string Password { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string Url { get; set; } = string.Empty;

    /// <summary>AES-GCM ciphertext of notes (client-encrypted).</summary>
    public string Notes { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Category { get; set; } = string.Empty;

    /// <summary>JSON array of custom fields; hidden values are client-encrypted.</summary>
    public string CustomFieldsJson { get; set; } = "[]";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public GroupEntity? Group { get; set; }
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
