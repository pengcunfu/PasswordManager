using System;
using System.Text;
using System.Windows;
using PasswordManager.Services;

namespace PasswordManager.Views
{
    /// <summary>
    /// PasswordGeneratorDialog.xaml 的交互逻辑
    /// </summary>
    public partial class PasswordGeneratorDialog : Window
    {
        public string? GeneratedPassword { get; private set; }

        public PasswordGeneratorDialog()
        {
            InitializeComponent();
            GeneratePassword();
        }

        private void LengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LengthLabel != null)
            {
                LengthLabel.Text = $"长度: {(int)LengthSlider.Value}";
            }
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            GeneratePassword();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(GeneratedPasswordTextBox.Text))
            {
                Clipboard.SetText(GeneratedPasswordTextBox.Text);
                MessageBox.Show("密码已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void UseButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(GeneratedPasswordTextBox.Text))
            {
                GeneratedPassword = GeneratedPasswordTextBox.Text;
                DialogResult = true;
                Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 生成密码
        /// </summary>
        private void GeneratePassword()
        {
            try
            {
                int length = (int)LengthSlider.Value;
                
                // 检查至少选择了一种字符类型
                if (IncludeUppercaseCheckBox.IsChecked != true &&
                    IncludeLowercaseCheckBox.IsChecked != true &&
                    IncludeDigitsCheckBox.IsChecked != true &&
                    IncludeSymbolsCheckBox.IsChecked != true)
                {
                    MessageBox.Show("请至少选择一种字符类型", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 构建字符集
                string charset = BuildCharset();
                
                // 生成密码
                string password = GeneratePasswordFromCharset(charset, length);
                
                GeneratedPasswordTextBox.Text = password;
                
                // 更新密码强度显示
                var (strength, score) = CryptoManager.CheckPasswordStrength(password);
                StrengthLabel.Text = $"密码强度: {strength}";
                StrengthLabel.Foreground = score switch
                {
                    <= 2 => System.Windows.Media.Brushes.Red,
                    <= 4 => System.Windows.Media.Brushes.Orange,
                    <= 6 => System.Windows.Media.Brushes.Green,
                    _ => System.Windows.Media.Brushes.DarkGreen
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"生成密码失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 构建字符集
        /// </summary>
        private string BuildCharset()
        {
            var charset = new StringBuilder();
            
            if (IncludeUppercaseCheckBox.IsChecked == true)
                charset.Append("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            
            if (IncludeLowercaseCheckBox.IsChecked == true)
                charset.Append("abcdefghijklmnopqrstuvwxyz");
            
            if (IncludeDigitsCheckBox.IsChecked == true)
                charset.Append("0123456789");
            
            if (IncludeSymbolsCheckBox.IsChecked == true)
                charset.Append("!@#$%^&*()_+-=[]{}|;:,.<>?");
            
            return charset.ToString();
        }

        /// <summary>
        /// 从字符集生成密码
        /// </summary>
        private string GeneratePasswordFromCharset(string charset, int length)
        {
            var random = new Random();
            var password = new StringBuilder(length);
            
            for (int i = 0; i < length; i++)
            {
                int index = random.Next(charset.Length);
                password.Append(charset[index]);
            }
            
            return password.ToString();
        }
    }
}
