using System.Windows;
using PasswordManager.Models;

namespace PasswordManager.Views
{
    /// <summary>
    /// GroupDialog.xaml 的交互逻辑
    /// </summary>
    public partial class GroupDialog : Window
    {
        private Group? _existingGroup;
        private static readonly string[] AvailableColors = 
        {
            "#4A90E2", // 蓝色
            "#50E3C2", // 青色
            "#B8E986", // 绿色
            "#F5A623", // 橙色
            "#E04F5F", // 红色
            "#9013FE", // 紫色
            "#BD10E0", // 紫红色
            "#4A4A4A"  // 深灰色
        };

        public GroupDialog()
        {
            InitializeComponent();
            _existingGroup = null;
            InitializeColors();
            GroupNameTextBox.Focus();
        }

        public GroupDialog(Group group) : this()
        {
            _existingGroup = group;
            Title = "编辑分组";
            
            // 填充现有数据
            GroupNameTextBox.Text = group.Name;
            GroupDescriptionTextBox.Text = group.Description;
            
            // 选择对应的颜色
            foreach (var color in AvailableColors)
            {
                if (color == group.Color)
                {
                    ColorComboBox.SelectedItem = color;
                    break;
                }
            }
        }

        private void InitializeColors()
        {
            ColorComboBox.ItemsSource = AvailableColors;
            ColorComboBox.SelectedIndex = 0; // 默认选择第一个颜色
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 验证输入
            if (string.IsNullOrWhiteSpace(GroupNameTextBox.Text))
            {
                MessageBox.Show("请输入分组名称", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                GroupNameTextBox.Focus();
                return;
            }

            try
            {
                if (_existingGroup != null)
                {
                    // 更新现有分组
                    _existingGroup.Name = GroupNameTextBox.Text.Trim();
                    _existingGroup.Description = GroupDescriptionTextBox.Text.Trim();
                    _existingGroup.Color = ColorComboBox.SelectedItem?.ToString() ?? "#4A90E2";
                    _existingGroup.UpdateModifiedTime();
                }
                else
                {
                    // 创建新分组
                    var newGroup = new Group(
                        GroupNameTextBox.Text.Trim(),
                        GroupDescriptionTextBox.Text.Trim(),
                        ColorComboBox.SelectedItem?.ToString() ?? "#4A90E2"
                    );
                    
                    // 设置排序顺序（添加到最后）
                    newGroup.SortOrder = AvailableColors.Length;
                    
                    _existingGroup = newGroup;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取分组对象
        /// </summary>
        public Group? GetGroup()
        {
            return _existingGroup;
        }
    }
}
