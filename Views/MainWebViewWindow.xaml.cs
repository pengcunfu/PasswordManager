using System;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using PasswordManager.Services;

namespace PasswordManager.Views
{
    public partial class MainWebViewWindow : Window
    {
        private readonly string _dataDir;
        private WebView2? _webView;
        private WebViewHandler? _handler;

        public MainWebViewWindow(string dataDir)
        {
            InitializeComponent();
            _dataDir = dataDir;
            Loaded += Window_Loaded;
            PreviewKeyDown += Window_PreviewKeyDown;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _webView = new WebView2();
                RootGrid.Children.Add(_webView);

                string userData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FNSoftware", "PasswordManager", "WebView2");

                var env = await CoreWebView2Environment.CreateAsync(null, userData);
                await _webView.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                _handler = new WebViewHandler(_webView, _dataDir);
                _handler.Register();

                // 加载 HTML
                string htmlPath = Path.Combine(AppContext.BaseDirectory, "App.html");
                if (File.Exists(htmlPath))
                {
                    _webView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
                }
                else
                {
                    // 回退：内联 HTML
                    _webView.CoreWebView2.NavigateToString("<html><body><h1>App.html 未找到</h1></body></html>");
                    MessageBox.Show($"找不到 App.html: {htmlPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 初始化失败: {ex.Message}\n\n请确保已安装 WebView2 Runtime。",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _handler?.Dispose();
            _webView?.Dispose();
        }

        /// <summary>
        /// 拦截键盘快捷键，禁用浏览器功能
        /// </summary>
        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var key = e.Key;

            // 禁用 F5（刷新）
            if (key == System.Windows.Input.Key.F5)
            {
                e.Handled = true;
                return;
            }

            // 禁用 F12（开发者工具）
            if (key == System.Windows.Input.Key.F12)
            {
                e.Handled = true;
                return;
            }

            // 禁用 Ctrl+R（刷新）、Ctrl+U（查看源代码）
            if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                if (key == System.Windows.Input.Key.R || key == System.Windows.Input.Key.U)
                {
                    e.Handled = true;
                    return;
                }
            }

            // 禁用 Ctrl+Shift+I（开发者工具）、Ctrl+Shift+J（控制台）
            if (e.KeyboardDevice.Modifiers == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
            {
                if (key == System.Windows.Input.Key.I || key == System.Windows.Input.Key.J)
                {
                    e.Handled = true;
                    return;
                }
            }
        }
    }
}
