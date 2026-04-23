using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using GuaranteeManager.Services;

namespace GuaranteeManager.Utils
{
    /// <summary>
    /// يدير مفتاح تشفير قاعدة البيانات باستخدام Windows DPAPI.
    /// المفتاح مشفر بحساب المستخدم الحالي — لا يمكن فك تشفيره من حساب آخر.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class EncryptionKeyProvider
    {
        private static string KeyFilePath => Path.Combine(AppPaths.DataFolder, ".dbkey");

        public static string GetOrCreateKey()
        {
            AppPaths.EnsureDirectoriesExist();

            if (File.Exists(KeyFilePath))
            {
                return LoadKey();
            }

            return CreateAndSaveKey();
        }

        internal static void SaveImportedKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("قيمة مفتاح التشفير المستورد غير صالحة.", nameof(key));
            }

            AppPaths.EnsureDirectoriesExist();

            byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(KeyFilePath, encrypted);
            SimpleLogger.Log("Database encryption key imported for current storage root.");
        }

        private static string LoadKey()
        {
            byte[] encrypted = File.ReadAllBytes(KeyFilePath);
            byte[] decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }

        private static string CreateAndSaveKey()
        {
            // مفتاح عشوائي 32 بايت بصيغة hex
            byte[] keyBytes = RandomNumberGenerator.GetBytes(32);
            string key = Convert.ToHexString(keyBytes).ToLowerInvariant();

            byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(KeyFilePath, encrypted);

            SimpleLogger.Log("Database encryption key created and stored.");
            return key;
        }
    }
}
