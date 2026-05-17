using System;
using System.Windows;
using PasswordManager.Services;
using PasswordManager.Views;

namespace PasswordManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
#if DEBUG
                Console.WriteLine("[PasswordManager] Starting (Debug / console mode)...");
#endif
                // 获取用户数据目录
                string dataDir = StorageManager.GetUserDataDirectory();
#if DEBUG
                Console.WriteLine($"[PasswordManager] Data directory: {dataDir}");
#endif
                
                // 创建并显示登录窗口
                var loginWindow = new LoginWindow(dataDir, (storageManager) =>
                {
                    // 登录成功后创建主窗口
                    var mainWindow = new MainWindow(storageManager, dataDir);
                    mainWindow.Show();
                });
                
                loginWindow.Show();
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.Error.WriteLine($"[PasswordManager] Startup failed: {ex}");
#endif
                MessageBox.Show($"应用程序启动失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void Application_DispatcherUnhandledException(object sender, 
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
#if DEBUG
            Console.Error.WriteLine($"[PasswordManager] Unhandled exception: {e.Exception}");
#endif
            MessageBox.Show($"发生未处理的异常: {e.Exception.Message}", "错误", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
