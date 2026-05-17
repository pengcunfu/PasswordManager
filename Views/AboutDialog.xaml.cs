using System.Reflection;
using System.Windows;

namespace PasswordManager.Views
{
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = version != null ? $"版本 {version.Major}.{version.Minor}" : "版本 2.0";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
