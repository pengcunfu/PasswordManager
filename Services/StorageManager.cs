using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using PasswordManager.Models;

namespace PasswordManager.Services
{
    /// <summary>
    /// 存储管理器
    /// </summary>
    public class StorageManager
    {
        private const string DefaultDataFile = "passwords.json";
        private const string BackupDir = "backups";

        private readonly string _dataPath;
        private readonly CryptoManager _cryptoManager;
        private Database _database;

        public StorageManager(string dataDir, CryptoManager cryptoManager)
        {
            // 确保数据目录存在
            Directory.CreateDirectory(dataDir);
            
            _dataPath = Path.Combine(dataDir, DefaultDataFile);
            _cryptoManager = cryptoManager;
        }

        /// <summary>
        /// 加载数据库
        /// </summary>
        public void LoadDatabase()
        {
            // 检查文件是否存在
            if (!File.Exists(_dataPath))
            {
                // 文件不存在，创建新数据库
                string salt = CryptoManager.GenerateSalt();
                _database = new Database(salt);
                SaveDatabase();
                return;
            }

            // 读取文件
            string jsonData = File.ReadAllText(_dataPath);
            
            // 解析JSON
            using var jsonDoc = JsonDocument.Parse(jsonData);
            var root = jsonDoc.RootElement;

            // 创建数据库对象
            _database = new Database();

            // 获取盐值（未加密）
            if (root.TryGetProperty("salt", out var saltElement))
            {
                _database.Salt = saltElement.GetString();
            }
            else
            {
                throw new InvalidOperationException("无法读取盐值");
            }

            // 获取版本（未加密）
            if (root.TryGetProperty("version", out var versionElement))
            {
                _database.Version = versionElement.GetString() ?? "1.0";
            }

            // 获取创建时间（未加密）
            if (root.TryGetProperty("created_at", out var createdAtElement))
            {
                if (DateTime.TryParse(createdAtElement.GetString(), out var createdAt))
                {
                    _database.CreatedAt = createdAt;
                }
            }

            // 解密条目数据
            if (root.TryGetProperty("entries", out var entriesElement) && entriesElement.ValueKind == JsonValueKind.Array)
            {
                _database.Entries = new List<PasswordEntry>();
                
                foreach (var entryElement in entriesElement.EnumerateArray())
                {
                    var entry = DecryptEntry(entryElement);
                    _database.Entries.Add(entry);
                }
            }
        }

        /// <summary>
        /// 保存数据库
        /// </summary>
        public void SaveDatabase()
        {
            if (_database == null)
                throw new InvalidOperationException("数据库未初始化");

            // 创建加密的数据结构
            var encryptedData = new Dictionary<string, object>
            {
                ["salt"] = _database.Salt,
                ["version"] = _database.Version,
                ["created_at"] = _database.CreatedAt.ToString("O"),
                ["entries"] = _database.Entries.Select(EncryptEntry).ToArray()
            };

            // 序列化为JSON
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            
            string jsonData = JsonSerializer.Serialize(encryptedData, options);

            // 写入文件
            File.WriteAllText(_dataPath, jsonData);
        }

        /// <summary>
        /// 添加密码条目
        /// </summary>
        public void AddEntry(PasswordEntry entry)
        {
            if (_database == null)
                throw new InvalidOperationException("数据库未初始化");

            _database.Entries.Add(entry);
            SaveDatabase();
        }

        /// <summary>
        /// 更新密码条目
        /// </summary>
        public void UpdateEntry(PasswordEntry entry)
        {
            if (_database == null)
                throw new InvalidOperationException("数据库未初始化");

            var existingEntry = _database.Entries.FirstOrDefault(e => e.Id == entry.Id);
            if (existingEntry == null)
                throw new InvalidOperationException($"未找到ID为 {entry.Id} 的条目");

            entry.UpdateModifiedTime();
            int index = _database.Entries.IndexOf(existingEntry);
            _database.Entries[index] = entry;
            SaveDatabase();
        }

        /// <summary>
        /// 删除密码条目
        /// </summary>
        public void DeleteEntry(string id)
        {
            if (_database == null)
                throw new InvalidOperationException("数据库未初始化");

            var entry = _database.Entries.FirstOrDefault(e => e.Id == id);
            if (entry == null)
                throw new InvalidOperationException($"未找到ID为 {id} 的条目");

            _database.Entries.Remove(entry);
            SaveDatabase();
        }

