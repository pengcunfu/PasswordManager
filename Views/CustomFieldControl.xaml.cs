using System;
using System.Windows;
using System.Windows.Controls;

namespace PasswordManager.Views
{
    /// <summary>
    /// CustomFieldControl.xaml 的交互逻辑
    /// 自定义字段行控件：字段名 + 值 + 隐藏开关 + 删除按钮
    /// </summary>
    public partial class CustomFieldControl : UserControl
    {
        private bool _isHidden;

        /// <summary>
        /// 删除按钮点击事件
        /// </summary>
        public event EventHandler? RemoveClicked;

        public CustomFieldControl(string key, string value, bool isHidden)
        {
            InitializeComponent();

            KeyTextBox.Text = key;
            _isHidden = isHidden;

            if (_isHidden)
            {
                ValueTextBox.Visibility = Visibility.Collapsed;
                ValueHiddenTextBox.Visibility = Visibility.Visible;
                ValueHiddenTextBox.Text = value;
                ToggleHiddenButton.Content = "显示";
            }
            else
            {
                ValueTextBox.Text = value;
                ValueHiddenTextBox.Visibility = Visibility.Collapsed;
                ToggleHiddenButton.Content = "隐藏";
            }
        }

        /// <summary>
        /// 切换隐藏/显示
        /// </summary>
        private void ToggleHiddenButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isHidden)
            {
                // 切换为显示
                ValueTextBox.Text = ValueHiddenTextBox.Text;
                ValueTextBox.Visibility = Visibility.Visible;
                ValueHiddenTextBox.Visibility = Visibility.Collapsed;
                ToggleHiddenButton.Content = "隐藏";
                _isHidden = false;
            }
            else
            {
                // 切换为隐藏
                ValueHiddenTextBox.Text = ValueTextBox.Text;
                ValueHiddenTextBox.Visibility = Visibility.Visible;
                ValueTextBox.Visibility = Visibility.Collapsed;
                ToggleHiddenButton.Content = "显示";
                _isHidden = true;
            }
        }

        /// <summary>
        /// 删除按钮点击
        /// </summary>
        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            RemoveClicked?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 获取字段数据
        /// </summary>
        public (string Key, string Value, bool IsHidden) GetFieldData()
        {
            string value = _isHidden ? ValueHiddenTextBox.Text : ValueTextBox.Text;
            return (KeyTextBox.Text ?? "", value ?? "", _isHidden);
        }
    }
}
