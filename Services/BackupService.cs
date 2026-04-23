using System;
using System.IO;
using System.Linq;
using System.Globalization;
using GuaranteeManager.Utils;
using Microsoft.Data.Sqlite;

namespace GuaranteeManager.Services
{
    public class BackupService
    {
        private const int MaxBackups = 7;
        private readonly string _connectionString;
        public string? LastManualBackupPath { get; private set; }
        public string? LastRestoreSourcePath { get; private set; }
        public string? LastPreRestoreSafetyBackupPath { get; private set; }
        public string? LastPortableBackupPackagePath { get; private set; }
        public string? LastPortableRestorePackagePath { get; private set; }
        public string? LastPortableRestoreSafetyPackagePath { get; private set; }

        public BackupService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void PerformAutoBackup()
        {
            try
            {
                if (!File.Exists(AppPaths.DatabasePath)) return;

                if (!Directory.Exists(AppPaths.BackupFolder))
                {
                    Directory.CreateDirectory(AppPaths.BackupFolder);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                string destFile = Path.Combine(AppPaths.BackupFolder, $"guarantees_backup_{timestamp}.db");
                using var sourceConn = SqliteConnectionFactory.Open(_connectionString);
                SqliteBackupUtility.CreateBackup(sourceConn, destFile);
                
                SimpleLogger.Log($"Automatic backup created and verified: {destFile}");
                RotateBackups();
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, "Automatic Backup");
            }
        }

        public void CreateManualBackup(string destinationPath)
        {
            LastManualBackupPath = null;

            if (!File.Exists(AppPaths.DatabasePath))
            {
                throw new FileNotFoundException("ملف قاعدة البيانات الحالي غير موجود.", AppPaths.DatabasePath);
            }

            using var sourceConn = SqliteConnectionFactory.Open(_connectionString);
            SqliteBackupUtility.CreateBackup(sourceConn, destinationPath);
            LastManualBackupPath = destinationPath;
            SimpleLogger.Log($"Manual backup created and verified successfully by user: {destinationPath}");
        }

        public void RestoreManualBackup(string sourceBackupPath)
        {
            LastRestoreSourcePath = null;
            LastPreRestoreSafetyBackupPath = null;

            if (string.IsNullOrWhiteSpace(sourceBackupPath))
            {
                throw new ArgumentException("مسار النسخة الاحتياطية المطلوب استرجاعها غير صالح.", nameof(sourceBackupPath));
            }

            string fullSourceBackupPath = Path.GetFullPath(sourceBackupPath);
            if (!File.Exists(fullSourceBackupPath))
            {
                throw new FileNotFoundException("ملف النسخة الاحتياطية المطلوب غير موجود.", fullSourceBackupPath);
            }

            AppPaths.EnsureDirectoriesExist();

            if (File.Exists(AppPaths.DatabasePath))
            {
                string safetyBackupPath = Path.Combine(
                    AppPaths.BackupFolder,
                    $"pre_restore_backup_{DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.db");

                using var sourceConnection = SqliteConnectionFactory.Open(_connectionString);
                SqliteBackupUtility.CreateBackup(sourceConnection, safetyBackupPath);
                LastPreRestoreSafetyBackupPath = safetyBackupPath;
            }

            SqliteBackupUtility.RestoreBackup(fullSourceBackupPath, AppPaths.DatabasePath);

            var initializer = new DatabaseRuntimeInitializer(
                $"Data Source={AppPaths.DatabasePath}",
                new AttachmentStorageService(),
                new WorkflowResponseStorageService());
            initializer.Initialize();

            LastRestoreSourcePath = fullSourceBackupPath;
            SimpleLogger.Log(
                $"Manual backup restored and verified successfully: source={fullSourceBackupPath}, safety={LastPreRestoreSafetyBackupPath ?? "(none)"}");
        }

        public void CreatePortableBackupPackage(string destinationPath, string passphrase)
        {
            CreatePortableBackupPackageCore(destinationPath, passphrase, trackAsLastUserPackage: true);
        }

        public void RestorePortableBackupPackage(string packagePath, string passphrase)
        {
            LastPortableRestorePackagePath = null;
            LastPortableRestoreSafetyPackagePath = null;

            using PortableBackupPackageExtraction extraction = PortableBackupPackageUtility.ExtractPackage(packagePath, passphrase);

            if (File.Exists(AppPaths.DatabasePath))
            {
                string safetyPackagePath = Path.Combine(
                    AppPaths.BackupFolder,
                    $"pre_portable_restore_{DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.gmpkg");
                CreatePortableBackupPackageCore(
                    safetyPackagePath,
                    passphrase,
                    trackAsLastUserPackage: false,
                    allowLegacyPassphrase: true);
                LastPortableRestoreSafetyPackagePath = safetyPackagePath;
            }

            SqliteConnection.ClearAllPools();
            ReplaceManagedDirectory(extraction.AttachmentsPath, AppPaths.AttachmentsFolder);
            ReplaceManagedDirectory(extraction.WorkflowPath, AppPaths.WorkflowFolder);
            EncryptionKeyProvider.SaveImportedKey(extraction.EncryptionKey);
            SqliteConnectionFactory.OverrideCurrentEncryptionKey(extraction.EncryptionKey);
            SqliteBackupUtility.RestoreBackup(extraction.DatabasePath, AppPaths.DatabasePath);

            var initializer = new DatabaseRuntimeInitializer(
                $"Data Source={AppPaths.DatabasePath}",
                new AttachmentStorageService(),
                new WorkflowResponseStorageService());
            initializer.Initialize();

            LastPortableRestorePackagePath = Path.GetFullPath(packagePath);
            SimpleLogger.Log(
                $"Portable backup package restored successfully: source={LastPortableRestorePackagePath}, safety={LastPortableRestoreSafetyPackagePath ?? "(none)"}");
        }

        private void RotateBackups()
        {
            try
            {
                var files = Directory.GetFiles(AppPaths.BackupFolder, "guarantees_backup_*.db")
                                     .OrderBy(f => Path.GetFileNameWithoutExtension(f))
                                     .ToList();

                if (files.Count > MaxBackups)
                {
                    int toDelete = files.Count - MaxBackups;
                    for (int i = 0; i < toDelete; i++)
                    {
                        File.Delete(files[i]);
                        SimpleLogger.Log($"Old backup rotated out: {files[i]}");
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, "Backup Rotation");
            }
        }

        private static void ReplaceManagedDirectory(string? extractedDirectoryPath, string targetDirectoryPath)
        {
            if (Directory.Exists(targetDirectoryPath))
            {
                Directory.Delete(targetDirectoryPath, recursive: true);
            }

            if (string.IsNullOrWhiteSpace(extractedDirectoryPath) || !Directory.Exists(extractedDirectoryPath))
            {
                Directory.CreateDirectory(targetDirectoryPath);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetDirectoryPath) ?? AppPaths.StorageRootDirectory);
            Directory.Move(extractedDirectoryPath, targetDirectoryPath);
        }

        private void CreatePortableBackupPackageCore(
            string destinationPath,
            string passphrase,
            bool trackAsLastUserPackage,
            bool allowLegacyPassphrase = false)
        {
            if (trackAsLastUserPackage)
            {
                LastPortableBackupPackagePath = null;
            }

            PortableBackupPackageUtility.CreatePackage(destinationPath, _connectionString, passphrase, allowLegacyPassphrase);

            if (trackAsLastUserPackage)
            {
                LastPortableBackupPackagePath = Path.GetFullPath(destinationPath);
            }

            SimpleLogger.Log($"Portable backup package created successfully: {Path.GetFullPath(destinationPath)}");
        }
    }
}
