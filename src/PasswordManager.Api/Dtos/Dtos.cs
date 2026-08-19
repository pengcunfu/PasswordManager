using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PasswordManager.Api.Dtos;

public record RegisterRequest(
    [Required, MinLength(2), MaxLength(64)] string Username,
    [Required, MinLength(8)] string Password,
    [Required] string KdfSalt);

public record LoginRequest(
    [Required] string Username,
    [Required] string Password);

public record AuthResponse(
    string AccessToken,
    int ExpiresIn,
    string Username,
    string KdfSalt,
    Guid UserId);

public record RefreshRequest(Guid? UserId);

public record LogoutRequest(Guid? UserId, bool All = false);

public record PreloginResponse(string KdfSalt);

public record SettingsDto(
    string Theme,
    int AutoLockMinutes,
    int ClearClipboardSeconds,
    string AiApiEndpoint,
    string AiApiKey,
    string AiModel,
    int AiMaxTokens,
    double AiTemperature);

public record UpdateSettingsRequest(
    string? Theme,
    int? AutoLockMinutes,
    int? ClearClipboardSeconds,
    string? AiApiEndpoint,
    string? AiApiKey,
    string? AiModel,
    int? AiMaxTokens,
    double? AiTemperature);

public record VaultDocumentDto(
    System.Text.Json.JsonElement Document,
    DateTime UpdatedAt);

public record SaveVaultRequest(System.Text.Json.JsonElement Document);

public record VaultBackupDto(
    string Version,
    DateTime ExportedAt,
    string Username,
    string KdfSalt,
    System.Text.Json.JsonElement Document);

public record AiTestRequest(string? ApiEndpoint, string? ApiKey, string? Model);

public class ErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;
}
