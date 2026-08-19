using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;
using PasswordManager.Models;

namespace PasswordManager.Services
{
    /// <summary>
    /// WebView2 消息处理器 - 桥接 C# 与 JavaScript
    /// </summary>
    public class WebViewHandler : IDisposable
    {
        private readonly string _dataDir;
        private readonly WebView2 _webView;
        private StorageManager? _storageManager;
        private AiService? _aiService;
        private AiConfig _aiConfig;
        private bool _isLoggedIn;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public WebViewHandler(WebView2 webView, string dataDir)
        {
            _webView = webView;
            _dataDir = dataDir;
            _aiConfig = AiConfig.Load(dataDir);
        }

        /// <summary>
        /// 注册消息监听
        /// </summary>
        public void Register()
        {
            _webView.CoreWebView2.WebMessageReceived += OnMessageReceived;
        }

        /// <summary>
        /// 发送消息到 JS
        /// </summary>
        public void PostMessage(string type, object? data = null)
        {
            var msg = new { type, data };
            _webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(msg, JsonOptions));
        }

        /// <summary>
        /// 执行 JS
        /// </summary>
        public void ExecJs(string script)
        {
            try { _webView.CoreWebView2.ExecuteScriptAsync(script); }
            catch { /* WebView2 已释放 */ }
        }

        // ─── 消息分发 ───

        private async void OnMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.WebMessageAsJson;
                if (json.StartsWith('"') && json.EndsWith('"'))
                    json = JsonSerializer.Deserialize<string>(json) ?? json;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                string type = root.GetProperty("type").GetString() ?? "";
                var data = root.TryGetProperty("data", out var d) ? d : default;

