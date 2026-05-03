using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    internal static class PendingFileOperationQueue
    {
        private const string AttachmentPromotion = "AttachmentPromotion";
        private const string WorkflowResponsePromotion = "WorkflowResponsePromotion";
        private const string FileCleanup = "FileCleanup";

        public static void EnsureSchema(SqliteConnection connection)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS PendingFileOperations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    OperationType TEXT NOT NULL,
                    SavedFileName TEXT NOT NULL,
                    StagingPath TEXT,
                    FinalPath TEXT NOT NULL,
                    Attempts INTEGER NOT NULL DEFAULT 0,
                    LastError TEXT,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_pending_file_operations_type
                    ON PendingFileOperations(OperationType);
                CREATE INDEX IF NOT EXISTS idx_pending_file_operations_saved
                    ON PendingFileOperations(SavedFileName);";
            command.ExecuteNonQuery();
        }

        public static void RecordAttachmentPromotionFailure(StagedAttachmentFile stagedFile, Exception error)
        {
            TryRecordPromotion(
                AttachmentPromotion,
                stagedFile.SavedFileName,
                stagedFile.StagingPath,
                stagedFile.FinalPath,
                error);
        }

        public static void RecordWorkflowResponsePromotionFailure(StagedWorkflowResponseDocument stagedDocument, Exception error)
        {
            TryRecordPromotion(
                WorkflowResponsePromotion,
                stagedDocument.SavedFileName,
                stagedDocument.StagingPath,
                stagedDocument.FinalPath,
                error);
        }

        public static void RecordCleanupFailure(string savedFileName, string finalPath, Exception error)
        {
            TryRecord(FileCleanup, savedFileName, null, finalPath, error);
        }

        public static void ProcessPendingOperations(SqliteConnection connection)
        {
            EnsureSchema(connection);
            foreach (PendingFileOperation operation in LoadPendingOperations(connection))
            {
                try
                {
                    Process(operation);
                    Delete(connection, operation.Id);
                }
                catch (Exception ex)
                {
                    RecordAttempt(connection, operation.Id, ex);
                    SimpleLogger.LogError(ex, $"PendingFileOperationQueue ({operation.OperationType}:{operation.SavedFileName})");
                }
            }
        }

        public static int CountPendingOperations(SqliteConnection connection)
        {
            EnsureSchema(connection);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM PendingFileOperations";
            return Convert.ToInt32(command.ExecuteScalar());
        }

        private static void TryRecordPromotion(
            string operationType,
            string savedFileName,
            string stagingPath,
            string finalPath,
            Exception error)
        {
            TryRecord(operationType, savedFileName, stagingPath, finalPath, error);
        }

        private static void TryRecord(
            string operationType,
            string savedFileName,
            string? stagingPath,
            string finalPath,
            Exception error)
        {
            try
            {
                if (!File.Exists(AppPaths.DatabasePath))
                {
                    return;
                }

                using SqliteConnection connection = SqliteConnectionFactory.OpenForPath(AppPaths.DatabasePath);
                EnsureSchema(connection);
                Upsert(connection, operationType, savedFileName, stagingPath, finalPath, error);
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, $"PendingFileOperationQueue.Record ({operationType}:{savedFileName})");
            }
        }

        private static void Upsert(
            SqliteConnection connection,
            string operationType,
            string savedFileName,
            string? stagingPath,
            string finalPath,
            Exception error)
        {
            string now = PersistedDateTime.FormatDateTime(DateTime.Now);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO PendingFileOperations (
                    OperationType,
                    SavedFileName,
                    StagingPath,
                    FinalPath,
                    Attempts,
                    LastError,
                    CreatedAt,
                    UpdatedAt)
                VALUES (
                    $operationType,
                    $savedFileName,
                    $stagingPath,
                    $finalPath,
                    1,
                    $lastError,
                    $now,
                    $now);";
            command.Parameters.AddWithValue("$operationType", operationType);
            command.Parameters.AddWithValue("$savedFileName", savedFileName);
            command.Parameters.AddWithValue("$stagingPath", (object?)stagingPath ?? DBNull.Value);
            command.Parameters.AddWithValue("$finalPath", finalPath);
            command.Parameters.AddWithValue("$lastError", error.Message);
            command.Parameters.AddWithValue("$now", now);
            command.ExecuteNonQuery();
        }

        private static IReadOnlyList<PendingFileOperation> LoadPendingOperations(SqliteConnection connection)
        {
            var operations = new List<PendingFileOperation>();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, OperationType, SavedFileName, StagingPath, FinalPath
                FROM PendingFileOperations
                ORDER BY Id";

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                operations.Add(new PendingFileOperation(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.GetString(4)));
            }

            return operations;
        }

        private static void Process(PendingFileOperation operation)
        {
            if (operation.OperationType == FileCleanup)
            {
                if (File.Exists(operation.FinalPath))
                {
                    File.Delete(operation.FinalPath);
                }

                return;
            }

            if (File.Exists(operation.FinalPath))
            {
                TryDeleteStaging(operation.StagingPath);
                return;
            }

            if (string.IsNullOrWhiteSpace(operation.StagingPath) || !File.Exists(operation.StagingPath))
            {
                throw new FileNotFoundException("ملف العملية المرحلي غير موجود.", operation.StagingPath ?? string.Empty);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(operation.FinalPath)!);
            File.Move(operation.StagingPath, operation.FinalPath);
        }

        private static void TryDeleteStaging(string? stagingPath)
        {
            if (string.IsNullOrWhiteSpace(stagingPath) || !File.Exists(stagingPath))
            {
                return;
            }

            File.Delete(stagingPath);
        }

        private static void Delete(SqliteConnection connection, int id)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM PendingFileOperations WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        private static void RecordAttempt(SqliteConnection connection, int id, Exception ex)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE PendingFileOperations
                SET Attempts = Attempts + 1,
                    LastError = $lastError,
                    UpdatedAt = $updatedAt
                WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$lastError", ex.Message);
            command.Parameters.AddWithValue("$updatedAt", PersistedDateTime.FormatDateTime(DateTime.Now));
            command.ExecuteNonQuery();
        }

        private sealed record PendingFileOperation(
            int Id,
            string OperationType,
            string SavedFileName,
            string? StagingPath,
            string FinalPath);
    }
}
