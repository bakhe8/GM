using System;
using System.Collections.Generic;
using System.IO;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GuaranteeManager.Tests
{
    [Collection("Database")]
    public sealed class DatabaseWorkflowSmokeTests
    {
        private readonly TestEnvironmentFixture _fixture;

        public DatabaseWorkflowSmokeTests(TestEnvironmentFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void SaveGuarantee_PersistsCurrentGuaranteeWithRoot()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            Guarantee input = _fixture.CreateGuarantee();

            database.SaveGuarantee(input, new List<string>());

            Guarantee? persisted = database.GetCurrentGuaranteeByNo(input.GuaranteeNo);

            Assert.NotNull(persisted);
            Assert.Equal(persisted!.Id, persisted.RootId);
            Assert.True(persisted.IsCurrent);
            Assert.Equal(1, persisted.VersionNumber);
            Assert.Equal(input.Supplier, persisted.Supplier);
            Assert.Equal(input.ReferenceNumber, persisted.ReferenceNumber);
        }

        [Fact]
        public void UpdateGuarantee_CreatesNewCurrentVersionAndKeepsHistory()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string sourceAttachmentPath = _fixture.CreateSourceFile(contents: "inherited-attachment");
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string> { sourceAttachmentPath });
            Guarantee original = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            original.Amount += 250m;
            original.ExpiryDate = original.ExpiryDate.AddDays(30);
            original.Notes = "updated-version";

            int newVersionId = database.UpdateGuarantee(
                original,
                new List<string>(),
                new List<AttachmentRecord>());

            Guarantee? current = database.GetCurrentGuaranteeByRootId(original.RootId ?? original.Id);
            List<Guarantee> history = database.GetGuaranteeHistory(newVersionId);

            Assert.NotNull(current);
            Assert.Equal(newVersionId, current!.Id);
            Assert.True(current.IsCurrent);
            Assert.Equal(2, current.VersionNumber);
            Assert.Single(current.Attachments);
            Assert.Equal(2, history.Count);
            Assert.Contains(history, item => item.Id == original.Id && !item.IsCurrent);
            Assert.Contains(history, item => item.Id == newVersionId && item.IsCurrent);
        }

        [Fact]
        public void SaveGuarantee_WithAttachment_PersistsMetadataAndMovesFile()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string sourceAttachmentPath = _fixture.CreateSourceFile(contents: "attachment-body");
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string> { sourceAttachmentPath });

            Guarantee persisted = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            AttachmentRecord attachment = Assert.Single(persisted.Attachments);

            Assert.Equal(".txt", attachment.FileExtension);
            Assert.True(File.Exists(attachment.FilePath));
            Assert.NotEqual(sourceAttachmentPath, attachment.FilePath);
            Assert.Equal("attachment-body", File.ReadAllText(attachment.FilePath));
        }

        [Fact]
        public void CreateWorkflowRequests_AssignsSequentialNumbersAndCreatesLetters()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            WorkflowRequest extensionRequest = workflow.CreateExtensionRequest(
                current.Id,
                current.ExpiryDate.AddDays(15),
                "extend",
                "tester");
            WorkflowRequest verificationRequest = workflow.CreateVerificationRequest(
                current.Id,
                "verify",
                "tester");

            List<WorkflowRequest> requests = database.GetWorkflowRequestsByRootId(current.RootId ?? current.Id);

            Assert.Equal(1, extensionRequest.SequenceNumber);
            Assert.Equal(2, verificationRequest.SequenceNumber);
            Assert.True(File.Exists(extensionRequest.LetterFilePath));
            Assert.True(File.Exists(verificationRequest.LetterFilePath));
            Assert.Equal(2, requests.Count);
            Assert.Contains(requests, item => item.Id == extensionRequest.Id && item.SequenceNumber == 1);
            Assert.Contains(requests, item => item.Id == verificationRequest.Id && item.SequenceNumber == 2);
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
        public void CountAttachments_CountsOnlyAttachmentsLinkedToCurrentVersion()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string inheritedAttachmentPath = _fixture.CreateSourceFile(contents: "attachment-v1");
            string newAttachmentPath = _fixture.CreateSourceFile(contents: "attachment-v2");
            Guarantee seed = _fixture.CreateGuarantee();
            int baselineAttachmentCount = database.CountAttachments();

            database.SaveGuarantee(seed, new List<string> { inheritedAttachmentPath });
            Guarantee original = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            original.Notes = "second-version";

            database.UpdateGuarantee(
                original,
                new List<string> { newAttachmentPath },
                new List<AttachmentRecord>());

            Guarantee current = database.GetCurrentGuaranteeByRootId(original.RootId ?? original.Id)!;

            Assert.Equal(baselineAttachmentCount + 2, database.CountAttachments());
            Assert.Equal(2, current.Attachments.Count);
            Assert.All(current.Attachments, attachment => Assert.True(File.Exists(attachment.FilePath)));
        }

        [Fact]
        public void QueryWorkflowRequests_PendingOrMissingResponseOnly_ReturnsPendingAndExecutedWithoutResponse()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            Guarantee pendingGuarantee = _fixture.CreateGuarantee();
            Guarantee executedGuarantee = _fixture.CreateGuarantee();

            database.SaveGuarantee(pendingGuarantee, new List<string>());
            database.SaveGuarantee(executedGuarantee, new List<string>());

            Guarantee currentPending = database.GetCurrentGuaranteeByNo(pendingGuarantee.GuaranteeNo)!;
            Guarantee currentExecuted = database.GetCurrentGuaranteeByNo(executedGuarantee.GuaranteeNo)!;

            WorkflowRequest pendingVerification = workflow.CreateVerificationRequest(currentPending.Id, "pending", "tester");
            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(currentExecuted.Id, "release", "tester");
            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "approved-without-file");

            List<WorkflowRequestListItem> followUpRequests = database.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                PendingOrMissingResponseOnly = true,
                SortMode = WorkflowRequestQuerySortMode.RequestDateAscending
            });

            List<WorkflowRequestListItem> executedWithoutResponse = database.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RequestStatus = RequestStatus.Executed,
                PendingOrMissingResponseOnly = true,
                SortMode = WorkflowRequestQuerySortMode.ActivityDateDescending
            });

            Assert.Contains(followUpRequests, item => item.Request.Id == pendingVerification.Id && item.Request.Status == RequestStatus.Pending);
            Assert.Contains(followUpRequests, item => item.Request.Id == releaseRequest.Id && item.Request.Status == RequestStatus.Executed && !item.Request.HasResponseDocument);
            Assert.Single(executedWithoutResponse);
            Assert.Equal(releaseRequest.Id, executedWithoutResponse[0].Request.Id);
            Assert.False(executedWithoutResponse[0].Request.HasResponseDocument);
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
        public void QueryWorkflowRequests_RootGuaranteeId_ReturnsOnlyMatchingSeries()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            Guarantee firstGuaranteeSeed = _fixture.CreateGuarantee();
            Guarantee secondGuaranteeSeed = _fixture.CreateGuarantee();

            database.SaveGuarantee(firstGuaranteeSeed, new List<string>());
            database.SaveGuarantee(secondGuaranteeSeed, new List<string>());

            Guarantee firstGuarantee = database.GetCurrentGuaranteeByNo(firstGuaranteeSeed.GuaranteeNo)!;
            Guarantee secondGuarantee = database.GetCurrentGuaranteeByNo(secondGuaranteeSeed.GuaranteeNo)!;

            WorkflowRequest firstRequest = workflow.CreateVerificationRequest(firstGuarantee.Id, "first", "tester");
            WorkflowRequest secondRequest = workflow.CreateVerificationRequest(secondGuarantee.Id, "second", "tester");

            List<WorkflowRequestListItem> filteredRequests = database.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RootGuaranteeId = firstGuarantee.RootId ?? firstGuarantee.Id,
                SortMode = WorkflowRequestQuerySortMode.RequestDateDescending
            });

            Assert.Single(filteredRequests);
            Assert.Equal(firstRequest.Id, filteredRequests[0].Request.Id);
            Assert.DoesNotContain(filteredRequests, item => item.Request.Id == secondRequest.Id);
        }

        [Fact]
        public void SaveGuarantee_DuplicateNormalizedGuaranteeNo_ThrowsAndKeepsSingleCurrentRecord()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string canonicalNumber = $"  DUP-{_fixture.NextToken("NO")}  ";
            Guarantee original = _fixture.CreateGuarantee(canonicalNumber);
            Guarantee duplicate = _fixture.CreateGuarantee(canonicalNumber.ToLowerInvariant());

            database.SaveGuarantee(original, new List<string>());

            Assert.ThrowsAny<Exception>(() => database.SaveGuarantee(duplicate, new List<string>()));
            Assert.NotNull(database.GetCurrentGuaranteeByNo(canonicalNumber));
            Assert.Equal(1, database.CountGuarantees(new GuaranteeQueryOptions
            {
                SearchText = canonicalNumber.Trim()
            }));
        }

        [Fact]
        public void RecordBankResponse_ExecutedRelease_CreatesReleasedCurrentVersion()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(current.Id, "release", "tester");
            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "released");

            Guarantee releasedGuarantee = database.GetCurrentGuaranteeByRootId(current.RootId ?? current.Id)!;
            WorkflowRequest executedRequest = database.GetWorkflowRequestById(releaseRequest.Id)!;

            Assert.Equal(GuaranteeLifecycleStatus.Released, releasedGuarantee.LifecycleStatus);
            Assert.Equal(2, releasedGuarantee.VersionNumber);
            Assert.Equal(RequestStatus.Executed, executedRequest.Status);
            Assert.NotNull(executedRequest.ResponseRecordedAt);
        }

        [Fact]
        public void CreateAnnulmentRequest_ForReleasedGuarantee_CreatesLetterBackedRequest()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(current.Id, "release", "tester");
            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "released");

            Guarantee releasedGuarantee = database.GetCurrentGuaranteeByRootId(current.RootId ?? current.Id)!;
            WorkflowRequest annulmentRequest = workflow.CreateAnnulmentRequest(releasedGuarantee.Id, "reverse", "tester");

            Assert.Equal(RequestType.Annulment, annulmentRequest.Type);
            Assert.True(annulmentRequest.HasLetter);
            Assert.True(File.Exists(annulmentRequest.LetterFilePath));
        }

        [Fact]
        public void AttachResponseDocumentToClosedRequest_PreservesExecutionAndAddsLateDocument()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();
            string responseDocumentPath = _fixture.CreateSourceFile(".pdf", "late-response");

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(current.Id, "release", "tester");
            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "approved-without-file");
            WorkflowRequest executedBeforeAttach = database.GetWorkflowRequestById(releaseRequest.Id)!;

            workflow.AttachResponseDocumentToClosedRequest(releaseRequest.Id, responseDocumentPath, "late doc");

            WorkflowRequest executedAfterAttach = database.GetWorkflowRequestById(releaseRequest.Id)!;

            Assert.Equal(RequestStatus.Executed, executedAfterAttach.Status);
            Assert.True(executedAfterAttach.HasResponseDocument);
            Assert.True(File.Exists(executedAfterAttach.ResponseFilePath));
            Assert.Equal(executedBeforeAttach.ResponseRecordedAt, executedAfterAttach.ResponseRecordedAt);
            Assert.Contains("approved-without-file", executedAfterAttach.ResponseNotes);
            Assert.Contains("late doc", executedAfterAttach.ResponseNotes);
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

        private static long GetSchemaObjectCount(SqliteConnection connection)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type IN ('table', 'index', 'view', 'trigger')";
            return Convert.ToInt64(command.ExecuteScalar());
        }

        private static void OpenWithoutEncryption(string databasePath)
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenForPath(
                databasePath,
                SqliteOpenMode.ReadWrite,
                encrypted: false,
                pooling: false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master";
            _ = command.ExecuteScalar();
        }
    }
}
