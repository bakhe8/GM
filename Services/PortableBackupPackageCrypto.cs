using System;
using System.Security.Cryptography;
using System.Text;

namespace GuaranteeManager.Services
{
    internal sealed class PortableKeyEnvelope
    {
        public int FormatVersion { get; set; } = 1;
        public int Iterations { get; set; } = 150000;
        public string SaltBase64 { get; set; } = string.Empty;
        public string NonceBase64 { get; set; } = string.Empty;
        public string CipherTextBase64 { get; set; } = string.Empty;
        public string TagBase64 { get; set; } = string.Empty;
    }

    internal static class PortableBackupPackageCrypto
    {
        private const int MinimumPassphraseLength = 8;
        private const int MinimumNewPackagePassphraseLength = 12;

        public static PortableKeyEnvelope ProtectText(string plainText, string passphrase)
        {
            ValidatePassphrase(passphrase);

            if (string.IsNullOrWhiteSpace(plainText))
            {
                throw new ArgumentException("النص المطلوب حمايته غير صالح.", nameof(plainText));
            }

            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherBytes = new byte[plainBytes.Length];
            byte[] tag = new byte[16];
            byte[] key = DeriveKey(passphrase, salt, 150000);

            using var aes = new AesGcm(key, tagSizeInBytes: 16);
            aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

            return new PortableKeyEnvelope
            {
                SaltBase64 = Convert.ToBase64String(salt),
                NonceBase64 = Convert.ToBase64String(nonce),
                CipherTextBase64 = Convert.ToBase64String(cipherBytes),
                TagBase64 = Convert.ToBase64String(tag)
            };
        }

        public static string UnprotectText(PortableKeyEnvelope envelope, string passphrase)
        {
            ValidatePassphrase(passphrase);

            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            byte[] salt = Convert.FromBase64String(envelope.SaltBase64);
            byte[] nonce = Convert.FromBase64String(envelope.NonceBase64);
            byte[] cipherBytes = Convert.FromBase64String(envelope.CipherTextBase64);
            byte[] tag = Convert.FromBase64String(envelope.TagBase64);
            byte[] plainBytes = new byte[cipherBytes.Length];
            byte[] key = DeriveKey(passphrase, salt, envelope.Iterations);

            try
            {
                using var aes = new AesGcm(key, tagSizeInBytes: 16);
                aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException("عبارة المرور غير صحيحة أو أن الحزمة المحمولة تالفة.", ex);
            }
        }

        public static void ValidatePassphrase(string passphrase)
        {
            if (string.IsNullOrWhiteSpace(passphrase) || passphrase.Trim().Length < MinimumPassphraseLength)
            {
                throw new InvalidOperationException($"عبارة المرور يجب أن تحتوي على {MinimumPassphraseLength} أحرف على الأقل.");
            }
        }

        public static void ValidatePassphraseForNewPackage(string passphrase)
        {
            ValidatePassphrase(passphrase);

            string trimmed = passphrase.Trim();
            bool hasLetter = trimmed.Any(char.IsLetter);
            bool hasDigit = trimmed.Any(char.IsDigit);
            bool hasSymbol = trimmed.Any(ch => !char.IsLetterOrDigit(ch) && !char.IsWhiteSpace(ch));

            if (trimmed.Length < MinimumNewPackagePassphraseLength || !hasLetter || !hasDigit || !hasSymbol)
            {
                throw new InvalidOperationException(
                    "عبارة المرور للحزم الجديدة يجب أن تحتوي على 12 حرفًا على الأقل، وتشمل حرفًا واحدًا ورقمًا واحدًا ورمزًا خاصًا واحدًا على الأقل.");
            }
        }

        private static byte[] DeriveKey(string passphrase, byte[] salt, int iterations)
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(passphrase),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                32);
        }
    }
}
