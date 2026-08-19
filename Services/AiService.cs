using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using PasswordManager.Models;

namespace PasswordManager.Services
{
    /// <summary>
    /// AI 对话服务（OpenAI 兼容协议）
    /// </summary>
    public class AiService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly StorageManager _storageManager;
        private readonly AiConfig _config;
        private readonly List<ChatMessage> _conversationHistory = [];

        public AiService(StorageManager storageManager, AiConfig config)
        {
            _storageManager = storageManager;
            _config = config;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }

        /// <summary>
        /// 清空对话历史
        /// </summary>
        public void ClearHistory()
        {
            _conversationHistory.Clear();
        }

        /// <summary>
        /// 发送消息并获取回复（流式）
        /// </summary>
        /// <param name="userMessage">用户消息</param>
        /// <param name="onStreamChunk">流式文本块回调</param>
        /// <param name="onToolCall">工具调用状态回调</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>AI 完整回复</returns>
        public async Task<string> SendMessageAsync(
            string userMessage,
            Action<string>? onStreamChunk = null,
            Action<string>? onToolCall = null,
            CancellationToken cancellationToken = default)
        {
            // 添加用户消息到历史
            _conversationHistory.Add(new ChatMessage("user", userMessage));

            // 构建系统提示
            var messages = new List<object>
            {
                new { role = "system", content = GetSystemPrompt() }
            };

            // 添加历史消息（保留最近 20 条）
            var recentHistory = _conversationHistory.Count > 20
                ? _conversationHistory.Skip(_conversationHistory.Count - 20).ToList()
                : _conversationHistory;

            foreach (var msg in recentHistory)
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }

            // 获取工具定义
            var tools = GetToolDefinitions();

            // Function calling 循环
            var fullResponse = new StringBuilder();

            while (true)
            {
                var requestBody = new
                {
                    model = _config.Model,
                    messages,
                    tools,
                    max_tokens = _config.MaxTokens,
                    temperature = _config.Temperature,
                    stream = onStreamChunk != null
                };

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                string jsonBody = JsonSerializer.Serialize(requestBody, options);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                string apiEndpoint = _config.ApiEndpoint.TrimEnd('/');
                string url = $"{apiEndpoint}/chat/completions";

                if (onStreamChunk != null)
                {
                    // 流式请求
                    var result = await ProcessStreamResponseAsync(url, content, onStreamChunk, onToolCall, cancellationToken);
                    fullResponse.Append(result.Text);

                    if (result.ToolCalls.Count > 0)
                    {
                        // 有工具调用，需要继续对话
                        // 添加 assistant 消息（带 tool_calls）
                        var assistantMsg = new Dictionary<string, object>
                        {
                            ["role"] = "assistant",
                            ["content"] = result.Text
                        };
                        assistantMsg["tool_calls"] = result.ToolCalls.Select(tc => new
                        {
                            id = tc.Id,
                            type = "function",
                            function = new { name = tc.Name, arguments = tc.Arguments }
                        }).ToList();
                        messages.Add(assistantMsg);

                        // 执行每个工具调用并添加结果
                        foreach (var toolCall in result.ToolCalls)
                        {
                            onToolCall?.Invoke($"正在执行: {GetToolDisplayName(toolCall.Name)}...");
                            string toolResult = ExecuteToolCall(toolCall.Name, toolCall.Arguments);
                            messages.Add(new
                            {
                                role = "tool",
                                tool_call_id = toolCall.Id,
                                content = toolResult
                            });
                        }
                    }
                    else
                    {
                        // 没有工具调用，结束
                        break;
                    }
                }
                else
                {
                    // 非流式请求
                    var response = await _httpClient.PostAsync(url, content, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    var responseObj = JsonDocument.Parse(responseJson);
                    var choice = responseObj.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message");

                    string? textContent = choice.TryGetProperty("content", out var c) ? c.GetString() : null;

                    if (choice.TryGetProperty("tool_calls", out var toolCallsElement) &&
                        toolCallsElement.ValueKind == JsonValueKind.Array &&
                        toolCallsElement.GetArrayLength() > 0)
                    {
                        fullResponse.Append(textContent ?? "");

                        var assistantMsg = new Dictionary<string, object>
                        {
                            ["role"] = "assistant",
                            ["content"] = textContent ?? ""
                        };

                        var toolCallList = new List<ToolCallInfo>();
                        foreach (var tc in toolCallsElement.EnumerateArray())
                        {
                            var func = tc.GetProperty("function");
                            toolCallList.Add(new ToolCallInfo
                            {
                                Id = tc.GetProperty("id").GetString() ?? "",
                                Name = func.GetProperty("name").GetString() ?? "",
                                Arguments = func.GetProperty("arguments").GetString() ?? "{}"
                            });
                        }

                        assistantMsg["tool_calls"] = toolCallList.Select(tc => new
                        {
                            id = tc.Id,
                            type = "function",
                            function = new { name = tc.Name, arguments = tc.Arguments }
                        }).ToList();
                        messages.Add(assistantMsg);

                        foreach (var toolCall in toolCallList)
                        {
                            onToolCall?.Invoke($"正在执行: {GetToolDisplayName(toolCall.Name)}...");
                            string toolResult = ExecuteToolCall(toolCall.Name, toolCall.Arguments);
                            messages.Add(new
                            {
                                role = "tool",
                                tool_call_id = toolCall.Id,
                                content = toolResult
                            });
                        }
                    }
                    else
                    {
                        string finalText = textContent ?? "";
                        fullResponse.Append(finalText);
                        break;
                    }
                }
            }

            // 添加 AI 回复到历史
            string responseText = fullResponse.ToString();
            if (!string.IsNullOrEmpty(responseText))
            {
                _conversationHistory.Add(new ChatMessage("assistant", responseText));
            }

            return responseText;
        }

