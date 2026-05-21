using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PasswordManager.Services
{
    /// <summary>
    /// 加密管理器
    /// </summary>
    public class CryptoManager
    {
        private const int SaltSize = 32;
        private const int KeySize = 32;
        private const int NonceSize = 12;
        private const int Pbkdf2Iterations = 100000;

        private readonly byte[] _key;

        public CryptoManager(string password, string salt)
        {
            _key = DeriveKey(password, salt);
        }

        /// <summary>
        /// 生成随机盐值
        /// </summary>
        public static string GenerateSalt()
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            return Convert.ToBase64String(salt);
        }

        /// <summary>
        /// 从密码和盐值派生密钥
        /// </summary>
        private static byte[] DeriveKey(string password, string salt)
        {
            byte[] saltBytes;
            try
            {
                saltBytes = Convert.FromBase64String(salt);
            }
            catch
            {
                // 如果解码失败，直接使用字符串作为盐值
                saltBytes = Encoding.UTF8.GetBytes(salt);
            }

            return Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256,
                KeySize);
        }

        /// <summary>
        /// 加密文本
        /// </summary>
        public string Encrypt(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
                return string.Empty;

            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = _key;
                    aes.GenerateIV();

                    byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                    
                    using (var encryptor = aes.CreateEncryptor())
                    using (var msEncrypt = new MemoryStream())
                    {
                        // 写入IV到流的开头
                        msEncrypt.Write(aes.IV, 0, aes.IV.Length);
                        
                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            csEncrypt.Write(plaintextBytes, 0, plaintextBytes.Length);
                            csEncrypt.FlushFinalBlock();
                        }
                        
                        return Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"加密失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 解密文本
        /// </summary>
        public string Decrypt(string ciphertext)
        {
            if (string.IsNullOrEmpty(ciphertext))
                return string.Empty;

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(ciphertext);
                
                using (var aes = Aes.Create())
                {
                    aes.Key = _key;
                    
                    // 从密文中提取IV
                    byte[] iv = new byte[aes.IV.Length];
                    Array.Copy(cipherBytes, 0, iv, 0, iv.Length);
                    aes.IV = iv;
                    
                    // 获取实际的密文数据
                    byte[] actualCiphertext = new byte[cipherBytes.Length - iv.Length];
                    Array.Copy(cipherBytes, iv.Length, actualCiphertext, 0, actualCiphertext.Length);
                    
                    using (var decryptor = aes.CreateDecryptor())
                    using (var msDecrypt = new MemoryStream(actualCiphertext))
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (var srDecrypt = new StreamReader(csDecrypt, Encoding.UTF8))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"解密失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 验证密码是否正确
        /// </summary>
        public static bool VerifyPassword(string password, string salt, string encryptedData)
        {
            try
            {
                var cryptoMgr = new CryptoManager(password, salt);
                cryptoMgr.Decrypt(encryptedData);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 生成强密码
        /// </summary>
        public static string GeneratePassword(int length, bool includeSymbols = true)
        {
            if (length < 4)
                throw new ArgumentException("密码长度至少为4位");

            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digits = "0123456789";
            const string symbols = "!@#$%^&*()_+-=[]{}|;:,.<>?";

            string charset = lowercase + uppercase + digits;
            if (includeSymbols)
                charset += symbols;

            var password = new StringBuilder(length);
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] randomBytes = new byte[4];
                for (int i = 0; i < length; i++)
                {
                    rng.GetBytes(randomBytes);
                    uint randomValue = BitConverter.ToUInt32(randomBytes, 0);
                    int index = (int)(randomValue % charset.Length);
                    password.Append(charset[index]);
                }
            }

            return password.ToString();
        }

        /// <summary>
        /// 检查密码强度
        /// </summary>
        public static (string strength, int score) CheckPasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password))
                return ("无", 0);

            int score = 0;

            // 长度检查
            if (password.Length >= 8) score += 1;
            if (password.Length >= 12) score += 1;
            if (password.Length >= 16) score += 1;

            // 字符类型检查
            bool hasLower = false, hasUpper = false, hasDigit = false, hasSymbol = false;

            foreach (char c in password)
            {
                if (char.IsLower(c)) hasLower = true;
                else if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsDigit(c)) hasDigit = true;
                else hasSymbol = true;
            }

            if (hasLower) score += 1;
            if (hasUpper) score += 1;
            if (hasDigit) score += 1;
            if (hasSymbol) score += 1;

            // 评定强度
            string strength = score switch
            {
                <= 2 => "弱",
                <= 4 => "中等",
                <= 6 => "强",
                _ => "非常强"
            };

            return (strength, score);
        }
    }
}
