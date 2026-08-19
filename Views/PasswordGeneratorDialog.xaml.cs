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

        private void SymbolCountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SymbolCountLabel != null)
            {
                SymbolCountLabel.Text = $"特殊符号位数: {(int)SymbolCountSlider.Value}";
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
                int symbolCount = IncludeSymbolsCheckBox.IsChecked == true ? (int)SymbolCountSlider.Value : 0;

                // 检查至少选择了一种字符类型
                if (IncludeUppercaseCheckBox.IsChecked != true &&
                    IncludeLowercaseCheckBox.IsChecked != true &&
                    IncludeDigitsCheckBox.IsChecked != true &&
                    IncludeSymbolsCheckBox.IsChecked != true)
                {
                    MessageBox.Show("请至少选择一种字符类型", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 检查特殊符号位数不超过密码长度
                if (symbolCount > length)
                {
                    MessageBox.Show("特殊符号位数不能超过密码长度", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 生成密码（保证特殊符号位数）
                string password = GeneratePasswordWithGuaranteedChars(length, symbolCount);

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
        /// 生成密码，保证指定数量的特殊符号
        /// </summary>
        private string GeneratePasswordWithGuaranteedChars(int length, int symbolCount)
        {
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string digits = "0123456789";
            const string symbols = "!@#$%^&*()_+-=[]{}|;:,.<>?";

            var random = new Random();
            var password = new char[length];

            // 1. 先填充特殊符号到随机位置
            var positions = new int[length];
            for (int i = 0; i < length; i++) positions[i] = i;

            // Fisher-Yates 洗牌取前 symbolCount 个位置
            for (int i = length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (positions[i], positions[j]) = (positions[j], positions[i]);
            }

            for (int i = 0; i < symbolCount; i++)
            {
                password[positions[i]] = symbols[random.Next(symbols.Length)];
            }

            // 2. 构建剩余字符集（排除已关闭的类型）
            var remainingCharset = new StringBuilder();
            if (IncludeUppercaseCheckBox.IsChecked == true) remainingCharset.Append(uppercase);
            if (IncludeLowercaseCheckBox.IsChecked == true) remainingCharset.Append(lowercase);
            if (IncludeDigitsCheckBox.IsChecked == true) remainingCharset.Append(digits);
            if (IncludeSymbolsCheckBox.IsChecked == true) remainingCharset.Append(symbols);

            // 如果剩余字符集为空，只用已选中的类型
            string charset = remainingCharset.Length > 0 ? remainingCharset.ToString() : lowercase;

            // 3. 填充剩余位置
            for (int i = symbolCount; i < length; i++)
            {
                password[positions[i]] = charset[random.Next(charset.Length)];
            }

            return new string(password);
        }
    }
}