        /// <summary>
        /// 处理流式响应
        /// </summary>
        private async Task<StreamResult> ProcessStreamResponseAsync(
            string url,
            HttpContent content,
            Action<string> onStreamChunk,
            Action<string>? onToolCall,
            CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = new StreamResult();
            var textBuffer = new StringBuilder();
            var toolCallBuffers = new Dictionary<int, ToolCallBuilder>();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (true)
            {
                string? line = await reader.ReadLineAsync(cancellationToken);
                if (line == null) break;
                if (line == null) break;

                // SSE 格式: "data: {...}" 或 "data: [DONE]"
                if (!line.StartsWith("data: "))
                    continue;

                string data = line[6..]; // 去掉 "data: " 前缀
                if (data == "[DONE]")
                    break;

                JsonDocument chunk;
                try
                {
                    chunk = JsonDocument.Parse(data);
                }
                catch
                {
                    continue;
                }

                var root = chunk.RootElement;
                if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                    continue;

                var delta = choices[0].GetProperty("delta");

                // 处理文本内容
                if (delta.TryGetProperty("content", out var contentElement) &&
                    contentElement.ValueKind == JsonValueKind.String)
                {
                    string? text = contentElement.GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        textBuffer.Append(text);
                        onStreamChunk(text);
                    }
                }

                // 处理工具调用
                if (delta.TryGetProperty("tool_calls", out var toolCallsElement) &&
                    toolCallsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tc in toolCallsElement.EnumerateArray())
                    {
                        int index = tc.GetProperty("index").GetInt32();

                        if (!toolCallBuffers.ContainsKey(index))
                        {
                            toolCallBuffers[index] = new ToolCallBuilder();
                            onToolCall?.Invoke("AI 正在调用工具...");
                        }

                        var builder = toolCallBuffers[index];

                        if (tc.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
                            builder.Id = idElement.GetString() ?? "";

                        if (tc.TryGetProperty("function", out var func))
                        {
                            if (func.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
                                builder.Name += nameElement.GetString();

                            if (func.TryGetProperty("arguments", out var argsElement) && argsElement.ValueKind == JsonValueKind.String)
                                builder.Arguments += argsElement.GetString();
                        }
                    }
                }

                // 检查是否完成
                if (choices[0].TryGetProperty("finish_reason", out var finishReason) &&
                    finishReason.ValueKind == JsonValueKind.String)
                {
                    string? reason = finishReason.GetString();
                    if (reason == "tool_calls" || reason == "stop")
                    {
                        // 读取剩余数据
                        continue;
                    }
                }
            }

            // 收集工具调用
            foreach (var kvp in toolCallBuffers.OrderBy(k => k.Key))
            {
                result.ToolCalls.Add(new ToolCallInfo
                {
                    Id = kvp.Value.Id,
                    Name = kvp.Value.Name,
                    Arguments = kvp.Value.Arguments
                });
            }

            result.Text = textBuffer.ToString();
            return result;
        }

