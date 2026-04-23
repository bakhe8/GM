using System;
using System.IO;
using System.Runtime.Versioning;

namespace GuaranteeManager.Utils
{
    [SupportedOSPlatform("windows")]
    public static class AppPaths
    {
        private const string DefaultStorageFolderName = "GuaranteeManager";
        private static readonly string[] ManagedStorageFolderNames = { "Data", "Attachments", "Workflow", "Logs" };
        private static readonly Lock InitializationLock = new();
        private static string? _baseDirectoryOverride;
        private static string? _storageRootOverride;
        private static bool _storagePrepared;

        public static string BaseDirectory
        {
            get
            {
                string? candidate = _baseDirectoryOverride;
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    candidate = Environment.GetEnvironmentVariable("GUARANTEE_MANAGER_BASEDIR");
                }

                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return Path.GetFullPath(candidate);
                }

                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        public static string StorageRootDirectory
        {
            get
            {
                string? candidate = _storageRootOverride;
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    candidate = Environment.GetEnvironmentVariable("GUARANTEE_MANAGER_DATAROOT");
                }

                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return Path.GetFullPath(candidate);
                }

                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(localAppData))
                {
                    return BaseDirectory;
                }

                return Path.Combine(localAppData, DefaultStorageFolderName);
            }
        }

        public static string DataFolder => Path.Combine(StorageRootDirectory, "Data");
        public static string DatabasePath => Path.Combine(DataFolder, "guarantees.db");
        public static string BackupFolder => Path.Combine(DataFolder, "Backups");

        public static string AttachmentsFolder => Path.Combine(StorageRootDirectory, "Attachments");
        public static string AttachmentStagingFolder => Path.Combine(AttachmentsFolder, "_staging");
        public static string WorkflowFolder => Path.Combine(StorageRootDirectory, "Workflow");
        public static string WorkflowLettersFolder => Path.Combine(WorkflowFolder, "Letters");
        public static string WorkflowResponsesFolder => Path.Combine(WorkflowFolder, "Responses");
        public static string WorkflowResponseStagingFolder => Path.Combine(WorkflowResponsesFolder, "_staging");
        public static string LogsFolder => Path.Combine(StorageRootDirectory, "Logs");

        public static void SetBaseDirectoryOverride(string? baseDirectory)
        {
            _baseDirectoryOverride = string.IsNullOrWhiteSpace(baseDirectory)
                ? null
                : Path.GetFullPath(baseDirectory);
            _storagePrepared = false;
        }

        public static void SetStorageRootOverride(string? storageRootDirectory)
        {
            _storageRootOverride = string.IsNullOrWhiteSpace(storageRootDirectory)
                ? null
                : Path.GetFullPath(storageRootDirectory);
            _storagePrepared = false;
        }

        public static void EnsureDirectoriesExist()
        {
            PrepareStorageRoot();

            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory(BackupFolder);
            Directory.CreateDirectory(AttachmentsFolder);
            Directory.CreateDirectory(AttachmentStagingFolder);
            Directory.CreateDirectory(WorkflowFolder);
            Directory.CreateDirectory(WorkflowLettersFolder);
            Directory.CreateDirectory(WorkflowResponsesFolder);
            Directory.CreateDirectory(WorkflowResponseStagingFolder);
            Directory.CreateDirectory(LogsFolder);
        }

        private static void PrepareStorageRoot()
        {
            lock (InitializationLock)
            {
                if (_storagePrepared)
                {
                    return;
                }

                MigrateLegacyStorageIfNeeded();
                _storagePrepared = true;
            }
        }

        private static void MigrateLegacyStorageIfNeeded()
        {
            string legacyRoot = Path.GetFullPath(BaseDirectory);
            string storageRoot = Path.GetFullPath(StorageRootDirectory);

            if (string.Equals(legacyRoot, storageRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!HasManagedStorageContent(legacyRoot))
            {
                return;
            }

            foreach (string folderName in ManagedStorageFolderNames)
            {
                string sourceDirectory = Path.Combine(legacyRoot, folderName);
                if (!Directory.Exists(sourceDirectory))
                {
                    continue;
                }

                string destinationDirectory = Path.Combine(storageRoot, folderName);
                CopyDirectoryContents(sourceDirectory, destinationDirectory);
            }
        }

        private static bool HasManagedStorageContent(string rootDirectory)
        {
            foreach (string folderName in ManagedStorageFolderNames)
            {
                string managedDirectory = Path.Combine(rootDirectory, folderName);
                if (!Directory.Exists(managedDirectory))
                {
                    continue;
                }

                if (Directory.EnumerateFiles(managedDirectory, "*", SearchOption.AllDirectories).Any())
                {
                    return true;
                }
            }

            return false;
        }

        private static void CopyDirectoryContents(string sourceDirectory, string destinationDirectory)
        {
            foreach (string sourcePath in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDirectory, sourcePath);
                Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
            }

            Directory.CreateDirectory(destinationDirectory);

            foreach (string sourceFilePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDirectory, sourceFilePath);
                string destinationFilePath = Path.Combine(destinationDirectory, relativePath);
                string? destinationFileDirectory = Path.GetDirectoryName(destinationFilePath);

                if (!string.IsNullOrWhiteSpace(destinationFileDirectory))
                {
                    Directory.CreateDirectory(destinationFileDirectory);
                }

                if (!File.Exists(destinationFilePath))
                {
                    File.Copy(sourceFilePath, destinationFilePath);
                }
            }
        }
    }
}
