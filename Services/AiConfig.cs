using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PasswordManager.Services
{
    /// <summary>
    /// AI 配置模型
    /// </summary>
    public class AiConfig
    {
        private const string ConfigFileName = "ai_config.json";

        /// <summary>
        /// API 地址
        /// </summary>
        [JsonPropertyName("api_endpoint")]
        public string ApiEndpoint { get; set; } = "https://api.openai.com/v1";

        /// <summary>
        /// API 密钥
        /// </summary>
        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// 模型名称
        /// </summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = "gpt-4o-mini";

        /// <summary>
        /// 最大 Token 数
        /// </summary>
        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; } = 2048;

        /// <summary>
        /// 温度参数
        /// </summary>
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.7;

        /// <summary>
        /// 配置文件保存路径
        /// </summary>
        [JsonIgnore]
        public string ConfigPath { get; private set; } = string.Empty;

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        public static AiConfig Load(string dataDir)
        {
            string configPath = Path.Combine(dataDir, ConfigFileName);

            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<AiConfig>(json) ?? new AiConfig();
                    config.ConfigPath = configPath;
                    return config;
                }
                catch
                {
                    // 配置文件损坏，返回默认配置
                    var config = new AiConfig { ConfigPath = configPath };
                    return config;
                }
            }

            return new AiConfig { ConfigPath = configPath };
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        public void Save()
        {
            if (string.IsNullOrEmpty(ConfigPath))
                throw new InvalidOperationException("配置路径未设置");

            // 确保目录存在
            string? dir = Path.GetDirectoryName(ConfigPath);
            if (dir != null)
                Directory.CreateDirectory(dir);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigPath, json);
        }

        /// <summary>
        /// 检查配置是否有效
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(ApiEndpoint) &&
                   !string.IsNullOrWhiteSpace(ApiKey) &&
                   !string.IsNullOrWhiteSpace(Model);
        }
    }
}