        /// <summary>
        /// 获取系统提示词
        /// </summary>
        private static string GetSystemPrompt()
        {
            return """
            你是"密码管家"AI 助手，帮助用户管理他们的密码和账户信息。

            ## 你的能力
            - 搜索、查看、添加、修改、删除密码条目
            - 生成安全的随机密码
            - 列出所有密码分类
            - 管理自定义字段（邮箱、手机号、密保手机、密保问题等）

            ## 多账号支持
            - 同一个服务（如 GitHub、QQ）可以有多个账号
            - 用户说"我有3个GitHub账号"时，应该创建3个条目，标题可以是"GitHub"、"GitHub-工作"等
            - 通过标题区分不同账号，如"GitHub-个人"、"GitHub-公司"

            ## 自定义字段规则（非常重要）
            - 密码条目支持自定义键值对字段，用于保存邮箱、手机号、密保手机等信息
            - 用户提到邮箱、手机号、密保手机等信息时，应该自动识别并存入自定义字段
            - 常见字段名：邮箱、手机号、密保手机、密保问题、备用邮箱、身份证号等
            - 自定义字段支持"隐藏"标记，敏感信息（如密保手机）应设置 isHidden=true
            - 添加或更新密码时，使用 custom_fields 参数传递自定义字段

            ## 查找密码规则（非常重要）
            - 用户要求查看某个密码时，先用 search_passwords 搜索关键词
            - 从搜索结果中找到最匹配的条目，再用 get_password 获取详情
            - 不要要求用户提供精确标题，主动搜索即可
            - 搜索不到时再询问用户

            ## 回复规则
            - 使用中文回复
            - 回复简洁明了
            - 当需要删除密码时，提醒用户确认
            - 如果用户的要求不明确，主动询问细节

            ## 密码显示规则（非常重要）
            - 当需要展示密码时，使用特殊格式：[PASSWORD:实际密码内容]
            - 例如密码是 abc123，则写成 [PASSWORD:abc123]
            - 除此之外不要在回复中出现任何密码明文
            - 用户名、网址、分类、备注等非密码信息正常显示即可
            - 自定义字段中的隐藏字段（isHidden=true）显示为 ••••••••，不要显示真实值

            ## 安全规则
            - 删除操作前必须确认
            - 不要在回复中重复 API 密钥等敏感信息
            """;
        }

