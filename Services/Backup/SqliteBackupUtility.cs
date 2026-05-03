using System;
using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;

namespace GuaranteeManager.Services
{
    internal static class SqliteBackupUtility
    {
        private const string QuickCheckCommandText = "PRAGMA quick_check";

        public static void CreateBackup(string sourceDatabasePath, string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(sourceDatabasePath))
            {
                throw new ArgumentException("مسار قاعدة البيانات المصدر غير صالح.", nameof(sourceDatabasePath));
            }

            if (!File.Exists(sourceDatabasePath))
            {
                throw new FileNotFoundException("ملف قاعدة البيانات المصدر غير موجود.", sourceDatabasePath);
            }

            using var sourceConnection = SqliteConnectionFactory.OpenForPath(sourceDatabasePath, SqliteOpenMode.ReadWrite);
            CreateBackup(sourceConnection, destinationPath);
        }

        public static void CreateBackup(SqliteConnection sourceConnection, string destinationPath)
        {
            if (sourceConnection is null)
            {
                throw new ArgumentNullException(nameof(sourceConnection));
            }

            if (sourceConnection.State != ConnectionState.Open)
            {
                throw new InvalidOperationException("اتصال قاعدة البيانات المصدر يجب أن يكون مفتوحًا قبل إنشاء النسخة الاحتياطية.");
            }

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                throw new ArgumentException("مسار النسخة الاحتياطية غير صالح.", nameof(destinationPath));
            }

