using System;
using System.IO;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GuaranteeManager.Tests
{
    [Collection("Database")]
    public sealed class PendingFileOperationQueueTests : DatabaseWorkflowTestBase
    {
        public PendingFileOperationQueueTests(TestEnvironmentFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void RuntimeInitialization_CompletesPendingAttachmentPromotion()
        {
            AppPaths.EnsureDirectoriesExist();
            string savedFileName = $"{_fixture.NextToken("ATT")}.txt";
            string stagingPath = Path.Combine(AppPaths.AttachmentStagingFolder, savedFileName);
            string finalPath = Path.Combine(AppPaths.AttachmentsFolder, savedFileName);
            File.WriteAllText(stagingPath, "pending attachment");

            PendingFileOperationQueue.RecordAttachmentPromotionFailure(
                new StagedAttachmentFile("source.txt", savedFileName, ".txt", stagingPath, finalPath),
                new IOException("simulated attachment promotion failure"));

            Assert.Equal(1, CountPendingFileOperations(savedFileName));

            DatabaseService.ResetRuntimeInitializationForTesting();
            DatabaseService.InitializeRuntime();

            Assert.True(File.Exists(finalPath));
            Assert.Equal("pending attachment", File.ReadAllText(finalPath));
            Assert.False(File.Exists(stagingPath));
            Assert.Equal(0, CountPendingFileOperations(savedFileName));
        }

        [Fact]
        public void RuntimeInitialization_CompletesPendingWorkflowResponsePromotion()
        {
            AppPaths.EnsureDirectoriesExist();
            string savedFileName = $"{_fixture.NextToken("RESP")}.pdf";
            string stagingPath = Path.Combine(AppPaths.WorkflowResponseStagingFolder, savedFileName);
            string finalPath = Path.Combine(AppPaths.WorkflowResponsesFolder, savedFileName);
            File.WriteAllText(stagingPath, "pending workflow response");

            PendingFileOperationQueue.RecordWorkflowResponsePromotionFailure(
                new StagedWorkflowResponseDocument("response.pdf", savedFileName, stagingPath, finalPath),
                new IOException("simulated workflow response promotion failure"));

            Assert.Equal(1, CountPendingFileOperations(savedFileName));

            DatabaseService.ResetRuntimeInitializationForTesting();
            DatabaseService.InitializeRuntime();

            Assert.True(File.Exists(finalPath));
            Assert.Equal("pending workflow response", File.ReadAllText(finalPath));
            Assert.False(File.Exists(stagingPath));
            Assert.Equal(0, CountPendingFileOperations(savedFileName));
        }

        [Fact]
        public void RuntimeInitialization_ReplaysPendingCleanup()
        {
            AppPaths.EnsureDirectoriesExist();
            string savedFileName = $"{_fixture.NextToken("CLEAN")}.tmp";
            string finalPath = Path.Combine(AppPaths.AttachmentsFolder, savedFileName);
            File.WriteAllText(finalPath, "pending cleanup");

            PendingFileOperationQueue.RecordCleanupFailure(
                savedFileName,
                finalPath,
                new IOException("simulated cleanup failure"));

            Assert.Equal(1, CountPendingFileOperations(savedFileName));

            DatabaseService.ResetRuntimeInitializationForTesting();
            DatabaseService.InitializeRuntime();

            Assert.False(File.Exists(finalPath));
            Assert.Equal(0, CountPendingFileOperations(savedFileName));
        }

        private static int CountPendingFileOperations(string savedFileName)
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenForPath(AppPaths.DatabasePath);
            PendingFileOperationQueue.EnsureSchema(connection);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM PendingFileOperations WHERE SavedFileName = $savedFileName";
            command.Parameters.AddWithValue("$savedFileName", savedFileName);
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }
}
