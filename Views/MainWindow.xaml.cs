using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
       private List<PasswordEntry> _allEntries = [];
       private List<PasswordEntry> _filteredEntries = [];
        private List<Group> _groups = [];
        private string? _selectedGroupId;
       private PasswordEntry? _selectedEntry;
       private bool _isPasswordVisible;

       public MainWindow(StorageManager storageManager, string dataDir)
       {
           InitializeComponent();
           _storageManager = storageManager;
           _dataDir = dataDir;
           
           LoadEntries();
            LoadGroups();
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
        /// 加载分组列表
        /// </summary>
        private void LoadGroups()
        {
            _groups = _storageManager.GetGroups();
            _groups = _groups.OrderBy(g => g.SortOrder).ThenBy(g => g.Name).ToList();
            GroupListBox.ItemsSource = _groups;
        }

        /// <summary>
        /// 根据分组筛选密码条目
        /// </summary>
        private void FilterEntriesByGroup(string? groupId)
        {
            _selectedGroupId = groupId;
            
            if (string.IsNullOrEmpty(groupId))
            {
                // 显示所有密码
                _filteredEntries = _allEntries.ToList();
            }
            else
            {
                // 显示指定分组的密码
                _filteredEntries = _allEntries.Where(e => e.GroupId == groupId).ToList();
            }
            
            // 应用当前的搜索过滤
            if (SearchTextBox.Text != "搜索密码..." && SearchTextBox.Foreground != System.Windows.Media.Brushes.Gray)
            {
                string searchText = SearchTextBox.Text.ToLowerInvariant();
                _filteredEntries = _filteredEntries.Where(entry =>
                    (entry.Title?.ToLowerInvariant().Contains(searchText) ?? false) ||
                    (entry.Username?.ToLowerInvariant().Contains(searchText) ?? false) ||
                    (entry.URL?.ToLowerInvariant().Contains(searchText) ?? false) ||
                    (entry.Category?.ToLowerInvariant().Contains(searchText) ?? false) ||
                    (entry.Notes?.ToLowerInvariant().Contains(searchText) ?? false) ||
                    entry.CustomFields.Any(f =>
                        (f.Key?.ToLowerInvariant().Contains(searchText) ?? false) ||
                        (f.Value?.ToLowerInvariant().Contains(searchText) ?? false))
                ).ToList();
            }
            
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

            // 显示自定义字段
            ShowCustomFields(entry.CustomFields);
        }

        /// <summary>
        /// 显示自定义字段
        /// </summary>
        private void ShowCustomFields(List<CustomField> customFields)
        {
            CustomFieldsDetailPanel.Children.Clear();

            if (customFields == null || customFields.Count == 0)
                return;

            foreach (var field in customFields)
            {
                var groupBox = new GroupBox
                {
                    Header = field.Key,
                    Style = (Style)FindResource("NaiveGroupBox")
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var textBox = new TextBox
                {
                    Style = (Style)FindResource("ReadOnlyTextBox"),
                    Text = field.IsHidden ? "••••••••" : field.Value,
                    Tag = field // 保存引用以便切换显示
                };
                Grid.SetColumn(textBox, 0);

                var copyButton = new Button
                {
                    Content = "复制",
                    Style = (Style)FindResource("NaiveSecondaryButton"),
                    Width = 70,
                    Tag = field.Value // 保存真实值用于复制
                };
                copyButton.Click += (s, e) =>
                {
                    if (copyButton.Tag is string val)
                    {
                        Clipboard.SetText(val);
                        MessageBox.Show($"{field.Key} 已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                };
                Grid.SetColumn(copyButton, 1);

                if (field.IsHidden)
                {
                    var showButton = new Button
                    {
                        Content = "显示",
                        Style = (Style)FindResource("NaiveSecondaryButton"),
                        Width = 70,
                        Margin = new Thickness(0, 0, 8, 0)
                    };

                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    Grid.SetColumn(showButton, 1);
                    Grid.SetColumn(copyButton, 2);

                    bool isVisible = false;
                    showButton.Click += (s, e) =>
                    {
                        if (isVisible)
                        {
                            textBox.Text = "••••••••";
                            showButton.Content = "显示";
                            isVisible = false;
                        }
                        else
                        {
                            textBox.Text = field.Value;
                            showButton.Content = "隐藏";
                            isVisible = true;
                        }
                    };

                    grid.Children.Add(textBox);
                    grid.Children.Add(showButton);
                    grid.Children.Add(copyButton);
                }
                else
                {
                    grid.Children.Add(textBox);
                    grid.Children.Add(copyButton);
                }

                groupBox.Content = grid;
                CustomFieldsDetailPanel.Children.Add(groupBox);
            }
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

        private void AiAssistantButton_Click(object sender, RoutedEventArgs e)
        {
            var aiChatWindow = new AiChatWindow(_storageManager, _dataDir);
            aiChatWindow.Owner = this;
            aiChatWindow.Show();
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

       private void RefreshButton_Click(object sender, RoutedEventArgs e)
       {
           try
           {
               LoadEntries();
                LoadGroups();
               FilterEntries(SearchTextBox.Text == "搜索密码..." ? "" : SearchTextBox.Text);
               
               // 如果之前有选中的条目，尝试重新选中它
               if (_selectedEntry != null)
               {
                   var updatedEntry = _filteredEntries.FirstOrDefault(e => e.Id == _selectedEntry.Id);
                   if (updatedEntry != null)
                   {
                       EntryListBox.SelectedItem = updatedEntry;
                       ShowEntryDetails(updatedEntry);
                   }
                   else
                   {
                       // 如果条目已被删除，隐藏详情面板
                       WelcomeText.Visibility = Visibility.Visible;
                       DetailContent.Visibility = Visibility.Collapsed;
                       _selectedEntry = null;
                   }
               }
           }
           catch (Exception ex)
           {
               MessageBox.Show($"刷新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
           }
       }

        private void AddGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var groupDialog = new GroupDialog();
            groupDialog.Owner = this;
            
            if (groupDialog.ShowDialog() == true)
            {
                var newGroup = groupDialog.GetGroup();
                if (newGroup != null)
                {
                    try
                    {
                        _storageManager.AddGroup(newGroup);
                        LoadGroups();
                        MessageBox.Show("分组添加成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"添加分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void EditGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string groupId)
            {
                var group = _groups.FirstOrDefault(g => g.Id == groupId);
                if (group == null) return;

                var groupDialog = new GroupDialog(group);
                groupDialog.Owner = this;

                if (groupDialog.ShowDialog() == true)
                {
                    var updatedGroup = groupDialog.GetGroup();
                    if (updatedGroup != null)
                    {
                        try
                        {
                            _storageManager.UpdateGroup(updatedGroup);
                            LoadGroups();
                            
                            // 重新应用分组筛选
                            if (_selectedGroupId == groupId)
                            {
                                FilterEntriesByGroup(groupId);
                            }
                            
                            MessageBox.Show("分组更新成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"更新分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string groupId)
            {
                var group = _groups.FirstOrDefault(g => g.Id == groupId);
                if (group == null) return;

                var result = MessageBox.Show(
                    $"确定要删除分组 '{group.Name}' 吗？\n\n该分组下的密码将移动到'全部密码'中。",
                    "删除确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        _storageManager.DeleteGroup(groupId);
                        LoadGroups();
                        
                        // 如果删除的是当前选中的分组，显示所有密码
                        if (_selectedGroupId == groupId)
                        {
                            _selectedGroupId = null;
                            LoadEntries();
                        }
                        else
                        {
                            // 重新应用分组筛选
                            FilterEntriesByGroup(_selectedGroupId);
                        }
                        
                        MessageBox.Show("分组删除成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"删除分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void GroupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupListBox.SelectedItem is Group selectedGroup)
            {
                FilterEntriesByGroup(selectedGroup.Id);
            }
        }

        private void AllEntriesBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            GroupListBox.SelectedItem = null;
            FilterEntriesByGroup(null);
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
                var updatedEntry = _filteredEntries.FirstOrDefault(e => e.Id == _selectedEntry!.Id);
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
