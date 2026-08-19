using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace PasswordManager.Api.Data;

public static class VaultMigrator
{
    public static async Task EnsureAsync(AppDbContext db, CancellationToken ct = default)
    {
        await db.Database.OpenConnectionAsync(ct);
        await EnsureColumnAsync(db, ct);
        await MigrateLegacyTablesAsync(db, ct);
    }

    private static async Task EnsureColumnAsync(AppDbContext db, CancellationToken ct)
    {
        if (!await ColumnExistsAsync(db, "Users", "VaultJson", ct))
        {
            await ExecuteNonQueryAsync(
                db,
                """ALTER TABLE "Users" ADD COLUMN "VaultJson" TEXT NOT NULL DEFAULT '{"version":"4.0","groups":[],"items":[]}'""",
                ct);
        }

        if (!await ColumnExistsAsync(db, "Users", "VaultUpdatedAt", ct))
        {
            await ExecuteNonQueryAsync(
                db,
                """ALTER TABLE "Users" ADD COLUMN "VaultUpdatedAt" TEXT NOT NULL DEFAULT '2020-01-01'""",
                ct);
        }
    }

    private static async Task MigrateLegacyTablesAsync(AppDbContext db, CancellationToken ct)
    {
        if (!await TableExistsAsync(db, "Entries", ct))
            return;

        var users = await db.Users.ToListAsync(ct);
        foreach (var user in users)
        {
            if (!IsEmptyVault(user.VaultJson) && user.VaultJson.Contains("\"items\""))
                continue;

            var vault = await BuildVaultFromLegacyAsync(db, user.Id, ct);
            user.VaultJson = vault.ToJsonString();
            user.VaultUpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task<JsonObject> BuildVaultFromLegacyAsync(AppDbContext db, Guid userId, CancellationToken ct)
    {
        var groups = new JsonArray();
        await using (var cmd = db.Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = """SELECT "Id","Name","Description","Color","SortOrder","CreatedAt","UpdatedAt" FROM "Groups" WHERE "UserId" = $id""";
            AddParam(cmd, "$id", userId.ToString());
            try
            {
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    groups.Add(new JsonObject
                    {
                        ["id"] = reader.GetString(0),
                        ["name"] = reader.IsDBNull(1) ? "" : reader.GetString(1),
                        ["description"] = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        ["color"] = reader.IsDBNull(3) ? "#4A90E2" : reader.GetString(3),
                        ["sortOrder"] = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4)),
                        ["createdAt"] = reader.IsDBNull(5) ? DateTime.UtcNow.ToString("O") : Convert.ToString(reader.GetValue(5)),
                        ["updatedAt"] = reader.IsDBNull(6) ? DateTime.UtcNow.ToString("O") : Convert.ToString(reader.GetValue(6))
                    });
                }
            }
            catch
            {
                // Groups table may not exist
            }
        }

        var rows = new List<JsonObject>();
        await using (var cmd = db.Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText = """
                SELECT "Id","Title","Username","Password","Url","Notes","Category","GroupId","CustomFieldsJson","CreatedAt","UpdatedAt"
                FROM "Entries" WHERE "UserId" = $id
                """;
            AddParam(cmd, "$id", userId.ToString());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                rows.Add(new JsonObject
                {
                    ["id"] = reader.GetString(0),
                    ["title"] = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    ["username"] = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ["password"] = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    ["url"] = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    ["notes"] = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    ["category"] = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    ["groupId"] = reader.IsDBNull(7) ? null : reader.GetString(7),
                    ["customFieldsJson"] = reader.IsDBNull(8) ? "[]" : reader.GetString(8),
                    ["createdAt"] = reader.IsDBNull(9) ? DateTime.UtcNow.ToString("O") : Convert.ToString(reader.GetValue(9)),
                    ["updatedAt"] = reader.IsDBNull(10) ? DateTime.UtcNow.ToString("O") : Convert.ToString(reader.GetValue(10))
                });
            }
        }

        var buckets = new Dictionary<string, List<JsonObject>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var url = row["url"]?.GetValue<string>() ?? "";
            var title = row["title"]?.GetValue<string>() ?? "";
            var key = string.IsNullOrWhiteSpace(url) ? $"title:{title.ToLowerInvariant()}" : $"url:{url.Trim().ToLowerInvariant()}";
            if (!buckets.TryGetValue(key, out var list))
            {
                list = [];
                buckets[key] = list;
            }
            list.Add(row);
        }

        var items = new JsonArray();
        foreach (var (key, list) in buckets)
        {
            var first = list[0];
            var url = first["url"]?.GetValue<string>() ?? "";
            var title = PickItemTitle(list, url);
            var accounts = new JsonArray();
            foreach (var row in list)
            {
                JsonNode fields;
                try { fields = JsonNode.Parse(row["customFieldsJson"]?.GetValue<string>() ?? "[]") ?? new JsonArray(); }
                catch { fields = new JsonArray(); }

                var rowTitle = row["title"]?.GetValue<string>() ?? "";
                var username = row["username"]?.GetValue<string>() ?? "";
                var label = list.Count == 1
                    ? (string.IsNullOrWhiteSpace(username) ? "默认" : username)
                    : (rowTitle.Equals(title, StringComparison.OrdinalIgnoreCase) ? username : rowTitle);

                accounts.Add(new JsonObject
                {
                    ["id"] = row["id"]?.GetValue<string>(),
                    ["label"] = string.IsNullOrWhiteSpace(label) ? "默认" : label,
                    ["username"] = username,
                    ["secret"] = row["password"]?.GetValue<string>() ?? "",
                    ["notes"] = row["notes"]?.GetValue<string>() ?? "",
                    ["fields"] = fields.DeepClone()
                });
            }

            items.Add(new JsonObject
            {
                ["id"] = Guid.NewGuid().ToString(),
                ["type"] = "login",
                ["title"] = title,
                ["url"] = url,
                ["groupId"] = first["groupId"],
                ["category"] = first["category"]?.GetValue<string>() ?? "",
                ["notes"] = "",
                ["accounts"] = accounts,
                ["createdAt"] = first["createdAt"],
                ["updatedAt"] = first["updatedAt"]
            });
        }

        return new JsonObject
        {
            ["version"] = "4.0",
            ["groups"] = groups,
            ["items"] = items
        };
    }

    private static string PickItemTitle(List<JsonObject> rows, string url)
    {
        var titles = rows.Select(r => r["title"]?.GetValue<string>() ?? "").Where(t => t.Length > 0).ToList();
        if (titles.Count == 0)
            return string.IsNullOrWhiteSpace(url) ? "未命名" : url;
        if (titles.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
            return titles[0];

        var prefixes = titles.Select(t =>
        {
            var i = t.IndexOfAny(['-', '_', '—', '/']);
            return i > 0 ? t[..i] : t;
        }).ToList();
        if (prefixes.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
            return prefixes[0];

        return titles.OrderBy(t => t.Length).First();
    }

    private static bool IsEmptyVault(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json is "{}")
            return true;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items))
                return true;
            return items.GetArrayLength() == 0;
        }
        catch
        {
            return true;
        }
    }

    private static async Task ExecuteNonQueryAsync(AppDbContext db, string sql, CancellationToken ct)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> TableExistsAsync(AppDbContext db, string name, CancellationToken ct)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$n";
        AddParam(cmd, "$n", name);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is not null and not DBNull;
    }

    private static async Task<bool> ColumnExistsAsync(AppDbContext db, string table, string column, CancellationToken ct)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void AddParam(DbCommand cmd, string name, string value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
