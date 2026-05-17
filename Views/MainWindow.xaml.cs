using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PasswordManager.Models;
using PasswordManager.Services;

namespace PasswordManager.Views
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly StorageManager _storageManager;
        private readonly string _dataDir;
        private List<PasswordEntry> _allEntries;
        private List<PasswordEntry> _filteredEntries;
        private PasswordEntry _selectedEntry;
        private bool _isPasswordVisible;

        public MainWindow(StorageManager storageManager, string dataDir)
        {
            InitializeComponent();
            _storageManager = storageManager;
            _dataDir = dataDir;
            
            LoadEntries();
            SetupPlaceholder();
        }

        /// <summary>
        /// 加载密码条目
        /// </summary>
        private void LoadEntries()
        {
            _allEntries = _storageManager.GetAllEntries();
            _filteredEntries = _allEntries.ToList();
            EntryListBox.ItemsSource = _filteredEntries;
        }

        /// <summary>
        /// 设置搜索框占位符
        /// </summary>
        private void SetupPlaceholder()
        {
            SearchTextBox.GotFocus += (s, e) =>
            {
                if (SearchTextBox.Text == "搜索密码...")
                {
                    SearchTextBox.Text = "";
                    SearchTextBox.Foreground = System.Windows.Media.Brushes.Black;
                }
            };

            SearchTextBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
                {
                    SearchTextBox.Text = "搜索密码...";
                    SearchTextBox.Foreground = System.Windows.Media.Brushes.Gray;
                }
            };

            SearchTextBox.Text = "搜索密码...";
            SearchTextBox.Foreground = System.Windows.Media.Brushes.Gray;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchTextBox.Text == "搜索密码..." || SearchTextBox.Foreground == System.Windows.Media.Brushes.Gray)
                return;

            FilterEntries(SearchTextBox.Text);
        }

        /// <summary>
        /// 过滤密码条目
        /// </summary>
        private void FilterEntries(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                _filteredEntries = _allEntries.ToList();
            }
            else
            {
                _filteredEntries = _storageManager.SearchEntries(searchText);
            }
            
            EntryListBox.ItemsSource = _filteredEntries;
        }

        private void EntryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EntryListBox.SelectedItem is PasswordEntry entry)
            {
                ShowEntryDetails(entry);
            }
        }

        /// <summary>
        /// 显示条目详情
        /// </summary>
        private void ShowEntryDetails(PasswordEntry entry)
        {
            _selectedEntry = entry;
            _isPasswordVisible = false;

            WelcomeText.Visibility = Visibility.Collapsed;
            DetailContent.Visibility = Visibility.Visible;

            DetailTitle.Text = entry.Title;
            DetailUsername.Text = entry.Username;
            DetailPassword.Text = "••••••••";
            DetailURL.Text = entry.URL;
            DetailCategory.Text = entry.Category;
            DetailNotes.Text = entry.Notes;
            DetailCreatedAt.Text = $"创建时间: {entry.CreatedAt:yyyy-MM-dd HH:mm:ss}";
            DetailUpdatedAt.Text = $"更新时间: {entry.UpdatedAt:yyyy-MM-dd HH:mm:ss}";

            ShowPasswordButton.Content = "显示";
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var addDialog = new AddEntryDialog(_storageManager);
            addDialog.Owner = this;
            
            if (addDialog.ShowDialog() == true)
            {
                LoadEntries();
                FilterEntries(SearchTextBox.Text == "搜索密码..." ? "" : SearchTextBox.Text);
            }
        }

        private void GeneratePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            var generatorDialog = new PasswordGeneratorDialog();
            generatorDialog.Owner = this;
            generatorDialog.ShowDialog();
        }

        private void BackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _storageManager.CreateBackup();
                MessageBox.Show("备份创建成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"备份创建失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var aboutDialog = new AboutDialog();
            aboutDialog.Owner = this;
            aboutDialog.ShowDialog();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要注销并返回登录界面吗？", "注销确认", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var loginWindow = new LoginWindow(_dataDir, (storageManager) =>
                {
                    var newMainWindow = new MainWindow(storageManager, _dataDir);
                    newMainWindow.Show();
                });
                
                loginWindow.Show();
                Close();
            }
        }

        private void CopyUsernameButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEntry != null)
            {
                Clipboard.SetText(_selectedEntry.Username ?? "");
                MessageBox.Show("用户名已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ShowPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEntry == null) return;

            if (_isPasswordVisible)
            {
                DetailPassword.Text = "••••••••";
                ShowPasswordButton.Content = "显示";
                _isPasswordVisible = false;
            }
            else
            {
                DetailPassword.Text = _selectedEntry.Password;
                ShowPasswordButton.Content = "隐藏";
                _isPasswordVisible = true;
            }
        }

        private void CopyPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEntry != null)
            {
                Clipboard.SetText(_selectedEntry.Password ?? "");
                MessageBox.Show("密码已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEntry == null) return;

            var editDialog = new AddEntryDialog(_storageManager, _selectedEntry);
            editDialog.Owner = this;
            
            if (editDialog.ShowDialog() == true)
            {
                LoadEntries();
                FilterEntries(SearchTextBox.Text == "搜索密码..." ? "" : SearchTextBox.Text);
                
                // 重新选择更新后的条目
                var updatedEntry = _filteredEntries.FirstOrDefault(e => e.Id == _selectedEntry.Id);
                if (updatedEntry != null)
                {
                    EntryListBox.SelectedItem = updatedEntry;
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedEntry == null) return;

            var result = MessageBox.Show(
                $"确定要删除密码条目 '{_selectedEntry.Title}' 吗？\n\n此操作不可撤销。",
                "删除确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _storageManager.DeleteEntry(_selectedEntry.Id);
                    LoadEntries();
                    FilterEntries(SearchTextBox.Text == "搜索密码..." ? "" : SearchTextBox.Text);
                    
                    // 隐藏详情面板
                    WelcomeText.Visibility = Visibility.Visible;
                    DetailContent.Visibility = Visibility.Collapsed;
                    _selectedEntry = null;
                    
                    MessageBox.Show("密码条目已删除", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
