using PasswordManager.Models;

namespace PasswordManager.Services
{
    /// <summary>
    /// 示例数据服务
    /// </summary>
    public static class SampleDataService
    {
        /// <summary>
        /// 添加示例数据
        /// </summary>
        public static void AddSampleData(StorageManager storageManager)
        {
            // 检查是否已有数据
            var entries = storageManager.GetAllEntries();
            if (entries.Count > 0)
                return; // 已有数据，不添加示例

            // 创建示例数据
            var sampleEntries = new[]
            {
                new PasswordEntry(
                    "GitHub",
                    "developer@example.com",
                    "MySecurePassword123!",
                    "https://github.com",
                    "开发者账号，用于代码管理",
                    "开发工具"
                ),
                new PasswordEntry(
                    "Gmail",
                    "user@gmail.com",
                    "EmailPass456@",
                    "https://gmail.com",
                    "主要邮箱账号",
                    "邮箱"
                ),
                new PasswordEntry(
                    "淘宝",
                    "shopper2024",
                    "ShoppingPass789#",
                    "https://taobao.com",
                    "购物账号，绑定银行卡",
                    "购物"
                ),
                new PasswordEntry(
                    "微信",
                    "15800000000",
                    "WeChatSecure999$",
                    "",
                    "日常聊天和支付工具",
                    "社交"
                ),
                new PasswordEntry(
                    "支付宝",
                    "alipay_user",
                    "PaymentSafe101%",
                    "https://alipay.com",
                    "支付和理财账号",
                    "金融"
                )
            };

            // 保存示例数据
            foreach (var entry in sampleEntries)
            {
                storageManager.AddEntry(entry);
            }
        }
    }
}
