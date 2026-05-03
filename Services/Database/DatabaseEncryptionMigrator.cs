using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    [SupportedOSPlatform("windows")]
    internal sealed class DatabaseEncryptionMigrator
    {
        private readonly string _connectionString;

        public DatabaseEncryptionMigrator(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void EnsureEncryptedIfNeeded()
        {
            using var scope = SimpleLogger.BeginScope("DatabaseRuntime.EnsureEncryptedStorage");
            string dbPath = AppPaths.DatabasePath;
            if (!File.Exists(dbPath))
            {
                return;
            }

            bool isUnencrypted;
            try
            {
                using (var testConn = SqliteConnectionFactory.Open(_connectionString, encrypted: false))
                {
                    var cmd = testConn.CreateCommand();
                    cmd.CommandText = "SELECT name FROM sqlite_master LIMIT 1";
                    cmd.ExecuteScalar();
                }

                isUnencrypted = true;
            }
            catch
            {
                isUnencrypted = false;
            }

            if (!isUnencrypted)
            {
                return;
            }

            SimpleLogger.Log("Detected unencrypted database. Migrating to encrypted format...", "WARNING");

            string tempPath = dbPath + ".encrypting.tmp";
            string backupPath = dbPath + ".pre-encryption.bak";

            try
            {
                using (var plainConn = SqliteConnectionFactory.Open(_connectionString, encrypted: false))
                {
                    string encKey = EncryptionKeyProvider.GetOrCreateKey();
                    var attachCmd = plainConn.CreateCommand();
                    attachCmd.CommandText = $"ATTACH DATABASE '{tempPath.Replace("'", "''")}' AS encrypted KEY '{encKey}'";
                    attachCmd.ExecuteNonQuery();

                    var exportCmd = plainConn.CreateCommand();
                    exportCmd.CommandText = "SELECT sqlcipher_export('encrypted')";
                    exportCmd.ExecuteNonQuery();

                    var detachCmd = plainConn.CreateCommand();
                    detachCmd.CommandText = "DETACH DATABASE encrypted";
                    detachCmd.ExecuteNonQuery();
                }

                SqliteConnection.ClearAllPools();
                File.Move(dbPath, backupPath, overwrite: true);
                File.Move(tempPath, dbPath, overwrite: true);

                SimpleLogger.Log($"Database encrypted successfully. Backup saved to: {backupPath}");
            }
            catch (Exception ex)
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                throw OperationFailure.LogAndWrap(
                    ex,
                    "DatabaseRuntime.EnsureEncryptedStorage",
                    "تعذر ترقية قاعدة البيانات إلى التخزين المشفر المطلوب.",
                    isCritical: true);
            }
        }
    }
}
