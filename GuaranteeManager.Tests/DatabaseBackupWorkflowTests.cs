using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GuaranteeManager.Tests
{
    [Collection("Database")]
    public sealed class DatabaseBackupWorkflowTests : DatabaseWorkflowTestBase
    {
        public DatabaseBackupWorkflowTests(TestEnvironmentFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void CreateManualBackup_CreatesEncryptedVerifiedCopy()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            Guarantee seed = _fixture.CreateGuarantee();
            BackupService backupService = _fixture.CreateBackupService();
            string backupPath = _fixture.CreateArtifactPath();

            database.SaveGuarantee(seed, new List<string>());
            backupService.CreateManualBackup(backupPath);

            Assert.Equal(backupPath, backupService.LastManualBackupPath);
            Assert.True(File.Exists(backupPath));

            using SqliteConnection sourceConnection = SqliteConnectionFactory.OpenForPath(
                AppPaths.DatabasePath,
                SqliteOpenMode.ReadWrite,
                pooling: false);
            using SqliteConnection backupConnection = SqliteConnectionFactory.OpenForPath(
                backupPath,
                SqliteOpenMode.ReadWrite,
                pooling: false);

            Assert.True(GetSchemaObjectCount(backupConnection) >= GetSchemaObjectCount(sourceConnection));
            Assert.ThrowsAny<Exception>(() => OpenWithoutEncryption(backupPath));
        }
        [Fact]
        public void RestoreManualBackup_RestoresPreviousDatabaseStateAndCreatesSafetyBackup()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            BackupService backupService = _fixture.CreateBackupService();
            Guarantee baselineGuarantee = _fixture.CreateGuarantee();
            string backupPath = _fixture.CreateArtifactPath();

            database.SaveGuarantee(baselineGuarantee, new List<string>());
            int baselineCount = database.CountGuarantees();
            backupService.CreateManualBackup(backupPath);

            Guarantee afterBackupGuarantee = _fixture.CreateGuarantee();
            database.SaveGuarantee(afterBackupGuarantee, new List<string>());
            Assert.Equal(baselineCount + 1, database.CountGuarantees());

            backupService.RestoreManualBackup(backupPath);

            Assert.Equal(backupPath, backupService.LastRestoreSourcePath);
            Assert.False(string.IsNullOrWhiteSpace(backupService.LastPreRestoreSafetyBackupPath));
            Assert.True(File.Exists(backupService.LastPreRestoreSafetyBackupPath!));
            Assert.Equal(baselineCount, database.CountGuarantees());
            Assert.NotNull(database.GetCurrentGuaranteeByNo(baselineGuarantee.GuaranteeNo));
            Assert.Null(database.GetCurrentGuaranteeByNo(afterBackupGuarantee.GuaranteeNo));
        }

        [Fact]
        public void CreateManualBackup_ToCurrentDatabasePath_IsRejected()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            BackupService backupService = _fixture.CreateBackupService();

            database.SaveGuarantee(_fixture.CreateGuarantee(), new List<string>());

            Assert.Throws<InvalidOperationException>(() => backupService.CreateManualBackup(AppPaths.DatabasePath));
            Assert.Null(backupService.LastManualBackupPath);
        }

        [Fact]
        public void RestoreManualBackup_WithCorruptSource_DoesNotReplaceCurrentDatabase()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            BackupService backupService = _fixture.CreateBackupService();
            Guarantee baselineGuarantee = _fixture.CreateGuarantee();
            string corruptBackupPath = _fixture.CreateArtifactPath();

            database.SaveGuarantee(baselineGuarantee, new List<string>());
            int baselineCount = database.CountGuarantees();
            File.WriteAllText(corruptBackupPath, "not a valid encrypted sqlite backup");

            Assert.ThrowsAny<Exception>(() => backupService.RestoreManualBackup(corruptBackupPath));

            Assert.Null(backupService.LastRestoreSourcePath);
            Assert.Equal(baselineCount, database.CountGuarantees());
            Assert.NotNull(database.GetCurrentGuaranteeByNo(baselineGuarantee.GuaranteeNo));
        }

        [Fact]
        public void CreateManualBackup_WhenDestinationFileIsLocked_LeavesExistingFileUntouched()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            BackupService backupService = _fixture.CreateBackupService();
            string backupPath = _fixture.CreateArtifactPath();
            string originalContents = "existing locked backup placeholder";

            database.SaveGuarantee(_fixture.CreateGuarantee(), new List<string>());
            File.WriteAllText(backupPath, originalContents);

            using (new FileStream(backupPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.ThrowsAny<IOException>(() => backupService.CreateManualBackup(backupPath));
            }

            Assert.Null(backupService.LastManualBackupPath);
            Assert.Equal(originalContents, File.ReadAllText(backupPath));
            Assert.Empty(Directory.GetFiles(
                Path.GetDirectoryName(backupPath)!,
                $"{Path.GetFileName(backupPath)}.*.tmp"));
        }

        [Fact]
        public void RestorePortableBackupPackage_WithMissingManifest_DoesNotReplaceCurrentDatabase()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            BackupService backupService = _fixture.CreateBackupService();
            Guarantee baselineGuarantee = _fixture.CreateGuarantee();
            string packagePath = _fixture.CreateArtifactPath(".gmpkg");

            database.SaveGuarantee(baselineGuarantee, new List<string>());
            int baselineCount = database.CountGuarantees();

            using (ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                ZipArchiveEntry entry = archive.CreateEntry("payload/readme.txt");
                using StreamWriter writer = new(entry.Open());
                writer.Write("not a valid GuaranteeManager portable package");
            }

            Assert.Throws<InvalidOperationException>(
                () => backupService.RestorePortableBackupPackage(packagePath, "StrongPass#2026"));

            Assert.Null(backupService.LastPortableRestorePackagePath);
            Assert.Null(backupService.LastPortableRestoreSafetyPackagePath);
            Assert.Equal(baselineCount, database.CountGuarantees());
            Assert.NotNull(database.GetCurrentGuaranteeByNo(baselineGuarantee.GuaranteeNo));
        }

        [Fact]
        public void PerformAutoBackup_WhenBackupFolderPathIsBlocked_RecordsFailure()
        {
            string originalStorageRoot = AppPaths.StorageRootDirectory;
            string blockedStorageRoot = _fixture.CreateStorageRoot($"backup-blocked-{_fixture.NextToken("ROOT")}");

            try
            {
                _fixture.SwitchStorageRoot(blockedStorageRoot);
                DatabaseService.InitializeRuntime();

                DatabaseService database = _fixture.CreateDatabaseService();
                database.SaveGuarantee(_fixture.CreateGuarantee(), new List<string>());

                if (Directory.Exists(AppPaths.BackupFolder))
                {
                    Directory.Delete(AppPaths.BackupFolder, recursive: true);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.BackupFolder)!);
                File.WriteAllText(AppPaths.BackupFolder, "this file intentionally blocks the backup folder path");

                BackupService backupService = _fixture.CreateBackupService();
                Assert.Throws<InvalidOperationException>(() => backupService.PerformAutoBackup());
                Assert.NotNull(backupService.LastAutoBackupError);
            }
            finally
            {
                if (File.Exists(AppPaths.BackupFolder))
                {
                    File.Delete(AppPaths.BackupFolder);
                }

                _fixture.SwitchStorageRoot(originalStorageRoot);
                DatabaseService.InitializeRuntime();
            }
        }

        [Fact]
        public void PortableBackupPackage_RestoresDatabaseAttachmentsAndWorkflowIntoNewStorageRoot()
        {
            string originalStorageRoot = AppPaths.StorageRootDirectory;
            const string passphrase = "PortablePass#2026";

            try
            {
                DatabaseService sourceDatabase = _fixture.CreateDatabaseService();
                WorkflowService sourceWorkflow = _fixture.CreateWorkflowService(sourceDatabase);
                BackupService sourceBackupService = _fixture.CreateBackupService();

                string attachmentPath = _fixture.CreateSourceFile(contents: "portable-attachment");
                string responseDocumentPath = _fixture.CreateSourceFile(".pdf", "portable-response");
                Guarantee seed = _fixture.CreateGuarantee();
                string packagePath = _fixture.CreateArtifactPath(".gmpkg");

                sourceDatabase.SaveGuarantee(seed, new List<string> { attachmentPath });
                Guarantee current = sourceDatabase.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
                WorkflowRequest verificationRequest = sourceWorkflow.CreateVerificationRequest(current.Id, "portable", "tester");
                sourceWorkflow.RecordBankResponse(
                    verificationRequest.Id,
                    RequestStatus.Executed,
                    "verified",
                    responseDocumentPath,
                    promoteResponseDocumentToOfficialAttachment: true);

                sourceBackupService.CreatePortableBackupPackage(packagePath, passphrase);
                Assert.True(File.Exists(packagePath));

                string restoredStorageRoot = _fixture.CreateStorageRoot("portable-restored-storage");
                _fixture.SwitchStorageRoot(restoredStorageRoot);

                BackupService restoreBackupService = _fixture.CreateBackupService();
                restoreBackupService.RestorePortableBackupPackage(packagePath, passphrase);

                DatabaseService restoredDatabase = _fixture.CreateDatabaseService();
                Guarantee restoredGuarantee = restoredDatabase.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
                List<WorkflowRequestListItem> restoredRequests = restoredDatabase.QueryWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RootGuaranteeId = restoredGuarantee.RootId ?? restoredGuarantee.Id
                });

                Assert.NotNull(restoredGuarantee);
                Assert.NotEmpty(restoredGuarantee.Attachments);
                Assert.All(restoredGuarantee.Attachments, attachment => Assert.True(File.Exists(attachment.FilePath)));
                Assert.Contains(restoredRequests, item => item.Request.Type == RequestType.Verification);
                Assert.Contains(restoredRequests, item => item.Request.HasLetter);
                Assert.Contains(restoredRequests, item => item.Request.HasResponseDocument && File.Exists(item.Request.ResponseFilePath));
                Assert.NotNull(restoreBackupService.LastPortableRestorePackagePath);
            }
            finally
            {
                _fixture.SwitchStorageRoot(originalStorageRoot);
                DatabaseService.InitializeRuntime();
            }
        }
    }
}
