using System;
using System.IO;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Xunit;

namespace GuaranteeManager.Tests
{
    [Collection("Database")]
    public sealed class AppPathsTests
    {
        private readonly TestEnvironmentFixture _fixture;

        public AppPathsTests(TestEnvironmentFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void StorageRootDirectory_DefaultsToLocalAppData_WhenNoOverridesExist()
        {
            string? originalBaseEnv = Environment.GetEnvironmentVariable("GUARANTEE_MANAGER_BASEDIR");
            string? originalDataEnv = Environment.GetEnvironmentVariable("GUARANTEE_MANAGER_DATAROOT");

            try
            {
                Environment.SetEnvironmentVariable("GUARANTEE_MANAGER_BASEDIR", null);
                Environment.SetEnvironmentVariable("GUARANTEE_MANAGER_DATAROOT", null);
                AppPaths.SetBaseDirectoryOverride(null);
                AppPaths.SetStorageRootOverride(null);

                string expected = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GuaranteeManager");

                Assert.Equal(expected, AppPaths.StorageRootDirectory);
                Assert.NotEqual(AppPaths.BaseDirectory, AppPaths.StorageRootDirectory);
            }
            finally
            {
                Environment.SetEnvironmentVariable("GUARANTEE_MANAGER_BASEDIR", originalBaseEnv);
                Environment.SetEnvironmentVariable("GUARANTEE_MANAGER_DATAROOT", originalDataEnv);
                AppPaths.SetBaseDirectoryOverride(_fixture.WorkspaceRoot);
                AppPaths.SetStorageRootOverride(_fixture.StorageRoot);
                DatabaseService.ResetRuntimeInitializationForTesting();
                SqliteConnectionFactory.ResetCachedKeyForTesting();
            }
        }

        [Fact]
        public void EnsureDirectoriesExist_CopiesLegacyManagedStorageIntoConfiguredStorageRoot()
        {
            string legacyRoot = Path.Combine(_fixture.WorkspaceRoot, "legacy-base");
            string storageRoot = Path.Combine(_fixture.WorkspaceRoot, "migrated-storage");
            string legacyDatabasePath = Path.Combine(legacyRoot, "Data", "guarantees.db");
            string legacyKeyPath = Path.Combine(legacyRoot, "Data", ".dbkey");
            string legacyLogPath = Path.Combine(legacyRoot, "Logs", "app.log");
            string migratedDatabasePath = Path.Combine(storageRoot, "Data", "guarantees.db");
            string migratedKeyPath = Path.Combine(storageRoot, "Data", ".dbkey");
            string migratedLogPath = Path.Combine(storageRoot, "Logs", "app.log");

            Directory.CreateDirectory(Path.GetDirectoryName(legacyDatabasePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(legacyLogPath)!);
            File.WriteAllText(legacyDatabasePath, "legacy-db");
            File.WriteAllText(legacyKeyPath, "legacy-key");
            File.WriteAllText(legacyLogPath, "legacy-log");

            try
            {
                AppPaths.SetBaseDirectoryOverride(legacyRoot);
                AppPaths.SetStorageRootOverride(storageRoot);

                AppPaths.EnsureDirectoriesExist();

                Assert.True(File.Exists(migratedDatabasePath));
                Assert.True(File.Exists(migratedKeyPath));
                Assert.True(File.Exists(migratedLogPath));
                Assert.Equal("legacy-db", File.ReadAllText(migratedDatabasePath));
                Assert.Equal("legacy-key", File.ReadAllText(migratedKeyPath));
                Assert.Equal("legacy-log", File.ReadAllText(migratedLogPath));
            }
            finally
            {
                AppPaths.SetBaseDirectoryOverride(_fixture.WorkspaceRoot);
                AppPaths.SetStorageRootOverride(_fixture.StorageRoot);
                DatabaseService.ResetRuntimeInitializationForTesting();
                SqliteConnectionFactory.ResetCachedKeyForTesting();
            }
        }
    }
}
