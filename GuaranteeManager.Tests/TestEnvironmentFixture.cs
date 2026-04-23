using System;
using System.IO;
using System.Threading;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Tests
{
    public sealed class TestEnvironmentFixture : IDisposable
    {
        private int _sequence;

        public TestEnvironmentFixture()
        {
            WorkspaceRoot = Path.Combine(
                Path.GetTempPath(),
                "GuaranteeManager.Tests",
                Guid.NewGuid().ToString("N"));
            StorageRoot = Path.Combine(WorkspaceRoot, "storage");

            Directory.CreateDirectory(WorkspaceRoot);
            AppPaths.SetBaseDirectoryOverride(WorkspaceRoot);
            AppPaths.SetStorageRootOverride(StorageRoot);
            DatabaseService.InitializeRuntime();
        }

        public string WorkspaceRoot { get; }

        public string StorageRoot { get; }

        public DatabaseService CreateDatabaseService()
        {
            return new DatabaseService();
        }

        public WorkflowService CreateWorkflowService(IDatabaseService databaseService)
        {
            return new WorkflowService(databaseService);
        }

        public BackupService CreateBackupService()
        {
            return new BackupService($"Data Source={AppPaths.DatabasePath}");
        }

        public Guarantee CreateGuarantee(string? guaranteeNo = null)
        {
            string token = NextToken("G");
            return new Guarantee
            {
                Supplier = $"Supplier {token}",
                Bank = $"Bank {token}",
                GuaranteeNo = guaranteeNo ?? $"GN-{token}",
                Amount = 1000m + _sequence,
                ExpiryDate = DateTime.Today.AddDays(90),
                GuaranteeType = "Performance",
                Beneficiary = "General Beneficiary",
                ReferenceType = GuaranteeReferenceType.Contract,
                ReferenceNumber = $"REF-{token}",
                Notes = $"Seed data {token}"
            };
        }

        public string CreateSourceFile(string extension = ".txt", string? contents = null)
        {
            string sourceDirectory = Path.Combine(WorkspaceRoot, "source-files");
            Directory.CreateDirectory(sourceDirectory);

            string path = Path.Combine(sourceDirectory, $"{NextToken("FILE")}{extension}");
            File.WriteAllText(path, contents ?? $"seed-content:{Path.GetFileNameWithoutExtension(path)}");
            return path;
        }

        public string CreateArtifactPath(string extension = ".db")
        {
            string artifactDirectory = Path.Combine(WorkspaceRoot, "artifacts");
            Directory.CreateDirectory(artifactDirectory);
            return Path.Combine(artifactDirectory, $"{NextToken("ART")}{extension}");
        }

        public string CreateStorageRoot(string suffix)
        {
            string path = Path.Combine(WorkspaceRoot, suffix);
            Directory.CreateDirectory(path);
            return path;
        }

        public void SwitchStorageRoot(string storageRoot)
        {
            AppPaths.SetStorageRootOverride(storageRoot);
            DatabaseService.ResetRuntimeInitializationForTesting();
            SqliteConnectionFactory.ResetCachedKeyForTesting();
        }

        public string NextToken(string prefix)
        {
            int nextValue = Interlocked.Increment(ref _sequence);
            return $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{nextValue}";
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(WorkspaceRoot, recursive: true);
            }
            catch
            {
                // Best effort cleanup only.
            }
        }
    }
}
