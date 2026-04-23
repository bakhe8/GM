using System;
using System.Runtime.Versioning;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    [SupportedOSPlatform("windows")]
    internal class DatabaseRuntimeInitializer
    {
        private readonly string _connectionString;
        private readonly AttachmentStorageService _attachmentStorage;
        private readonly WorkflowResponseStorageService _workflowResponseStorage;
        private readonly DatabaseEncryptionMigrator _encryptionMigrator;

        public DatabaseRuntimeInitializer(
            string connectionString,
            AttachmentStorageService attachmentStorage,
            WorkflowResponseStorageService workflowResponseStorage)
        {
            _connectionString = connectionString;
            _attachmentStorage = attachmentStorage;
            _workflowResponseStorage = workflowResponseStorage;
            _encryptionMigrator = new DatabaseEncryptionMigrator(connectionString);
        }

        public void Initialize()
        {
            using var scope = SimpleLogger.BeginScope("DatabaseRuntime.Initialize");
            try
            {
                AppPaths.EnsureDirectoriesExist();
                _encryptionMigrator.EnsureEncryptedIfNeeded();

                using var connection = SqliteConnectionFactory.Open(_connectionString);

                GuaranteeSchemaManager.EnsureAttachmentsSchema(connection);
                GuaranteeSchemaManager.EnsureBaseSchema(connection);
                WorkflowSchemaManager.EnsureSchema(connection);
                GuaranteeSchemaManager.EnsureVersioningAndMetadataSchema(connection);
                GuaranteeSchemaManager.EnsureCurrentGuaranteeIntegrity(connection);
                WorkflowSchemaManager.NormalizeLegacyCreatedBy(connection);
                _attachmentStorage.RecoverStagedFiles(connection);
                _workflowResponseStorage.RecoverStagedFiles(connection);
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "DatabaseRuntime.Initialize",
                    "تعذر تهيئة قاعدة بيانات النظام للتشغيل.",
                    isCritical: true);
            }
        }
    }
}
