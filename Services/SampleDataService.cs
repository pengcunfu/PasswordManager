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

            // 创建示例数据 - 展示多账号和自定义字段
            var sampleEntries = new[]
            {
                // GitHub 个人账号
                CreateEntryWithCustomFields(
                    "GitHub-个人",
                    "personal@example.com",
                    "MySecurePassword123!",
                    "https://github.com",
                    "个人开发者账号",
                    "开发工具",
                    new[]
                    {
                        new CustomField("邮箱", "personal@example.com"),
                        new CustomField("手机号", "13800138000"),
                        new CustomField("密保邮箱", "backup@example.com")
                    }
                ),
                // GitHub 工作账号
                CreateEntryWithCustomFields(
                    "GitHub-工作",
                    "work@company.com",
                    "WorkPass456@",
                    "https://github.com",
                    "公司工作账号",
                    "开发工具",
                    new[]
                    {
                        new CustomField("邮箱", "work@company.com"),
                        new CustomField("工号", "EMP2024001")
                    }
                ),
                // QQ 账号
                CreateEntryWithCustomFields(
                    "QQ-主号",
                    "100001",
                    "QQPass789#",
                    "https://qq.com",
                    "主要QQ账号",
                    "社交",
                    new[]
                    {
                        new CustomField("密保手机", "13800138000", true),
                        new CustomField("密保问题", "我的第一只宠物叫什么"),
                        new CustomField("备用邮箱", "backup@qq.com")
                    }
                ),
                // 微信
                CreateEntryWithCustomFields(
                    "微信",
                    "15800000000",
                    "WeChatSecure999$",
                    "",
                    "日常聊天和支付工具",
                    "社交",
                    new[]
                    {
                        new CustomField("手机号", "15800000000"),
                        new CustomField("微信号", "my_wechat_id"),
                        new CustomField("密保手机", "13800138000", true)
                    }
                ),
                // 淘宝
                CreateEntryWithCustomFields(
                    "淘宝",
                    "shopper2024",
                    "ShoppingPass789#",
                    "https://taobao.com",
                    "购物账号，绑定银行卡",
                    "购物",
                    new[]
                    {
                        new CustomField("邮箱", "shopper@example.com"),
                        new CustomField("手机号", "13900139000"),
                        new CustomField("收货地址", "北京市朝阳区xxx街道")
                    }
                ),
                // 支付宝
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

        /// <summary>
        /// 创建带自定义字段的密码条目
        /// </summary>
        private static PasswordEntry CreateEntryWithCustomFields(
            string title, string username, string password,
            string url, string notes, string category,
            CustomField[] customFields)
        {
            var entry = new PasswordEntry(title, username, password, url, notes, category);
            entry.CustomFields.AddRange(customFields);
            return entry;
        }
    }
}
