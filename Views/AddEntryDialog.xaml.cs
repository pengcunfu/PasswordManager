using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using PasswordManager.Models;
using PasswordManager.Services;

namespace PasswordManager.Views
{
    /// <summary>
    /// AddEntryDialog.xaml 的交互逻辑
    /// </summary>
    public partial class AddEntryDialog : Window
    {
        private readonly StorageManager _storageManager;
        private readonly PasswordEntry? _editingEntry;
        private bool _isPasswordVisible;
        private bool _isEditMode;
        private readonly List<CustomFieldControl> _customFieldControls = new();

        public AddEntryDialog(StorageManager storageManager, PasswordEntry? editingEntry = null)
        {
            InitializeComponent();
            _storageManager = storageManager;
            _editingEntry = editingEntry;
            _isEditMode = editingEntry != null;

            InitializeDialog();
        }

       /// <summary>
       /// 初始化对话框
       /// </summary>
       private void InitializeDialog()
       {
            // 加载分组列表
            LoadGroups();
            
            if (_isEditMode)
            {
                DialogTitle.Text = "编辑密码";
                SaveButton.Content = "更新";

                // 填充现有数据
                TitleTextBox.Text = _editingEntry!.Title;
                UsernameTextBox.Text = _editingEntry.Username;
                PasswordBox.Password = _editingEntry.Password;
                URLTextBox.Text = _editingEntry.URL;
                CategoryTextBox.Text = _editingEntry.Category;
                NotesTextBox.Text = _editingEntry.Notes;

                // 设置当前分组
                SetCurrentGroup(_editingEntry.GroupId);

                UpdatePasswordStrength(_editingEntry.Password);

                // 加载自定义字段
                foreach (var field in _editingEntry.CustomFields)
                {
                    AddCustomFieldRow(field.Key, field.Value, field.IsHidden);
                }
            }
            else
            {
                DialogTitle.Text = "添加新密码";
                SaveButton.Content = "保存";
                
                // 如果有当前选中的分组，默认选中它（从主窗口传递）
                // 这里需要通过其他方式传递，暂时保持默认不选择分组
            }

            // 聚焦到标题输入框
            Loaded += (s, e) => TitleTextBox.Focus();
        }

        /// <summary>
        /// 加载分组列表
        /// </summary>
        private void LoadGroups()
        {
            var groups = _storageManager.GetGroups();
            
            // 添加"无分组"选项
            var noGroupOption = new Group { Name = "无分组", Id = "" };
            var displayList = new List<Group> { noGroupOption };
            displayList.AddRange(groups.OrderBy(g => g.SortOrder).ThenBy(g => g.Name));
            
            GroupComboBox.ItemsSource = displayList;
            GroupComboBox.SelectedIndex = 0; // 默认选择"无分组"
        }

        /// <summary>
        /// 设置当前分组
        /// </summary>
        private void SetCurrentGroup(string groupId)
        {
            if (GroupComboBox.ItemsSource is List<Group> groups)
            {
                var selectedGroup = groups.FirstOrDefault(g => g.Id == groupId);
                if (selectedGroup != null)
                {
                    GroupComboBox.SelectedItem = selectedGroup;
                }
                else
                {
                    GroupComboBox.SelectedIndex = 0; // 选择"无分组"
                }
            }
        }

        /// <summary>
        /// 添加预设邮箱字段
        /// </summary>
        private void AddPresetEmail_Click(object sender, RoutedEventArgs e)
        {
            AddCustomFieldRow("邮箱", "", false);
        }

        /// <summary>
        /// 添加预设手机号字段
        /// </summary>
        private void AddPresetPhone_Click(object sender, RoutedEventArgs e)
        {
            AddCustomFieldRow("手机号", "", false);
        }

        /// <summary>
        /// 添加预设密保手机字段
        /// </summary>
        private void AddPresetSecurePhone_Click(object sender, RoutedEventArgs e)
        {
            AddCustomFieldRow("密保手机", "", true);
        }

        /// <summary>
        /// 添加自定义字段按钮点击
        /// </summary>
        private void AddCustomFieldButton_Click(object sender, RoutedEventArgs e)
        {
            AddCustomFieldRow("", "", false);
        }

