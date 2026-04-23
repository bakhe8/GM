using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using GuaranteeManager.Utils;
using Microsoft.Data.Sqlite;

namespace GuaranteeManager.Services
{
    internal sealed class PortableBackupPackageManifest
    {
        public int FormatVersion { get; set; } = 1;
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("O");
        public string DatabaseEntryName { get; set; } = "database/guarantees.db";
        public string KeyEnvelopeEntryName { get; set; } = "security/key-envelope.json";
        public bool IncludesAttachments { get; set; }
        public bool IncludesWorkflow { get; set; }
        public int AttachmentFileCount { get; set; }
        public int WorkflowFileCount { get; set; }
    }

    internal sealed class PortableBackupPackageExtraction : IDisposable
    {
        public required string ExtractionRoot { get; init; }
        public required string DatabasePath { get; init; }
        public required string EncryptionKey { get; init; }
        public string? AttachmentsPath { get; init; }
        public string? WorkflowPath { get; init; }
        public required PortableBackupPackageManifest Manifest { get; init; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(ExtractionRoot))
                {
                    Directory.Delete(ExtractionRoot, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup only.
            }
        }
    }

    internal static class PortableBackupPackageUtility
    {
        private const string ManifestEntryName = "manifest.json";
        private const string DatabaseEntryName = "database/guarantees.db";
        private const string KeyEnvelopeEntryName = "security/key-envelope.json";
        private const string AttachmentsPrefix = "payload/Attachments/";
        private const string WorkflowPrefix = "payload/Workflow/";

        public static void CreatePackage(string destinationPath, string connectionString, string passphrase)
        {
            PortableBackupPackageCrypto.ValidatePassphrase(passphrase);

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                throw new ArgumentException("مسار الحزمة المحمولة غير صالح.", nameof(destinationPath));
            }

            AppPaths.EnsureDirectoriesExist();
            string fullDestinationPath = Path.GetFullPath(destinationPath);
            string? destinationDirectory = Path.GetDirectoryName(fullDestinationPath);
            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                throw new InvalidOperationException("تعذر تحديد مجلد الحزمة المحمولة.");
            }

            Directory.CreateDirectory(destinationDirectory);

