using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using PasswordManager.Services;

namespace PasswordManager.Views
{
    /// <summary>
    /// AiSettingsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class AiSettingsWindow : Window
    {
        private readonly AiConfig _config;

        public AiSettingsWindow(AiConfig config)
        {
            InitializeComponent();
            _config = config;
            LoadConfig();
        }

        /// <summary>
        /// 加载配置到界面
        /// </summary>
        private void LoadConfig()
        {
            ApiEndpointTextBox.Text = _config.ApiEndpoint;
            ApiKeyPasswordBox.Password = _config.ApiKey;
            ModelTextBox.Text = _config.Model;
            MaxTokensTextBox.Text = _config.MaxTokens.ToString();
            TemperatureTextBox.Text = _config.Temperature.ToString("F1");
        }

        /// <summary>
        /// 从界面保存配置
        /// </summary>
        private bool SaveConfigFromUI()
        {
            string endpoint = ApiEndpointTextBox.Text.Trim();
            string apiKey = ApiKeyPasswordBox.Password.Trim();
            string model = ModelTextBox.Text.Trim();
            string maxTokensStr = MaxTokensTextBox.Text.Trim();
            string temperatureStr = TemperatureTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                StatusText.Text = "请输入 API 地址";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                return false;
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                StatusText.Text = "请输入 API 密钥";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                return false;
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                StatusText.Text = "请输入模型名称";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                return false;
            }

            if (!int.TryParse(maxTokensStr, out int maxTokens) || maxTokens <= 0)
            {
                StatusText.Text = "最大 Token 数必须为正整数";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                return false;
            }

            if (!double.TryParse(temperatureStr, out double temperature) ||
                temperature < 0 || temperature > 2)
            {
                StatusText.Text = "温度必须在 0.0 到 2.0 之间";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                return false;
            }

            _config.ApiEndpoint = endpoint;
            _config.ApiKey = apiKey;
            _config.Model = model;
            _config.MaxTokens = maxTokens;
            _config.Temperature = temperature;

            return true;
        }

        /// <summary>
        /// 测试连接
        /// </summary>
        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SaveConfigFromUI())
                return;

            TestButton.IsEnabled = false;
            StatusText.Text = "正在测试连接...";
            StatusText.Foreground = System.Windows.Media.Brushes.Gray;

            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _config.ApiKey);
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                string endpoint = _config.ApiEndpoint.TrimEnd('/');
                string url = $"{endpoint}/chat/completions";

                var requestBody = new
                {
                    model = _config.Model,
                    messages = new[]
                    {
                        new { role = "user", content = "Hello" }
                    },
                    max_tokens = 10
                };

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                string json = JsonSerializer.Serialize(requestBody, options);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    StatusText.Text = "连接成功！API 配置正确。";
                    StatusText.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    StatusText.Text = $"连接失败: HTTP {(int)response.StatusCode} - {errorBody[..Math.Min(200, errorBody.Length)]}";
                    StatusText.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            catch (TaskCanceledException)
            {
                StatusText.Text = "连接超时，请检查 API 地址是否正确";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"连接失败: {ex.Message}";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
            finally
            {
                TestButton.IsEnabled = true;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SaveConfigFromUI())
                return;

            try
            {
                _config.Save();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"保存失败: {ex.Message}";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
