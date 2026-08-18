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

                _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
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
    }
}
