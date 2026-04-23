using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    [SupportedOSPlatform("windows")]
    internal static class SqliteConnectionFactory
    {
        private static string? _cachedKey;

        private static string GetEncryptionKey()
        {
            return _cachedKey ??= EncryptionKeyProvider.GetOrCreateKey();
        }

        internal static string GetCurrentEncryptionKey()
        {
            return GetEncryptionKey();
        }

        internal static void OverrideCurrentEncryptionKey(string encryptionKey)
        {
            if (string.IsNullOrWhiteSpace(encryptionKey))
            {
                throw new ArgumentException("قيمة مفتاح التشفير غير صالحة.", nameof(encryptionKey));
            }

            _cachedKey = encryptionKey.Trim();
        }

        internal static void ResetCachedKeyForTesting()
        {
            _cachedKey = null;
        }

        public static SqliteConnection Create(string connectionString, bool encrypted = true, bool pooling = true)
        {
            return Create(connectionString, encrypted, pooling, null);
        }

        internal static SqliteConnection Create(string connectionString, bool encrypted, bool pooling, string? encryptionKeyOverride)
        {
            var builder = new SqliteConnectionStringBuilder(connectionString)
            {
                ForeignKeys = true,
                Pooling = pooling
            };

            if (encrypted)
            {
                builder.Password = string.IsNullOrWhiteSpace(encryptionKeyOverride)
                    ? GetEncryptionKey()
                    : encryptionKeyOverride;
            }

            return new SqliteConnection(builder.ToString());
        }

        public static SqliteConnection CreateForPath(
            string databasePath,
            SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate,
            bool encrypted = true,
            bool pooling = true,
            string? encryptionKeyOverride = null)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("مسار قاعدة البيانات غير صالح.", nameof(databasePath));
            }

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = Path.GetFullPath(databasePath),
                Mode = mode,
                ForeignKeys = true,
                Pooling = pooling
            };

            if (encrypted)
            {
                builder.Password = string.IsNullOrWhiteSpace(encryptionKeyOverride)
                    ? GetEncryptionKey()
                    : encryptionKeyOverride;
            }

            return new SqliteConnection(builder.ToString());
        }

        public static SqliteConnection Open(string connectionString, bool encrypted = true, bool pooling = true)
        {
            var connection = Create(connectionString, encrypted, pooling, null);
            connection.Open();
            return connection;
        }

        public static SqliteConnection OpenForPath(
            string databasePath,
            SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate,
            bool encrypted = true,
            bool pooling = true,
            string? encryptionKeyOverride = null)
        {
            var connection = CreateForPath(databasePath, mode, encrypted, pooling, encryptionKeyOverride);
            connection.Open();
            return connection;
        }
    }
}
