using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PasswordManager.Services;

namespace PasswordManager.Views
{
    /// <summary>
    /// LoginWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LoginWindow : Window
    {
        private readonly string _dataDir;
        private readonly Action<StorageManager> _onLoginSuccess;
        private bool _isFirstTime;

        public LoginWindow(string dataDir, Action<StorageManager> onLoginSuccess)
        {
            InitializeComponent();
            _dataDir = dataDir;
            _onLoginSuccess = onLoginSuccess;
            
            CheckFirstTimeSetup();
            SetupEventHandlers();
        }

        /// <summary>
        /// 检查是否为首次设置
        /// </summary>
        private void CheckFirstTimeSetup()
        {
            string dataFile = Path.Combine(_dataDir, "passwords.json");
            _isFirstTime = !File.Exists(dataFile);

            if (_isFirstTime)
            {
                ShowFirstTimeSetup();
            }
            else
            {
                ShowLogin();
            }
        }

        /// <summary>
        /// 显示首次设置界面
        /// </summary>
        private void ShowFirstTimeSetup()
        {
            Title = "密码管家 - 首次设置";
            TitleTextBlock.Text = "欢迎使用密码管家";
            SubtitleTextBlock.Text = "请设置您的主密码";
            
            ConfirmPasswordLabel.Visibility = Visibility.Visible;
            ConfirmPasswordBox.Visibility = Visibility.Visible;
            StrengthLabel.Visibility = Visibility.Visible;
            
            LoginButton.Content = "创建密码库";
        }

        /// <summary>
        /// 显示登录界面
        /// </summary>
        private void ShowLogin()
        {
            Title = "密码管家 - 登录";
            TitleTextBlock.Text = "密码管家";
            SubtitleTextBlock.Text = "请输入主密码解锁";
            
            ConfirmPasswordLabel.Visibility = Visibility.Collapsed;
            ConfirmPasswordBox.Visibility = Visibility.Collapsed;
            StrengthLabel.Visibility = Visibility.Collapsed;
            
            LoginButton.Content = "解锁";
        }

        /// <summary>
        /// 设置事件处理程序
        /// </summary>
        private void SetupEventHandlers()
        {
            // 密码输入框事件
            PasswordBox.PasswordChanged += PasswordBox_PasswordChanged;
            PasswordBox.KeyDown += PasswordBox_KeyDown;
            ConfirmPasswordBox.KeyDown += ConfirmPasswordBox_KeyDown;
            
            // 窗口加载完成后聚焦到密码输入框
            Loaded += (s, e) => PasswordBox.Focus();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isFirstTime)
            {
                // 检查密码强度
                var (strength, score) = CryptoManager.CheckPasswordStrength(PasswordBox.Password);
                StrengthLabel.Text = $"密码强度: {strength}";
                StrengthLabel.Foreground = score switch
                {
                    <= 2 => System.Windows.Media.Brushes.Red,
                    <= 4 => System.Windows.Media.Brushes.Orange,
                    <= 6 => System.Windows.Media.Brushes.Green,
                    _ => System.Windows.Media.Brushes.DarkGreen
                };
            }
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            e.Handled = true;

            if (_isFirstTime)
                ConfirmPasswordBox.Focus();
            else
                LoginButton_Click(sender, e);
        }

        private void ConfirmPasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            e.Handled = true;
            LoginButton_Click(sender, e);
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isFirstTime)
            {
                HandleFirstTimeSetup();
            }
            else
            {
                HandleLogin();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        /// <summary>
        /// 处理首次设置
        /// </summary>
        private void HandleFirstTimeSetup()
        {
            string password = PasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;

            // 验证输入
            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("请输入主密码", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
                return;
            }

            if (password != confirmPassword)
            {
                MessageBox.Show("两次输入的密码不一致", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                ConfirmPasswordBox.Focus();
                return;
            }

            // 检查密码强度
            var (strength, score) = CryptoManager.CheckPasswordStrength(password);
            if (score < 3)
            {
                var result = MessageBox.Show(
                    $"您的密码强度为: {strength}\n\n建议使用更强的密码以提高安全性。\n\n是否继续使用当前密码？",
                    "密码强度较弱",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    PasswordBox.Focus();
                    return;
                }
            }

            try
            {
                CreateNewDatabase(password);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建密码库失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 创建新数据库
        /// </summary>
        private void CreateNewDatabase(string password)
        {
            // 生成盐值
            string salt = CryptoManager.GenerateSalt();

            // 直接创建数据库文件，保存salt
            string dataFile = Path.Combine(_dataDir, "passwords.json");
            var initialData = new
            {
                salt = salt,
                version = "1.0",
                created_at = DateTime.Now.ToString("O"),
                entries = new object[] { }
            };

            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string jsonData = System.Text.Json.JsonSerializer.Serialize(initialData, options);
            File.WriteAllText(dataFile, jsonData);

            // 创建加密管理器
            var cryptoManager = new CryptoManager(password, salt);

            // 创建存储管理器并加载数据库
            var storageManager = new StorageManager(_dataDir, cryptoManager);
            storageManager.LoadDatabase();

            // 添加示例数据
            SampleDataService.AddSampleData(storageManager);

            MessageBox.Show("密码库创建成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);

            // 关闭登录窗口并回调
            _onLoginSuccess?.Invoke(storageManager);
            Close();
        }

        /// <summary>
        /// 处理登录
        /// </summary>
        private void HandleLogin()
        {
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("请输入主密码", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
                return;
            }

            try
            {
                // 首先读取数据库文件获取salt
                string dataFile = Path.Combine(_dataDir, "passwords.json");
                if (!File.Exists(dataFile))
                {
                    MessageBox.Show("数据库文件不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string jsonData = File.ReadAllText(dataFile);
                using var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonData);
                var root = jsonDoc.RootElement;

                // 获取salt
                if (!root.TryGetProperty("salt", out var saltElement))
                {
                    MessageBox.Show("无法读取数据库盐值", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                string salt = saltElement.GetString() ?? "";

                // 使用正确的salt创建加密管理器
                var cryptoManager = new CryptoManager(password, salt);
                var storageManager = new StorageManager(_dataDir, cryptoManager);

                // 尝试加载数据库来验证密码
                storageManager.LoadDatabase();

                // 密码正确，关闭登录窗口并回调
                _onLoginSuccess?.Invoke(storageManager);
                Close();
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("解密"))
                {
                    MessageBox.Show("主密码错误，请重试", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show($"加载数据库失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                PasswordBox.Focus();
            }
        }
    }
}
