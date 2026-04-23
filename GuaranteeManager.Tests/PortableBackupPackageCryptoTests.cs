using System;
using System.Collections.Generic;
using System.IO;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Xunit;

namespace GuaranteeManager.Tests
{
    [Collection("Database")]
    public sealed class PortableBackupPackageCryptoTests
    {
        private readonly TestEnvironmentFixture _fixture;

        public PortableBackupPackageCryptoTests(TestEnvironmentFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void CreatePortableBackupPackage_RejectsWeakPassphraseForNewPackages()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            BackupService backupService = _fixture.CreateBackupService();
            Guarantee guarantee = _fixture.CreateGuarantee();
            string packagePath = _fixture.CreateArtifactPath(".gmpkg");

            database.SaveGuarantee(guarantee, new List<string>());

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
                () => backupService.CreatePortableBackupPackage(packagePath, "legacy88"));

            Assert.Contains("12", ex.Message);
            Assert.False(File.Exists(packagePath));
        }

        [Fact]
        public void RestorePortableBackupPackage_AllowsLegacyPassphraseForExistingPackages()
        {
            string originalStorageRoot = AppPaths.StorageRootDirectory;
            const string legacyPassphrase = "legacy88";

            try
            {
                DatabaseService sourceDatabase = _fixture.CreateDatabaseService();
                WorkflowService sourceWorkflow = _fixture.CreateWorkflowService(sourceDatabase);

                string attachmentPath = _fixture.CreateSourceFile(contents: "legacy-attachment");
                string responseDocumentPath = _fixture.CreateSourceFile(".pdf", "legacy-response");
                Guarantee sourceGuarantee = _fixture.CreateGuarantee();
                string packagePath = _fixture.CreateArtifactPath(".gmpkg");

                sourceDatabase.SaveGuarantee(sourceGuarantee, new List<string> { attachmentPath });
                Guarantee current = sourceDatabase.GetCurrentGuaranteeByNo(sourceGuarantee.GuaranteeNo)!;
                WorkflowRequest verificationRequest = sourceWorkflow.CreateVerificationRequest(current.Id, "legacy", "tester");
                sourceWorkflow.RecordBankResponse(
                    verificationRequest.Id,
                    RequestStatus.Executed,
                    "verified",
                    responseDocumentPath,
                    promoteResponseDocumentToOfficialAttachment: true);

                PortableBackupPackageUtility.CreatePackage(
                    packagePath,
                    $"Data Source={AppPaths.DatabasePath}",
                    legacyPassphrase,
                    allowLegacyPassphrase: true);

                string restoredStorageRoot = _fixture.CreateStorageRoot("portable-restored-legacy");
                _fixture.SwitchStorageRoot(restoredStorageRoot);
                DatabaseService.InitializeRuntime();

                DatabaseService targetDatabase = _fixture.CreateDatabaseService();
                targetDatabase.SaveGuarantee(_fixture.CreateGuarantee(), new List<string>());

                BackupService restoreBackupService = _fixture.CreateBackupService();
                restoreBackupService.RestorePortableBackupPackage(packagePath, legacyPassphrase);

                DatabaseService restoredDatabase = _fixture.CreateDatabaseService();
                Guarantee restoredGuarantee = restoredDatabase.GetCurrentGuaranteeByNo(sourceGuarantee.GuaranteeNo)!;

                Assert.NotNull(restoredGuarantee);
                Assert.NotNull(restoreBackupService.LastPortableRestoreSafetyPackagePath);
                Assert.True(File.Exists(restoreBackupService.LastPortableRestoreSafetyPackagePath));
            }
            finally
            {
                _fixture.SwitchStorageRoot(originalStorageRoot);
                DatabaseService.InitializeRuntime();
            }
        }
    }
}