            string tempRoot = Path.Combine(Path.GetTempPath(), "GuaranteeManager.PortablePackage", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            string tempDatabasePath = Path.Combine(tempRoot, "guarantees.db");

            try
            {
                using (SqliteConnection sourceConnection = SqliteConnectionFactory.Open(connectionString))
                {
                    SqliteBackupUtility.CreateBackup(sourceConnection, tempDatabasePath);
                }

                PortableKeyEnvelope keyEnvelope = PortableBackupPackageCrypto.ProtectText(
                    SqliteConnectionFactory.GetCurrentEncryptionKey(),
                    passphrase);

                PortableBackupPackageManifest manifest = new()
                {
                    DatabaseEntryName = DatabaseEntryName,
                    KeyEnvelopeEntryName = KeyEnvelopeEntryName,
                    IncludesAttachments = Directory.Exists(AppPaths.AttachmentsFolder),
                    IncludesWorkflow = Directory.Exists(AppPaths.WorkflowFolder),
                    AttachmentFileCount = CountFiles(AppPaths.AttachmentsFolder),
                    WorkflowFileCount = CountFiles(AppPaths.WorkflowFolder)
                };

                if (File.Exists(fullDestinationPath))
                {
                    File.Delete(fullDestinationPath);
                }

                using ZipArchive archive = ZipFile.Open(fullDestinationPath, ZipArchiveMode.Create);
                AddJsonEntry(archive, ManifestEntryName, manifest);
                AddJsonEntry(archive, KeyEnvelopeEntryName, keyEnvelope);
                archive.CreateEntryFromFile(tempDatabasePath, DatabaseEntryName, CompressionLevel.Optimal);
                AddDirectoryToArchive(archive, AppPaths.AttachmentsFolder, AttachmentsPrefix);
                AddDirectoryToArchive(archive, AppPaths.WorkflowFolder, WorkflowPrefix);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                    {
                        Directory.Delete(tempRoot, recursive: true);
                    }
                }
                catch
                {
                    // Best effort cleanup only.
                }
            }
        }

        public static PortableBackupPackageExtraction ExtractPackage(string packagePath, string passphrase)
        {
            PortableBackupPackageCrypto.ValidatePassphrase(passphrase);

            if (string.IsNullOrWhiteSpace(packagePath))
            {
                throw new ArgumentException("مسار الحزمة المحمولة غير صالح.", nameof(packagePath));
            }

            string fullPackagePath = Path.GetFullPath(packagePath);
            if (!File.Exists(fullPackagePath))
            {
                throw new FileNotFoundException("ملف الحزمة المحمولة المطلوب غير موجود.", fullPackagePath);
            }

            string extractionRoot = Path.Combine(Path.GetTempPath(), "GuaranteeManager.PortableRestore", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractionRoot);

            try
            {
                using ZipArchive archive = ZipFile.OpenRead(fullPackagePath);
                PortableBackupPackageManifest manifest = ReadJsonEntry<PortableBackupPackageManifest>(archive, ManifestEntryName);
                PortableKeyEnvelope envelope = ReadJsonEntry<PortableKeyEnvelope>(archive, manifest.KeyEnvelopeEntryName);
                string encryptionKey = PortableBackupPackageCrypto.UnprotectText(envelope, passphrase);

                string databasePath = ExtractFileEntry(archive, manifest.DatabaseEntryName, Path.Combine(extractionRoot, "database", "guarantees.db"));
                ExtractPrefixedEntries(archive, AttachmentsPrefix, Path.Combine(extractionRoot, "Attachments"));
                ExtractPrefixedEntries(archive, WorkflowPrefix, Path.Combine(extractionRoot, "Workflow"));

                VerifyPackageDatabase(databasePath, encryptionKey);

                return new PortableBackupPackageExtraction
                {
                    ExtractionRoot = extractionRoot,
                    DatabasePath = databasePath,
                    EncryptionKey = encryptionKey,
                    AttachmentsPath = Directory.Exists(Path.Combine(extractionRoot, "Attachments"))
                        ? Path.Combine(extractionRoot, "Attachments")
                        : null,
                    WorkflowPath = Directory.Exists(Path.Combine(extractionRoot, "Workflow"))
                        ? Path.Combine(extractionRoot, "Workflow")
                        : null,
                    Manifest = manifest
                };
            }
            catch
            {
                try
                {
                    if (Directory.Exists(extractionRoot))
                    {
                        Directory.Delete(extractionRoot, recursive: true);
                    }
                }
                catch
                {
                    // Best effort cleanup only.
                }

                throw;
            }
        }

        private static void VerifyPackageDatabase(string databasePath, string encryptionKey)
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenForPath(
                databasePath,
                SqliteOpenMode.ReadWrite,
                pooling: false,
                encryptionKeyOverride: encryptionKey);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check";
            string? result = command.ExecuteScalar()?.ToString();
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("تعذر التحقق من سلامة قاعدة البيانات داخل الحزمة المحمولة.");
            }
        }

        private static void AddDirectoryToArchive(ZipArchive archive, string sourceDirectory, string entryPrefix)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                return;
            }

            foreach (string filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDirectory, filePath)
                    .Replace('\\', '/');
                archive.CreateEntryFromFile(filePath, $"{entryPrefix}{relativePath}", CompressionLevel.Optimal);
            }
        }

        private static void AddJsonEntry<T>(ZipArchive archive, string entryName, T payload)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using Stream stream = entry.Open();
            JsonSerializer.Serialize(stream, payload, new JsonSerializerOptions { WriteIndented = true });
        }

        private static T ReadJsonEntry<T>(ZipArchive archive, string entryName)
        {
            ZipArchiveEntry entry = archive.GetEntry(entryName)
                ?? throw new InvalidOperationException($"الحزمة المحمولة ناقصة. المدخل المطلوب غير موجود: {entryName}");
            using Stream stream = entry.Open();
            return JsonSerializer.Deserialize<T>(stream)
                ?? throw new InvalidOperationException($"تعذر قراءة بيانات المدخل {entryName} من الحزمة المحمولة.");
        }

        private static string ExtractFileEntry(ZipArchive archive, string entryName, string destinationPath)
        {
            ZipArchiveEntry entry = archive.GetEntry(entryName)
                ?? throw new InvalidOperationException($"الحزمة المحمولة ناقصة. الملف المطلوب غير موجود: {entryName}");

            string fullDestinationPath = Path.GetFullPath(destinationPath);
            string? destinationDirectory = Path.GetDirectoryName(fullDestinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            entry.ExtractToFile(fullDestinationPath, overwrite: true);
            return fullDestinationPath;
        }

        private static void ExtractPrefixedEntries(ZipArchive archive, string entryPrefix, string destinationRoot)
        {
            foreach (ZipArchiveEntry entry in archive.Entries.Where(item => item.FullName.StartsWith(entryPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                string relativePath = entry.FullName.Substring(entryPrefix.Length).Replace('/', Path.DirectorySeparatorChar);
                string destinationPath = Path.Combine(destinationRoot, relativePath);
                string fullDestinationPath = Path.GetFullPath(destinationPath);
                string fullDestinationRoot = Path.GetFullPath(destinationRoot);

                if (!fullDestinationPath.StartsWith(fullDestinationRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("الحزمة المحمولة تحتوي على مسار غير صالح أو غير آمن.");
                }

                string? destinationDirectory = Path.GetDirectoryName(fullDestinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                entry.ExtractToFile(fullDestinationPath, overwrite: true);
            }
        }

        private static int CountFiles(string directoryPath)
        {
            return Directory.Exists(directoryPath)
                ? Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories).Count()
                : 0;
        }
    }
}
