using System.Windows;
using PasswordManager.Desktop.Services;
using PasswordManager.Desktop.Views;

namespace PasswordManager.Desktop;

public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        try
        {
            var settings = DesktopSettings.Load();
            if (string.IsNullOrWhiteSpace(settings.ServerUrl))
            {
                var dialog = new ConnectServerWindow(settings);
                var ok = dialog.ShowDialog() == true;
                if (!ok || string.IsNullOrWhiteSpace(settings.ServerUrl))
                {
                    Shutdown();
                    return;
                }
            }

            var main = new MainWebViewWindow(settings);
            main.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"应用程序启动失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void Application_DispatcherUnhandledException(object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"发生未处理的异常: {e.Exception.Message}", "错误",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
