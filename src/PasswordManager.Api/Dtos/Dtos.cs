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

public record PreloginResponse(string KdfSalt);

public record CustomFieldDto(string Key, string Value, bool IsHidden);

public record EntryDto(
    Guid Id,
    string Title,
    string Username,
    string Password,
    string Url,
    string Notes,
    string Category,
    Guid? GroupId,
    List<CustomFieldDto> CustomFields,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record UpsertEntryRequest(
    [Required, MaxLength(256)] string Title,
    string? Username,
    string? Password,
    string? Url,
    string? Notes,
    string? Category,
    Guid? GroupId,
    List<CustomFieldDto>? CustomFields);

public record GroupDto(
    Guid Id,
    string Name,
    string Description,
    string Color,
    int SortOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record UpsertGroupRequest(
    [Required, MaxLength(128)] string Name,
    string? Description,
    string? Color,
    int SortOrder);

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

public record VaultBackupDto(
    string Version,
    DateTime ExportedAt,
    string Username,
    string KdfSalt,
    List<GroupDto> Groups,
    List<EntryDto> Entries);

public record ImportVaultRequest(
    bool SkipDuplicates,
    List<ImportGroupItem>? Groups,
    List<ImportEntryItem>? Entries);

public record ImportGroupItem(
    [Required, MaxLength(128)] string Name,
    string? Description,
    string? Color,
    int SortOrder);

public record ImportEntryItem(
    [Required, MaxLength(256)] string Title,
    string? Username,
    string? Password,
    string? Url,
    string? Notes,
    string? Category,
    string? GroupName,
    List<CustomFieldDto>? CustomFields);

public record ImportResultDto(int GroupsCreated, int EntriesImported, int EntriesSkipped);

public record AiTestRequest(string? ApiEndpoint, string? ApiKey, string? Model);

public class ErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;
}