                switch (type)
                {
                    case "init": HandleInit(); break;
                    case "login": await HandleLogin(data); break;
                    case "getEntries": HandleGetEntries(data); break;
                    case "getEntry": HandleGetEntry(data); break;
                    case "addEntry": HandleAddEntry(data); break;
                    case "updateEntry": HandleUpdateEntry(data); break;
                    case "deleteEntry": HandleDeleteEntry(data); break;
                    case "generatePassword": HandleGeneratePassword(data); break;
                    case "aiChat": await HandleAiChat(data); break;
                    case "aiSettings": HandleAiSettings(); break;
                    case "saveAiSettings": HandleSaveAiSettings(data); break;
                    case "testAiConnection": await HandleTestAiConnection(data); break;
                    case "backup": HandleBackup(); break;
                    case "about": HandleAbout(); break;
                    case "clearAiHistory": HandleClearAiHistory(); break;
                    case "copyToClipboard": HandleCopyToClipboard(data); break;
                }
            }
            catch (Exception ex)
            {
                ExecJs($"App.onError({JsonSerializer.Serialize(ex.Message)})");
            }
        }

        // ─── 处理器 ───

        private void HandleInit()
        {
            // 如果已登录（页面刷新场景），直接发送登录结果
            if (_isLoggedIn && _storageManager != null)
            {
                var allEntries = _storageManager.GetAllEntries();
                var list = allEntries.Select(e => new
                {
                    e.Id, e.Title, e.Username, e.Category,
                    createdAt = e.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    updatedAt = e.UpdatedAt.ToString("yyyy-MM-dd HH:mm")
                }).ToList();

                PostMessage("loginResult", new { success = true, entries = list });
                return;
            }

            string dbPath = Path.Combine(_dataDir, "passwords.json");
            bool hasDb = File.Exists(dbPath);
            PostMessage("init", new { hasDb, dataDir = _dataDir });
        }

        private async Task HandleLogin(JsonElement data)
        {
            string password = data.GetProperty("password").GetString() ?? "";

            try
            {
                string dbPath = Path.Combine(_dataDir, "passwords.json");
                bool isNew = !File.Exists(dbPath);

                // 生成盐值
                string salt;
                if (isNew)
                {
                    salt = CryptoManager.GenerateSalt();
                }
                else
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(dbPath));
                    salt = doc.RootElement.GetProperty("salt").GetString()
                        ?? throw new InvalidOperationException("无法读取盐值");
                }

                var crypto = new CryptoManager(password, salt);
                var storage = new StorageManager(_dataDir, crypto);

                if (isNew)
                {
                    storage.LoadDatabase();
                    SampleDataService.AddSampleData(storage);
                }
                else
                {
                    try
                    {
                        storage.LoadDatabase();
                    }
                    catch
                    {
                        PostMessage("loginResult", new { success = false, error = "密码错误" });
                        return;
                    }
                }

                _storageManager = storage;
                _isLoggedIn = true;

                var allEntries = storage.GetAllEntries();
                var list = allEntries.Select(e => new
                {
                    e.Id, e.Title, e.Username, e.Category,
                    createdAt = e.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    updatedAt = e.UpdatedAt.ToString("yyyy-MM-dd HH:mm")
                }).ToList();

                PostMessage("loginResult", new { success = true, entries = list });
            }
            catch (Exception ex)
            {
                PostMessage("loginResult", new { success = false, error = ex.Message });
            }
        }

        private void HandleGetEntries(JsonElement data)
        {
            EnsureLoggedIn();
            string keyword = data.TryGetProperty("keyword", out var k) ? k.GetString() ?? "" : "";
            var entries = string.IsNullOrWhiteSpace(keyword)
                ? _storageManager!.GetAllEntries()
                : _storageManager!.SearchEntries(keyword);

            var list = entries.Select(e => new
            {
                e.Id, e.Title, e.Username, e.Category,
                createdAt = e.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                updatedAt = e.UpdatedAt.ToString("yyyy-MM-dd HH:mm")
            }).ToList();

            PostMessage("entries", new { entries = list });
        }

        private void HandleGetEntry(JsonElement data)
        {
            EnsureLoggedIn();
            string id = data.GetProperty("id").GetString() ?? "";
            var entry = _storageManager!.GetAllEntries().FirstOrDefault(e => e.Id == id);

            if (entry == null)
            {
                PostMessage("entryDetail", new { error = "未找到条目" });
                return;
            }

            PostMessage("entryDetail", new
            {
                entry.Id, entry.Title, entry.Username, entry.Password,
                entry.URL, entry.Notes, entry.Category,
                createdAt = entry.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                updatedAt = entry.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        private void HandleAddEntry(JsonElement data)
        {
            EnsureLoggedIn();
            var entry = new PasswordEntry(
                data.GetProperty("title").GetString() ?? "",
                data.GetProperty("username").GetString() ?? "",
                data.GetProperty("password").GetString() ?? "",
                data.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                data.TryGetProperty("notes", out var n) ? n.GetString() ?? "" : "",
                data.TryGetProperty("category", out var c) ? c.GetString() ?? "" : ""
            );
            _storageManager!.AddEntry(entry);
            PostMessage("entryAdded", new { entry.Id, entry.Title, entry.Username, entry.Category });
        }

        private void HandleUpdateEntry(JsonElement data)
        {
            EnsureLoggedIn();
            string id = data.GetProperty("id").GetString() ?? "";
            var entry = _storageManager!.GetAllEntries().FirstOrDefault(e => e.Id == id);
            if (entry == null) { PostMessage("error", new { message = "未找到条目" }); return; }

            if (data.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String) entry.Title = t.GetString()!;
            if (data.TryGetProperty("username", out var un) && un.ValueKind == JsonValueKind.String) entry.Username = un.GetString()!;
            if (data.TryGetProperty("password", out var p) && p.ValueKind == JsonValueKind.String) entry.Password = p.GetString()!;
            if (data.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String) entry.URL = url.GetString()!;
            if (data.TryGetProperty("notes", out var notes) && notes.ValueKind == JsonValueKind.String) entry.Notes = notes.GetString()!;
            if (data.TryGetProperty("category", out var cat) && cat.ValueKind == JsonValueKind.String) entry.Category = cat.GetString()!;

            _storageManager.UpdateEntry(entry);
            PostMessage("entryUpdated", new { entry.Id, entry.Title, entry.Username, entry.Category });
        }

        private void HandleDeleteEntry(JsonElement data)
        {
            EnsureLoggedIn();
            string id = data.GetProperty("id").GetString() ?? "";
            _storageManager!.DeleteEntry(id);
            PostMessage("entryDeleted", new { id });
        }

        private void HandleGeneratePassword(JsonElement data)
        {
            int length = data.TryGetProperty("length", out var l) ? l.GetInt32() : 16;
            bool symbols = data.TryGetProperty("symbols", out var s) ? s.GetBoolean() : true;
            length = Math.Clamp(length, 4, 128);
            string password = CryptoManager.GeneratePassword(length, symbols);
            PostMessage("passwordGenerated", new { password });
        }

        private async Task HandleAiChat(JsonElement data)
        {
            if (!_isLoggedIn || _storageManager == null)
            {
                ExecJs("App.onAiError('请先登录')");
                return;
            }
            if (!_aiConfig.IsValid())
            {
                ExecJs("App.onAiError('请先配置 AI 设置')");
                return;
            }

            _aiService ??= new AiService(_storageManager, _aiConfig);
            string message = data.GetProperty("message").GetString() ?? "";

            try
            {
                string response = await _aiService.SendMessageAsync(
                    message,
                    onStreamChunk: chunk =>
                    {
                        var escaped = JsonSerializer.Serialize(chunk);
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            ExecJs($"App.onAiStream({escaped})"));
                    },
                    onToolCall: status =>
                    {
                        var escaped = JsonSerializer.Serialize(status);
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            ExecJs($"App.onAiToolCall({escaped})"));
                    });

                var responseEscaped = JsonSerializer.Serialize(response);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    ExecJs($"App.onAiDone({responseEscaped})"));
            }
            catch (Exception ex)
            {
                var msg = JsonSerializer.Serialize($"请求失败: {ex.Message}");
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    ExecJs($"App.onAiError({msg})"));
            }
        }

        private void HandleAiSettings()
        {
            PostMessage("aiSettingsData", new
            {
                _aiConfig.ApiEndpoint,
                ApiKey = MaskKey(_aiConfig.ApiKey),
                _aiConfig.Model,
                _aiConfig.MaxTokens,
                _aiConfig.Temperature
            });
        }

        private void HandleSaveAiSettings(JsonElement data)
        {
            if (data.TryGetProperty("apiEndpoint", out var ep)) _aiConfig.ApiEndpoint = ep.GetString()!;
            if (data.TryGetProperty("apiKey", out var key))
            {
                string k = key.GetString()!;
                if (k != MaskKey(_aiConfig.ApiKey)) _aiConfig.ApiKey = k;
            }
            if (data.TryGetProperty("model", out var m)) _aiConfig.Model = m.GetString()!;
            if (data.TryGetProperty("maxTokens", out var mt)) _aiConfig.MaxTokens = mt.GetInt32();
            if (data.TryGetProperty("temperature", out var temp)) _aiConfig.Temperature = temp.GetDouble();

            _aiConfig.Save();
            _aiService?.Dispose();
            _aiService = null;
            PostMessage("aiSettingsSaved", new { success = true });
        }

        private async Task HandleTestAiConnection(JsonElement data)
        {
            try
            {
                var testConfig = new AiConfig
                {
                    ApiEndpoint = data.TryGetProperty("apiEndpoint", out var ep) ? ep.GetString()! : _aiConfig.ApiEndpoint,
                    ApiKey = data.TryGetProperty("apiKey", out var key) ? key.GetString()! : _aiConfig.ApiKey,
                    Model = data.TryGetProperty("model", out var m) ? m.GetString()! : _aiConfig.Model
                };

                using var http = new System.Net.Http.HttpClient();
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", testConfig.ApiKey);
                http.Timeout = TimeSpan.FromSeconds(15);

                string url = $"{testConfig.ApiEndpoint.TrimEnd('/')}/chat/completions";
                var body = JsonSerializer.Serialize(new
                {
                    model = testConfig.Model,
                    messages = new[] { new { role = "user", content = "Hi" } },
                    max_tokens = 5
                }, JsonOptions);

                var content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json");
                var resp = await http.PostAsync(url, content);

                PostMessage("aiTestResult", new
                {
                    success = resp.IsSuccessStatusCode,
                    error = resp.IsSuccessStatusCode ? null : $"HTTP {(int)resp.StatusCode}"
                });
            }
            catch (Exception ex)
            {
                PostMessage("aiTestResult", new { success = false, error = ex.Message });
            }
        }

        private void HandleBackup()
        {
            EnsureLoggedIn();
            _storageManager!.CreateBackup();
            PostMessage("backupDone", new { success = true });
        }

        private void HandleAbout()
        {
            PostMessage("aboutData", new
            {
                name = "密码管家",
                version = "2.0.0",
                description = "安全的本地密码管理工具",
                author = "FNSoftware"
            });
        }

        private void HandleClearAiHistory()
        {
            _aiService?.ClearHistory();
        }

        private void HandleCopyToClipboard(JsonElement data)
        {
            string text = data.GetProperty("text").GetString() ?? "";
            System.Windows.Clipboard.SetText(text);
            PostMessage("copyDone", new { success = true });
        }

        // ─── 辅助 ───

        private void EnsureLoggedIn()
        {
            if (!_isLoggedIn || _storageManager == null)
                throw new InvalidOperationException("未登录");
        }

        private static string MaskKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length < 8) return key;
            return key[..4] + "****" + key[^4..];
        }

        public void Dispose()
        {
            _aiService?.Dispose();
        }
    }
}
