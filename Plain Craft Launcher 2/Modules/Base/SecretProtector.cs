using System;
using System.Security.Cryptography;
using System.Text;

namespace PCL
{
    /// <summary>
    /// 敏感設置（登錄令牌、帳號列表等）的加密存儲。
    /// 使用 Windows DPAPI（綁定當前 Windows 用戶），取代原先以固定密鑰進行的 DES 加密：
    /// 開源版的識別碼固定為 0000-0000-0000-0000，導致所有人的 DES 密鑰相同，加密形同虛設。
    /// </summary>
    public static class SecretProtector
    {
        private const string Prefix = "dpapi:";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PCL152.Settings.v1");

        /// <summary>
        /// 加密明文，返回可直接寫入註冊表或 ini 的字符串。
        /// </summary>
        public static string Protect(string PlainText)
        {
            byte[] Data = ProtectedData.Protect(Encoding.UTF8.GetBytes(PlainText ?? ""), Entropy, DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(Data);
        }

        /// <summary>
        /// 解密存儲的字符串。若為舊版 DES 格式，則用舊算法解密並通過 IsLegacy 告知調用方需要重新保存。
        /// </summary>
        public static string Unprotect(string Stored, Func<string, string> LegacyDecrypt, out bool IsLegacy)
        {
            if (Stored is not null && Stored.StartsWith(Prefix, StringComparison.Ordinal))
            {
                IsLegacy = false;
                byte[] Data = ProtectedData.Unprotect(Convert.FromBase64String(Stored.Substring(Prefix.Length)), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(Data);
            }
            IsLegacy = true;
            return LegacyDecrypt(Stored);
        }
    }
}