            string fullDestinationPath = Path.GetFullPath(destinationPath);
            string? destinationDirectory = Path.GetDirectoryName(fullDestinationPath);
            string sourceDataSource = new SqliteConnectionStringBuilder(sourceConnection.ConnectionString).DataSource;
            string fullSourcePath = string.IsNullOrWhiteSpace(sourceDataSource)
                ? string.Empty
                : Path.GetFullPath(sourceDataSource);

            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                throw new InvalidOperationException("تعذر تحديد مجلد النسخة الاحتياطية.");
            }

            if (!string.IsNullOrWhiteSpace(fullSourcePath) &&
                string.Equals(fullSourcePath, fullDestinationPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("لا يمكن إنشاء نسخة احتياطية فوق ملف قاعدة البيانات الحالي.");
            }

            Directory.CreateDirectory(destinationDirectory);
            string tempDestinationPath = Path.Combine(
                destinationDirectory,
                $"{Path.GetFileName(fullDestinationPath)}.{Guid.NewGuid():N}.tmp");
            long expectedSchemaObjectCount = GetSchemaObjectCount(sourceConnection);

            if (File.Exists(tempDestinationPath))
            {
                File.Delete(tempDestinationPath);
            }

            try
            {
                ExportEncryptedBackup(sourceConnection, tempDestinationPath);

                VerifyEncryptedBackup(tempDestinationPath, expectedSchemaObjectCount);

                SqliteConnection.ClearAllPools();
                if (File.Exists(fullDestinationPath))
                {
                    File.Delete(fullDestinationPath);
                }

                File.Move(tempDestinationPath, fullDestinationPath, overwrite: false);
            }
            catch
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(tempDestinationPath))
                {
                    TryDeleteTemporaryBackup(tempDestinationPath);
                }

                throw;
            }
        }

        public static void RestoreBackup(string backupPath, string targetDatabasePath)
        {
            if (string.IsNullOrWhiteSpace(backupPath))
            {
                throw new ArgumentException("مسار النسخة الاحتياطية غير صالح.", nameof(backupPath));
            }

            if (!File.Exists(backupPath))
            {
                throw new FileNotFoundException("ملف النسخة الاحتياطية المطلوب غير موجود.", backupPath);
            }

            if (string.IsNullOrWhiteSpace(targetDatabasePath))
            {
                throw new ArgumentException("مسار قاعدة البيانات الهدف غير صالح.", nameof(targetDatabasePath));
            }

            string fullBackupPath = Path.GetFullPath(backupPath);
            string fullTargetPath = Path.GetFullPath(targetDatabasePath);

            if (string.Equals(fullBackupPath, fullTargetPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("لا يمكن استرجاع النسخة الاحتياطية فوق الملف نفسه مباشرة.");
            }

            string? targetDirectory = Path.GetDirectoryName(fullTargetPath);
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new InvalidOperationException("تعذر تحديد مجلد قاعدة البيانات الهدف.");
            }

            Directory.CreateDirectory(targetDirectory);
            long schemaObjectCount = VerifyEncryptedBackup(fullBackupPath);

            string tempRestorePath = Path.Combine(
                targetDirectory,
                $"{Path.GetFileName(fullTargetPath)}.{Guid.NewGuid():N}.restore.tmp");

            try
            {
                File.Copy(fullBackupPath, tempRestorePath, overwrite: true);
                VerifyEncryptedBackup(tempRestorePath, schemaObjectCount);

                SqliteConnection.ClearAllPools();

                if (File.Exists(fullTargetPath))
                {
                    File.Delete(fullTargetPath);
                }

                File.Move(tempRestorePath, fullTargetPath, overwrite: false);
            }
            catch
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(tempRestorePath))
                {
                    TryDeleteTemporaryBackup(tempRestorePath);
                }

                throw;
            }
        }

        private static void ExportEncryptedBackup(SqliteConnection sourceConnection, string tempDestinationPath)
        {
            string escapedTempPath = EscapeSqlLiteral(tempDestinationPath);
            string escapedKey = EscapeSqlLiteral(SqliteConnectionFactory.GetCurrentEncryptionKey());
            bool attached = false;

            try
            {
                var attachCommand = sourceConnection.CreateCommand();
                attachCommand.CommandText = $"ATTACH DATABASE '{escapedTempPath}' AS backup KEY '{escapedKey}'";
                attachCommand.ExecuteNonQuery();
                attached = true;

                var exportCommand = sourceConnection.CreateCommand();
                exportCommand.CommandText = "SELECT sqlcipher_export('backup')";
                exportCommand.ExecuteNonQuery();

                var detachCommand = sourceConnection.CreateCommand();
                detachCommand.CommandText = "DETACH DATABASE backup";
                detachCommand.ExecuteNonQuery();
                attached = false;
            }
            finally
            {
                if (attached)
                {
                    try
                    {
                        var detachCommand = sourceConnection.CreateCommand();
                        detachCommand.CommandText = "DETACH DATABASE backup";
                        detachCommand.ExecuteNonQuery();
                    }
                    catch
                    {
                        // Best effort only. The caller will surface the original failure.
                    }
                }
            }
        }

        private static long VerifyEncryptedBackup(string backupPath, long? expectedSchemaObjectCount = null)
        {
            using (var encryptedConnection = SqliteConnectionFactory.OpenForPath(
                backupPath,
                SqliteOpenMode.ReadWrite,
                pooling: false))
            {
                long actualSchemaObjectCount = GetSchemaObjectCount(encryptedConnection);
                EnsureBackupSchemaMatchesSource(encryptedConnection, expectedSchemaObjectCount ?? 1, actualSchemaObjectCount);
                EnsureQuickCheckPasses(encryptedConnection);
                if (actualSchemaObjectCount <= 0)
                {
                    throw new InvalidOperationException("النسخة الاحتياطية لا تحتوي على أي كائنات schema قابلة للاستخدام.");
                }

                if (CanOpenWithoutEncryption(backupPath))
                {
                    throw new InvalidOperationException("تم إنشاء ملف نسخة احتياطية قابل للقراءة دون التشفير المطلوب.");
                }

                return actualSchemaObjectCount;
            }
        }

        private static long GetSchemaObjectCount(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type IN ('table', 'index', 'view', 'trigger')";
            return Convert.ToInt64(command.ExecuteScalar());
        }

        private static void EnsureBackupSchemaMatchesSource(SqliteConnection connection, long expectedSchemaObjectCount, long? actualSchemaObjectCount = null)
        {
            long backupObjectCount = actualSchemaObjectCount ?? GetSchemaObjectCount(connection);

            if (backupObjectCount < expectedSchemaObjectCount)
            {
                throw new InvalidOperationException(
                    $"النسخة الاحتياطية غير مكتملة. عدد كائنات المصدر = {expectedSchemaObjectCount} بينما النسخة = {backupObjectCount}.");
            }
        }

        private static void EnsureQuickCheckPasses(SqliteConnection connection)
        {
            var quickCheckCommand = connection.CreateCommand();
            quickCheckCommand.CommandText = QuickCheckCommandText;
            string? quickCheckResult = quickCheckCommand.ExecuteScalar()?.ToString();

            if (!string.Equals(quickCheckResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"فشل التحقق من سلامة النسخة الاحتياطية. نتيجة quick_check: {quickCheckResult ?? "(null)"}");
            }
        }

        private static bool CanOpenWithoutEncryption(string backupPath)
        {
            try
            {
                using var unencryptedConnection = SqliteConnectionFactory.OpenForPath(
                    backupPath,
                    SqliteOpenMode.ReadWrite,
                    encrypted: false,
                    pooling: false);
                var command = unencryptedConnection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master";
                _ = command.ExecuteScalar();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string EscapeSqlLiteral(string value)
        {
            return value.Replace("'", "''");
        }

        private static void TryDeleteTemporaryBackup(string tempDestinationPath)
        {
            try
            {
                File.Delete(tempDestinationPath);
            }
            catch (Exception cleanupEx)
            {
                SimpleLogger.Log(
                    $"Warning: Cleanup failed for temporary backup {tempDestinationPath}: {cleanupEx.Message}",
                    "WARNING");
            }
        }
    }
}