        /// <summary>
        /// 添加一行自定义字段
        /// </summary>
        private void AddCustomFieldRow(string key, string value, bool isHidden)
        {
            var control = new CustomFieldControl(key, value, isHidden);
            control.RemoveClicked += (s, e) =>
            {
                CustomFieldsPanel.Children.Remove(control);
                _customFieldControls.Remove(control);
            };
            _customFieldControls.Add(control);
            CustomFieldsPanel.Children.Add(control);
        }

        /// <summary>
        /// 获取所有自定义字段数据
        /// </summary>
        private List<CustomField> CollectCustomFields()
        {
            var fields = new List<CustomField>();
            foreach (var control in _customFieldControls)
            {
                var (key, value, isHidden) = control.GetFieldData();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    fields.Add(new CustomField(key.Trim(), value ?? "", isHidden));
                }
            }
            return fields;
        }

        private void ShowPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isPasswordVisible)
            {
                // 隐藏密码
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordBox.Visibility = Visibility.Visible;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                ShowPasswordButton.Content = "显示";
                _isPasswordVisible = false;
            }
            else
            {
                // 显示密码
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordBox.Visibility = Visibility.Collapsed;
                PasswordTextBox.Visibility = Visibility.Visible;
                ShowPasswordButton.Content = "隐藏";
                _isPasswordVisible = true;
            }
        }

        private void GeneratePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            var generatorDialog = new PasswordGeneratorDialog();
            generatorDialog.Owner = this;
            
            if (generatorDialog.ShowDialog() == true && !string.IsNullOrEmpty(generatorDialog.GeneratedPassword))
            {
                PasswordBox.Password = generatorDialog.GeneratedPassword;
                PasswordTextBox.Text = generatorDialog.GeneratedPassword;
                UpdatePasswordStrength(generatorDialog.GeneratedPassword);
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!_isPasswordVisible)
            {
                UpdatePasswordStrength(PasswordBox.Password);
            }
        }

        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isPasswordVisible)
            {
                PasswordBox.Password = PasswordTextBox.Text;
                UpdatePasswordStrength(PasswordTextBox.Text);
            }
        }

        /// <summary>
        /// 更新密码强度显示
        /// </summary>
        private void UpdatePasswordStrength(string password)
        {
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

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateAndSave())
            {
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 验证并保存
        /// </summary>
        private bool ValidateAndSave()
        {
            // 验证必填字段
            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                MessageBox.Show("请输入标题", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                MessageBox.Show("请输入用户名", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                UsernameTextBox.Focus();
                return false;
            }

            string password = _isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("请输入密码", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (_isPasswordVisible)
                    PasswordTextBox.Focus();
                else
                    PasswordBox.Focus();
                return false;
            }

           try
           {
               var customFields = CollectCustomFields();
                var selectedGroup = GroupComboBox.SelectedItem as Group;
                string groupId = selectedGroup?.Id ?? "";

                if (_isEditMode)
                {
                    // 更新现有条目
                    var entry = _editingEntry!;
                    entry.Title = TitleTextBox.Text.Trim();
                    entry.Username = UsernameTextBox.Text.Trim();
                    entry.Password = password;
                    entry.URL = URLTextBox.Text?.Trim() ?? "";
                    entry.Category = CategoryTextBox.Text?.Trim() ?? "";
                    entry.Notes = NotesTextBox.Text?.Trim() ?? "";
                    entry.GroupId = groupId;
                    entry.CustomFields = customFields;

                    _storageManager.UpdateEntry(entry);
                    MessageBox.Show("密码条目已更新", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // 创建新条目
                    var entry = new PasswordEntry(
                        TitleTextBox.Text.Trim(),
                        UsernameTextBox.Text.Trim(),
                        password,
                        URLTextBox.Text?.Trim() ?? "",
                        NotesTextBox.Text?.Trim() ?? "",
                        CategoryTextBox.Text?.Trim() ?? "",
                        groupId
                    );
                    entry.CustomFields = customFields;

                    _storageManager.AddEntry(entry);
                    MessageBox.Show("密码条目已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}