        /// <summary>
        /// 获取工具定义
        /// </summary>
        private static List<object> GetToolDefinitions()
        {
            return
            [
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "search_passwords",
                        description = "搜索密码条目。根据关键词搜索标题、用户名、网址、分类、备注等字段。",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                keyword = new { type = "string", description = "搜索关键词" }
                            },
                            required = new[] { "keyword" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_password",
                        description = "获取指定密码条目的详细信息。根据标题精确匹配。",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                title = new { type = "string", description = "密码条目标题" }
                            },
                            required = new[] { "title" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "add_password",
                        description = "添加新的密码条目。支持自定义字段（如邮箱、手机号等）。",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                title = new { type = "string", description = "标题（如 GitHub、Gmail）" },
                                username = new { type = "string", description = "用户名/账号" },
                                password = new { type = "string", description = "密码" },
                                url = new { type = "string", description = "网址" },
                                notes = new { type = "string", description = "备注" },
                                category = new { type = "string", description = "分类" },
                                custom_fields = new
                                {
                                    type = "array",
                                    description = "自定义字段列表，如邮箱、手机号、密保手机等",
                                    items = new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            key = new { type = "string", description = "字段名，如：邮箱、手机号、密保手机" },
                                            value = new { type = "string", description = "字段值" },
                                            isHidden = new { type = "boolean", description = "是否隐藏显示（敏感信息设为true），默认false" }
                                        },
                                        required = new[] { "key", "value" }
                                    }
                                }
                            },
                            required = new[] { "title", "username", "password" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "update_password",
                        description = "更新现有密码条目。需要提供条目 ID。支持更新自定义字段。",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                id = new { type = "string", description = "条目 ID" },
                                title = new { type = "string", description = "新标题" },
                                username = new { type = "string", description = "新用户名" },
                                password = new { type = "string", description = "新密码" },
                                url = new { type = "string", description = "新网址" },
                                notes = new { type = "string", description = "新备注" },
                                category = new { type = "string", description = "新分类" },
                                custom_fields = new
                                {
                                    type = "array",
                                    description = "自定义字段列表（替换现有自定义字段）",
                                    items = new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            key = new { type = "string", description = "字段名" },
                                            value = new { type = "string", description = "字段值" },
                                            isHidden = new { type = "boolean", description = "是否隐藏显示，默认false" }
                                        },
                                        required = new[] { "key", "value" }
                                    }
                                }
                            },
                            required = new[] { "id" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "delete_password",
                        description = "删除密码条目。需要提供条目 ID。",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                id = new { type = "string", description = "条目 ID" }
                            },
                            required = new[] { "id" }
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "list_categories",
                        description = "列出所有密码条目的分类。",
                        parameters = new
                        {
                            type = "object",
                            properties = new { },
                            required = Array.Empty<string>()
                        }
                    }
                },
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "generate_password",
                        description = "生成安全的随机密码。",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                length = new { type = "integer", description = "密码长度，默认16" },
                                includeSymbols = new { type = "boolean", description = "是否包含特殊符号，默认true" }
                            },
                            required = Array.Empty<string>()
                        }
                    }
                }
            ];
        }

        /// <summary>
        /// 执行工具调用
        /// </summary>
        private string ExecuteToolCall(string toolName, string argumentsJson)
        {
            try
            {
                var args = JsonDocument.Parse(argumentsJson);
                var root = args.RootElement;

                return toolName switch
                {
                    "search_passwords" => ExecuteSearchPasswords(root),
                    "get_password" => ExecuteGetPassword(root),
                    "add_password" => ExecuteAddPassword(root),
                    "update_password" => ExecuteUpdatePassword(root),
                    "delete_password" => ExecuteDeletePassword(root),
                    "list_categories" => ExecuteListCategories(),
                    "generate_password" => ExecuteGeneratePassword(root),
                    _ => JsonSerializer.Serialize(new { error = $"未知工具: {toolName}" })
                };
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = $"工具执行失败: {ex.Message}" });
            }
        }

        private string ExecuteSearchPasswords(JsonElement args)
        {
            string keyword = args.GetProperty("keyword").GetString() ?? "";
            var entries = _storageManager.SearchEntries(keyword);

            if (entries.Count == 0)
                return JsonSerializer.Serialize(new { message = "未找到匹配的密码条目", results = Array.Empty<object>() });

            var results = entries.Select(e => new
            {
                id = e.Id,
                title = e.Title,
                username = e.Username,
                url = e.URL,
                category = e.Category,
                notes = e.Notes,
                custom_fields = e.CustomFields.Select(f => new
                {
                    key = f.Key,
                    value = f.IsHidden ? "••••••••" : f.Value,
                    isHidden = f.IsHidden
                }).ToList()
            }).ToList();

            return JsonSerializer.Serialize(new { count = results.Count, results });
        }

        private string ExecuteGetPassword(JsonElement args)
        {
            string title = args.GetProperty("title").GetString() ?? "";
            var entries = _storageManager.SearchEntries(title);

            // 优先精确匹配，其次模糊匹配
            var entry = entries.FirstOrDefault(e =>
                e.Title.Equals(title, StringComparison.OrdinalIgnoreCase))
                ?? entries.FirstOrDefault();

            if (entry == null)
                return JsonSerializer.Serialize(new { error = $"未找到与 '{title}' 相关的密码条目" });

            return JsonSerializer.Serialize(new
            {
                id = entry.Id,
                title = entry.Title,
                username = entry.Username,
                password = entry.Password,
                url = entry.URL,
                category = entry.Category,
                notes = entry.Notes,
                custom_fields = entry.CustomFields.Select(f => new
                {
                    key = f.Key,
                    value = f.Value,
                    isHidden = f.IsHidden
                }).ToList(),
                created_at = entry.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                updated_at = entry.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        private string ExecuteAddPassword(JsonElement args)
        {
            string title = args.GetProperty("title").GetString() ?? "";
            string username = args.GetProperty("username").GetString() ?? "";
            string password = args.GetProperty("password").GetString() ?? "";
            string url = args.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
            string notes = args.TryGetProperty("notes", out var n) ? n.GetString() ?? "" : "";
            string category = args.TryGetProperty("category", out var cat) ? cat.GetString() ?? "" : "";

            var entry = new PasswordEntry(title, username, password, url, notes, category);

            // 解析自定义字段
            if (args.TryGetProperty("custom_fields", out var customFields) &&
                customFields.ValueKind == JsonValueKind.Array)
            {
                foreach (var field in customFields.EnumerateArray())
                {
                    string key = field.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
                    string value = field.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";
                    bool isHidden = field.TryGetProperty("isHidden", out var h) && h.GetBoolean();

                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        entry.CustomFields.Add(new CustomField(key, value, isHidden));
                    }
                }
            }

            _storageManager.AddEntry(entry);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"密码条目 '{title}' 已添加",
                id = entry.Id
            });
        }

        private string ExecuteUpdatePassword(JsonElement args)
        {
            string id = args.GetProperty("id").GetString() ?? "";
            var allEntries = _storageManager.GetAllEntries();
            var entry = allEntries.FirstOrDefault(e => e.Id == id);

            if (entry == null)
                return JsonSerializer.Serialize(new { error = $"未找到 ID 为 '{id}' 的密码条目" });

            if (args.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                entry.Title = title.GetString()!;
            if (args.TryGetProperty("username", out var username) && username.ValueKind == JsonValueKind.String)
                entry.Username = username.GetString()!;
            if (args.TryGetProperty("password", out var password) && password.ValueKind == JsonValueKind.String)
                entry.Password = password.GetString()!;
            if (args.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
                entry.URL = url.GetString()!;
            if (args.TryGetProperty("notes", out var notes) && notes.ValueKind == JsonValueKind.String)
                entry.Notes = notes.GetString()!;
            if (args.TryGetProperty("category", out var category) && category.ValueKind == JsonValueKind.String)
                entry.Category = category.GetString()!;

            // 更新自定义字段
            if (args.TryGetProperty("custom_fields", out var customFields) &&
                customFields.ValueKind == JsonValueKind.Array)
            {
                entry.CustomFields.Clear();
                foreach (var field in customFields.EnumerateArray())
                {
                    string key = field.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
                    string value = field.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";
                    bool isHidden = field.TryGetProperty("isHidden", out var h) && h.GetBoolean();

                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        entry.CustomFields.Add(new CustomField(key, value, isHidden));
                    }
                }
            }

            _storageManager.UpdateEntry(entry);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"密码条目 '{entry.Title}' 已更新"
            });
        }

        private string ExecuteDeletePassword(JsonElement args)
        {
            string id = args.GetProperty("id").GetString() ?? "";
            var allEntries = _storageManager.GetAllEntries();
            var entry = allEntries.FirstOrDefault(e => e.Id == id);

            if (entry == null)
                return JsonSerializer.Serialize(new { error = $"未找到 ID 为 '{id}' 的密码条目" });

            string title = entry.Title;
            _storageManager.DeleteEntry(id);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"密码条目 '{title}' 已删除"
            });
        }

        private string ExecuteListCategories()
        {
            var entries = _storageManager.GetAllEntries();
            var categories = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Category))
                .Select(e => e.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            return JsonSerializer.Serialize(new { categories });
        }

        private string ExecuteGeneratePassword(JsonElement args)
        {
            int length = args.TryGetProperty("length", out var len) ? len.GetInt32() : 16;
            bool includeSymbols = args.TryGetProperty("includeSymbols", out var sym) ? sym.GetBoolean() : true;

            length = Math.Clamp(length, 8, 128);
            string password = CryptoManager.GeneratePassword(length, includeSymbols);

            return JsonSerializer.Serialize(new { password });
        }

        /// <summary>
        /// 获取工具显示名称
        /// </summary>
        private static string GetToolDisplayName(string toolName) => toolName switch
        {
            "search_passwords" => "搜索密码",
            "get_password" => "获取密码",
            "add_password" => "添加密码",
            "update_password" => "更新密码",
            "delete_password" => "删除密码",
            "list_categories" => "列出分类",
            "generate_password" => "生成密码",
            _ => toolName
        };

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }

    /// <summary>
    /// 聊天消息
    /// </summary>
    public class ChatMessage
    {
        public string Role { get; }
        public string Content { get; }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }

    /// <summary>
    /// 工具调用信息
    /// </summary>
    internal class ToolCallInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Arguments { get; set; } = "{}";
    }

    /// <summary>
    /// 流式响应结果
    /// </summary>
    internal class StreamResult
    {
        public string Text { get; set; } = "";
        public List<ToolCallInfo> ToolCalls { get; set; } = [];
    }

    /// <summary>
    /// 工具调用构建器（用于流式拼接）
    /// </summary>
    internal class ToolCallBuilder
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Arguments { get; set; } = "";
    }
}
