using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using PasswordManager.Services;

namespace PasswordManager.Views
{
    /// <summary>
    /// AiChatWindow.xaml 的交互逻辑
    /// </summary>
    public partial class AiChatWindow : Window
    {
        private readonly StorageManager _storageManager;
        private readonly string _dataDir;
        private AiConfig _aiConfig;
        private AiService? _aiService;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isProcessing;
        private bool _webViewReady;
        private WebView2? _webView;

        public AiChatWindow(StorageManager storageManager, string dataDir)
        {
            InitializeComponent();
            _storageManager = storageManager;
            _dataDir = dataDir;
            _aiConfig = AiConfig.Load(dataDir);
            Loaded += AiChatWindow_Loaded;
        }

        /// <summary>
        /// 窗口加载完成后初始化 WebView2
        /// </summary>
        private async void AiChatWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _webView = new WebView2();
                WebViewContainer.Child = _webView;

                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FNSoftware", "PasswordManager", "WebView2");

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await _webView.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                _webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                _webView.CoreWebView2.NavigateToString(GetChatHtml());

                _webView.CoreWebView2.NavigationCompleted += (s, args) =>
                {
                    _webViewReady = true;
                    Dispatcher.Invoke(() =>
                    {
                        ExecuteScript("addWelcome()");
                        InputTextBox.Focus();
                    });
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 初始化失败: {ex.Message}\n\n请确保已安装 WebView2 Runtime。",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取聊天 HTML 模板
        /// </summary>
        private static string GetChatHtml()
        {
            return """
            <!DOCTYPE html>
            <html>
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Microsoft YaHei', sans-serif;
                    background: #f5f5f5;
                    color: #333;
                    font-size: 13px;
                    line-height: 1.6;
                    padding: 16px;
                    overflow-y: auto;
                    word-wrap: break-word;
                }
                .message {
                    margin-bottom: 16px;
                    display: flex;
                    animation: fadeIn 0.2s ease;
                }
                @keyframes fadeIn {
                    from { opacity: 0; transform: translateY(8px); }
                    to { opacity: 1; transform: translateY(0); }
                }
                .message.user { justify-content: flex-end; }
                .message.ai { justify-content: flex-start; }
                .bubble {
                    max-width: 80%;
                    padding: 10px 14px;
                    border-radius: 12px;
                    word-break: break-word;
                    white-space: pre-wrap;
                    user-select: text;
                    -webkit-user-select: text;
                }
                .message.user .bubble {
                    background: #18A058;
                    color: #fff;
                    border-bottom-right-radius: 4px;
                }
                .message.ai .bubble {
                    background: #fff;
                    color: #333;
                    border: 1px solid #e0e0e6;
                    border-bottom-left-radius: 4px;
                }
                .bubble .status {
                    color: #18A058;
                    font-style: italic;
                    font-size: 12px;
                }
                .bubble .error { color: #e53935; }
                .bubble code {
                    background: #f0f0f2;
                    padding: 1px 5px;
                    border-radius: 3px;
                    font-family: 'Cascadia Code', 'Consolas', monospace;
                    font-size: 12px;
                }
                .message.user .bubble code { background: rgba(255,255,255,0.2); }
                .bubble pre {
                    background: #1e1e1e;
                    color: #d4d4d4;
                    padding: 10px 12px;
                    border-radius: 6px;
                    margin: 8px 0;
                    overflow-x: auto;
                    font-family: 'Cascadia Code', 'Consolas', monospace;
                    font-size: 12px;
                    line-height: 1.5;
                }
                .bubble pre code { background: none; padding: 0; color: inherit; }
                .welcome {
                    text-align: center;
                    color: #999;
                    padding: 40px 20px;
                    font-size: 13px;
                    line-height: 2;
                }
                .welcome-icon { font-size: 36px; margin-bottom: 12px; }
                .typing-dot {
                    display: inline-block;
                    width: 6px; height: 6px;
                    border-radius: 50%;
                    background: #18A058;
                    margin: 0 2px;
                    animation: typing 1.2s infinite;
                }
                .typing-dot:nth-child(2) { animation-delay: 0.2s; }
                .typing-dot:nth-child(3) { animation-delay: 0.4s; }
                @keyframes typing {
                    0%, 60%, 100% { opacity: 0.3; transform: scale(0.8); }
                    30% { opacity: 1; transform: scale(1); }
                }
                .pwd-field {
                    display: inline-flex;
                    align-items: center;
                    gap: 6px;
                    background: #f0f0f2;
                    border: 1px solid #e0e0e6;
                    border-radius: 6px;
                    padding: 2px 4px 2px 10px;
                    vertical-align: middle;
                    font-family: 'Cascadia Code', 'Consolas', monospace;
                    font-size: 12px;
                    line-height: 1;
                }
                .pwd-mask { color: #888; letter-spacing: 1px; }
                .pwd-copy-btn {
                    display: inline-flex;
                    align-items: center;
                    justify-content: center;
                    background: #18A058;
                    color: #fff;
                    border: none;
                    border-radius: 4px;
                    padding: 4px 10px;
                    font-size: 12px;
                    cursor: pointer;
                    white-space: nowrap;
                    transition: background 0.15s;
                }
                .pwd-copy-btn:hover { background: #15924b; }
                .pwd-copy-btn.copied { background: #999; }
                .message.user .pwd-field { background: rgba(255,255,255,0.2); border-color: rgba(255,255,255,0.3); }
                .message.user .pwd-mask { color: rgba(255,255,255,0.7); }
                .message.user .pwd-copy-btn { background: rgba(255,255,255,0.3); }
                .message.user .pwd-copy-btn:hover { background: rgba(255,255,255,0.5); }
                ::-webkit-scrollbar { width: 6px; }
                ::-webkit-scrollbar-track { background: transparent; }
                ::-webkit-scrollbar-thumb { background: #c0c0c0; border-radius: 3px; }
                ::-webkit-scrollbar-thumb:hover { background: #a0a0a0; }
            </style>
            </head>
            <body>
            <div id="messages"></div>
            <script>
                const ICO = {
                    bot: '<svg xmlns="http://www.w3.org/2000/svg" height="1em" viewBox="0 0 24 24" fill="currentColor" style="vertical-align:-0.125em"><path d="M12 2a2 2 0 012 2c0 .74-.4 1.39-1 1.73V7h1a7 7 0 017 7h1a1 1 0 011 1v3a1 1 0 01-1 1h-1v1a2 2 0 01-2 2H5a2 2 0 01-2-2v-1H2a1 1 0 01-1-1v-3a1 1 0 011-1h1a7 7 0 017-7h1V5.73c-.6-.34-1-.99-1-1.73a2 2 0 012-2zm-3 9a1.5 1.5 0 100 3 1.5 1.5 0 000-3zm6 0a1.5 1.5 0 100 3 1.5 1.5 0 000-3z"/></svg>',
                    clipboard: '<svg xmlns="http://www.w3.org/2000/svg" height="1em" viewBox="0 0 24 24" fill="currentColor" style="vertical-align:-0.125em"><path d="M19 2h-4.18C14.4.84 13.3 0 12 0c-1.3 0-2.4.84-2.82 2H5c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-7 0c.55 0 1 .45 1 1s-.45 1-1 1-1-.45-1-1 .45-1 1-1zm7 18H5V4h2v3h10V4h2v16z"/></svg>',
                    check: '<svg xmlns="http://www.w3.org/2000/svg" height="1em" viewBox="0 0 24 24" fill="currentColor" style="vertical-align:-0.125em"><path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"/></svg>',
                    wrench: '<svg xmlns="http://www.w3.org/2000/svg" height="1em" viewBox="0 0 24 24" fill="currentColor" style="vertical-align:-0.125em"><path d="M22.7 19l-9.1-9.1c.9-2.3.4-5-1.5-6.9-2-2-5-2.4-7.4-1.3L9 6 6 9 1.6 4.7C.4 7.1.9 10.1 2.9 12.1c1.9 1.9 4.6 2.4 6.9 1.5l9.1 9.1c.4.4 1 .4 1.4 0l2.3-2.3c.5-.4.5-1.1.1-1.4z"/></svg>'
                };
                const messagesDiv = document.getElementById('messages');
                let currentAiBubble = null;

                function scrollToBottom() {
                    requestAnimationFrame(() => {
                        window.scrollTo(0, document.body.scrollHeight);
                    });
                }

                function addWelcome() {
                    const div = document.createElement('div');
                    div.className = 'welcome';
                    div.innerHTML = `
                        <div class="welcome-icon">${ICO.bot}</div>
                        <div>你好！我是密码管家 AI 助手</div>
                        <div>你可以用自然语言和我交流来管理密码，例如：</div>
                        <div style="margin-top:8px; text-align:left; display:inline-block;">
                            • 帮我查看 GitHub 的密码<br>
                            • 添加一个新的 Gmail 密码<br>
                            • 生成一个16位的随机密码<br>
                            • 列出所有分类
                        </div>
                    `;
                    messagesDiv.appendChild(div);
                }

                function escapeHtml(text) {
                    const div = document.createElement('div');
                    div.textContent = text;
                    return div.innerHTML;
                }

                function formatContent(text) {
                    let html = escapeHtml(text);
                    // 密码字段：[PASSWORD:xxx] → 掩码 + 复制按钮
                    html = html.replace(/\[PASSWORD:(.*?)\]/g, function(match, pwd) {
                        const encoded = btoa(unescape(encodeURIComponent(pwd)));
                        return `<span class="pwd-field"><span class="pwd-mask">••••••••</span><button class="pwd-copy-btn" onclick="copyPwd(this,'${encoded}')" title="复制密码">${ICO.clipboard} 复制</button></span>`;
                    });
                    html = html.replace(/```(\w*)\n([\s\S]*?)```/g, '<pre><code>$2</code></pre>');
                    html = html.replace(/`([^`]+)`/g, '<code>$1</code>');
                    html = html.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
                    html = html.replace(/\n/g, '<br>');
                    return html;
                }

                function copyPwd(btn, encoded) {
                    const pwd = decodeURIComponent(escape(atob(encoded)));
                    // 通过 WebView2 消息通道调用 C# 剪贴板
                    if (window.chrome && window.chrome.webview) {
                        window.chrome.webview.postMessage(JSON.stringify({ type: 'copy', text: pwd }));
                    } else {
                        // fallback
                        const ta = document.createElement('textarea');
                        ta.value = pwd;
                        ta.style.position = 'fixed';
                        ta.style.left = '-9999px';
                        document.body.appendChild(ta);
                        ta.select();
                        document.execCommand('copy');
                        document.body.removeChild(ta);
                    }
                    btn.innerHTML = ICO.check + ' 已复制';
                    btn.classList.add('copied');
                    setTimeout(() => {
                        btn.innerHTML = ICO.clipboard + ' 复制';
                        btn.classList.remove('copied');
                    }, 2000);
                }

                function addUserMessage(text) {
                    const div = document.createElement('div');
                    div.className = 'message user';
                    div.innerHTML = `<div class="bubble">${escapeHtml(text)}</div>`;
                    messagesDiv.appendChild(div);
                    scrollToBottom();
                }

                function addAiMessage() {
                    const div = document.createElement('div');
                    div.className = 'message ai';
                    div.innerHTML = `<div class="bubble"><span class="status"><span class="typing-dot"></span><span class="typing-dot"></span><span class="typing-dot"></span> 正在思考...</span><span class="content" style="display:none"></span></div>`;
                    messagesDiv.appendChild(div);
                    currentAiBubble = div.querySelector('.bubble');
                    scrollToBottom();
                    return currentAiBubble;
                }

                function updateAiStatus(bubble, text) {
                    if (!bubble) return;
                    const status = bubble.querySelector('.status');
                    if (status) {
                        status.innerHTML = ICO.wrench + ' ' + escapeHtml(text);
                        status.style.color = '#18A058';
                    }
                    scrollToBottom();
                }

                function appendAiContent(bubble, text) {
                    if (!bubble) return;
                    const status = bubble.querySelector('.status');
                    const content = bubble.querySelector('.content');
                    if (status) status.style.display = 'none';
                    if (content) {
                        content.style.display = 'inline';
                        content.setAttribute('data-raw', (content.getAttribute('data-raw') || '') + text);
                        content.innerHTML = formatContent(content.getAttribute('data-raw'));
                    }
                    scrollToBottom();
                }

                function finalizeAiMessage(bubble, text) {
                    if (!bubble) return;
                    const status = bubble.querySelector('.status');
                    const content = bubble.querySelector('.content');
                    if (status) status.style.display = 'none';
                    if (content) {
                        content.style.display = 'inline';
                        if (!content.getAttribute('data-raw') && text) {
                            content.setAttribute('data-raw', text);
                            content.innerHTML = formatContent(text);
                        }
                    }
                    currentAiBubble = null;
                    scrollToBottom();
                }

                function setAiError(bubble, text) {
                    if (!bubble) return;
                    const status = bubble.querySelector('.status');
                    const content = bubble.querySelector('.content');
                    if (status) status.style.display = 'none';
                    if (content) {
                        content.style.display = 'inline';
                        content.className = 'content error';
                        content.textContent = text;
                    }
                    currentAiBubble = null;
                    scrollToBottom();
                }

                function clearMessages() {
                    messagesDiv.innerHTML = '';
                }
            </script>
            </body>
            </html>
            """;
        }

        /// <summary>
        /// 确保 AI 服务已初始化
        /// </summary>
        private bool EnsureAiService()
        {
            if (_aiConfig.IsValid())
            {
                _aiService?.Dispose();
                _aiService = new AiService(_storageManager, _aiConfig);
                return true;
            }

            var result = MessageBox.Show(
                "请先配置 AI 模型参数（API 地址、密钥、模型名称）。\n\n是否现在打开设置？",
                "AI 配置",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                OpenSettings();
                return _aiConfig.IsValid();
            }

            return false;
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        /// <summary>
        /// 回车发送
        /// </summary>
        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !_isProcessing)
            {
                e.Handled = true;
                _ = SendMessageAsync();
            }
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        private async Task SendMessageAsync()
        {
            string message = InputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(message) || _isProcessing)
                return;

            if (!EnsureAiService())
                return;

            InputTextBox.Text = "";
            _isProcessing = true;
            SendButton.IsEnabled = false;

            ExecuteScript($"addUserMessage({JsonSerializer.Serialize(message)})");
            ExecuteScript("addAiMessage()");

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                string response = await _aiService!.SendMessageAsync(
                    message,
                    onStreamChunk: chunk =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            string escaped = JsonSerializer.Serialize(chunk);
                            ExecuteScript($"appendAiContent(currentAiBubble, {escaped})");
                        });
                    },
                    onToolCall: toolStatus =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            string escaped = JsonSerializer.Serialize(toolStatus);
                            ExecuteScript($"updateAiStatus(currentAiBubble, {escaped})");
                        });
                    },
                    cancellationToken: _cancellationTokenSource.Token);

                string responseEscaped = JsonSerializer.Serialize(response);
                Dispatcher.Invoke(() =>
                {
                    ExecuteScript($"finalizeAiMessage(currentAiBubble, {responseEscaped})");
                });
            }
            catch (OperationCanceledException)
            {
                Dispatcher.Invoke(() =>
                {
                    ExecuteScript("setAiError(currentAiBubble, '已取消')");
                });
            }
            catch (Exception ex)
            {
                string errorMsg = JsonSerializer.Serialize($"请求失败: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    ExecuteScript($"setAiError(currentAiBubble, {errorMsg})");
                });
            }
            finally
            {
                _isProcessing = false;
                SendButton.IsEnabled = true;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 处理 WebView2 消息（复制密码到剪贴板）
        /// </summary>
        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.WebMessageAsJson;
                // 去掉外层引号（WebView2 会对字符串消息加引号）
                if (json.StartsWith('"') && json.EndsWith('"'))
                    json = JsonSerializer.Deserialize<string>(json) ?? json;

                var msg = JsonDocument.Parse(json);
                var root = msg.RootElement;

                if (root.TryGetProperty("type", out var type) && type.GetString() == "copy")
                {
                    string text = root.GetProperty("text").GetString() ?? "";
                    Dispatcher.Invoke(() =>
                    {
                        Clipboard.SetText(text);
                    });
                }
            }
            catch
            {
                // 忽略解析错误
            }
        }

        /// <summary>
        /// 执行 JavaScript
        /// </summary>
        private void ExecuteScript(string script)
        {
            if (_webViewReady && _webView?.CoreWebView2 != null)
            {
                try
                {
                    _webView.CoreWebView2.ExecuteScriptAsync(script);
                }
                catch
                {
                    // WebView2 可能已释放
                }
            }
        }

        /// <summary>
        /// 打开设置
        /// </summary>
        private void OpenSettings()
        {
            var settingsWindow = new AiSettingsWindow(_aiConfig);
            settingsWindow.Owner = this;

            if (settingsWindow.ShowDialog() == true)
            {
                _aiConfig = AiConfig.Load(_dataDir);
                _aiService?.Dispose();
                _aiService = null;
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteScript("clearMessages(); addWelcome()");
            _aiService?.ClearHistory();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _aiService?.Dispose();
            _webView?.Dispose();
        }
    }
}
