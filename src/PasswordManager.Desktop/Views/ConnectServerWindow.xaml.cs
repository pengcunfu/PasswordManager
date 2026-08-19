using System.Net.Http;
using System.Windows;
using PasswordManager.Desktop.Services;

namespace PasswordManager.Desktop.Views;

public partial class ConnectServerWindow : Window
{
    private readonly DesktopSettings _settings;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public ConnectServerWindow(DesktopSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        if (!string.IsNullOrWhiteSpace(settings.ServerUrl))
            UrlBox.Text = settings.ServerUrl;
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        var url = Normalize(UrlBox.Text);
        if (url is null)
        {
            StatusText.Text = "请输入有效的 http(s) 地址";
            StatusText.Foreground = System.Windows.Media.Brushes.IndianRed;
            return;
        }

        StatusText.Foreground = System.Windows.Media.Brushes.Gray;
        StatusText.Text = "正在检测...";

        try
        {
            var resp = await Http.GetAsync(new Uri(new Uri(url), "/api/health"));
            if (resp.IsSuccessStatusCode)
            {
                StatusText.Foreground = System.Windows.Media.Brushes.SeaGreen;
                StatusText.Text = "连接成功";
            }
            else
            {
                StatusText.Foreground = System.Windows.Media.Brushes.IndianRed;
                StatusText.Text = $"服务返回 HTTP {(int)resp.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            StatusText.Foreground = System.Windows.Media.Brushes.IndianRed;
            StatusText.Text = $"无法连接: {ex.Message}";
        }
    }

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        var url = Normalize(UrlBox.Text);
        if (url is null)
        {
            StatusText.Text = "请输入有效的 http(s) 地址";
            StatusText.Foreground = System.Windows.Media.Brushes.IndianRed;
            return;
        }

        _settings.ServerUrl = url;
        _settings.Save();
        DialogResult = true;
        Close();
    }

    private static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim().TrimEnd('/');
        if (!text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            text = "http://" + text;
        return Uri.TryCreate(text, UriKind.Absolute, out _) ? text : null;
    }
}