        /// <summary>
        /// 获取所有密码条目
        /// </summary>
        public List<PasswordEntry> GetAllEntries()
        {
            return _database?.Entries ?? new List<PasswordEntry>();
        }

        /// <summary>
        /// 搜索密码条目
        /// </summary>
        public List<PasswordEntry> SearchEntries(string keyword)
        {
            if (_database == null || string.IsNullOrWhiteSpace(keyword))
                return GetAllEntries();

            keyword = keyword.ToLowerInvariant();
            
            return _database.Entries.Where(entry =>
                (entry.Title?.ToLowerInvariant().Contains(keyword) ?? false) ||
                (entry.Username?.ToLowerInvariant().Contains(keyword) ?? false) ||
                (entry.URL?.ToLowerInvariant().Contains(keyword) ?? false) ||
                (entry.Category?.ToLowerInvariant().Contains(keyword) ?? false) ||
                (entry.Notes?.ToLowerInvariant().Contains(keyword) ?? false)
            ).ToList();
        }

        /// <summary>
        /// 创建备份
        /// </summary>
        public void CreateBackup()
        {
            if (_database == null)
                throw new InvalidOperationException("数据库未初始化");

            // 创建备份目录
            string backupDir = Path.Combine(Path.GetDirectoryName(_dataPath), BackupDir);
            Directory.CreateDirectory(backupDir);

            // 生成备份文件名
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupPath = Path.Combine(backupDir, $"passwords_backup_{timestamp}.json");

            // 复制当前数据文件
            File.Copy(_dataPath, backupPath);
        }

        /// <summary>
        /// 加密密码条目
        /// </summary>
        private Dictionary<string, object> EncryptEntry(PasswordEntry entry)
        {
            // 加密敏感字段
            string encryptedPassword = _cryptoManager.Encrypt(entry.Password);
            string encryptedNotes = _cryptoManager.Encrypt(entry.Notes);

            return new Dictionary<string, object>
            {
                ["id"] = entry.Id,
                ["title"] = entry.Title,
                ["username"] = entry.Username,
                ["password"] = encryptedPassword,
                ["url"] = entry.URL,
                ["notes"] = encryptedNotes,
                ["category"] = entry.Category,
                ["created_at"] = entry.CreatedAt.ToString("O"),
                ["updated_at"] = entry.UpdatedAt.ToString("O")
            };
        }

        /// <summary>
        /// 解密密码条目
        /// </summary>
        private PasswordEntry DecryptEntry(JsonElement entryElement)
        {
            var entry = new PasswordEntry();

            // 解析非加密字段
            if (entryElement.TryGetProperty("id", out var idElement))
                entry.Id = idElement.GetString();

            if (entryElement.TryGetProperty("title", out var titleElement))
                entry.Title = titleElement.GetString();

            if (entryElement.TryGetProperty("username", out var usernameElement))
                entry.Username = usernameElement.GetString();

            if (entryElement.TryGetProperty("url", out var urlElement))
                entry.URL = urlElement.GetString();

            if (entryElement.TryGetProperty("category", out var categoryElement))
                entry.Category = categoryElement.GetString();

            // 解析时间字段
            if (entryElement.TryGetProperty("created_at", out var createdAtElement))
            {
                if (DateTime.TryParse(createdAtElement.GetString(), out var createdAt))
                    entry.CreatedAt = createdAt;
            }

            if (entryElement.TryGetProperty("updated_at", out var updatedAtElement))
            {
                if (DateTime.TryParse(updatedAtElement.GetString(), out var updatedAt))
                    entry.UpdatedAt = updatedAt;
            }

            // 解密敏感字段
            if (entryElement.TryGetProperty("password", out var passwordElement))
            {
                string encryptedPassword = passwordElement.GetString();
                entry.Password = _cryptoManager.Decrypt(encryptedPassword);
            }

            if (entryElement.TryGetProperty("notes", out var notesElement))
            {
                string encryptedNotes = notesElement.GetString();
                entry.Notes = _cryptoManager.Decrypt(encryptedNotes);
            }

            return entry;
        }

        /// <summary>
        /// 获取用户数据目录（可执行文件所在目录下的data文件夹）
        /// </summary>
        public static string GetUserDataDirectory()
        {
            // 获取可执行文件所在目录
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string exeDir = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory;

            // 数据保存在可执行文件目录下的data子目录
            string dataDir = Path.Combine(exeDir, "data");

            // 确保目录存在
            Directory.CreateDirectory(dataDir);

            return dataDir;
        }
    }
}
