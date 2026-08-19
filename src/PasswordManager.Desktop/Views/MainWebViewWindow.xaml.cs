using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using PasswordManager.Desktop.Services;

namespace PasswordManager.Desktop.Views;

public partial class MainWebViewWindow : Window
{
    private readonly DesktopSettings _settings;
    private WebView2? _webView;

    public MainWebViewWindow(DesktopSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        ServerLabel.Text = settings.ServerUrl;
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

#if DEBUG
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
#else
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
#endif
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

            _webView.CoreWebView2.Navigate(_settings.ServerUrl.TrimEnd('/') + "/");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"WebView2 初始化失败: {ex.Message}\n\n请确保已安装 WebView2 Runtime。",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ChangeServer_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ConnectServerWindow(_settings) { Owner = this };
        if (dialog.ShowDialog() == true && _webView?.CoreWebView2 is not null)
        {
            ServerLabel.Text = _settings.ServerUrl;
            _webView.CoreWebView2.Navigate(_settings.ServerUrl.TrimEnd('/') + "/");
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _webView?.Dispose();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
#if DEBUG
        return;
#else
        if (e.Key == System.Windows.Input.Key.F5 || e.Key == System.Windows.Input.Key.F12)
            e.Handled = true;
#endif
    }
}
