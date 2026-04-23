using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    internal static class WorkflowSchemaManager
    {
        public static void EnsureSchema(SqliteConnection connection)
        {
            if (!SqliteSchemaInspector.TableExists(connection, "WorkflowRequests"))
            {
                CreateWorkflowRequestsTable(connection);
                EnsureWorkflowIndexes(connection);
                return;
            }

            HashSet<string> existingColumns = SqliteSchemaInspector.GetTableColumns(connection, "WorkflowRequests");
            string[] requiredColumns =
            {
                "Id",
                "RootId",
                "SequenceNumber",
                "BaseVersionId",
                "ResultVersionId",
                "RequestType",
                "RequestStatus",
                "RequestDate",
                "CreatedAt",
                "UpdatedAt",
                "ResponseRecordedAt",
                "RequestedDataJson",
                "LetterOriginalFileName",
                "LetterSavedFileName",
                "ResponseOriginalFileName",
                "ResponseSavedFileName",
                "ResponseNotes",
                "Notes",
                "CreatedBy"
            };

            bool schemaMatches = requiredColumns.All(existingColumns.Contains);
            bool referencesGuaranteesId = false;

            var foreignKeyCommand = connection.CreateCommand();
            foreignKeyCommand.CommandText = "PRAGMA foreign_key_list('WorkflowRequests')";
            using (var fkReader = foreignKeyCommand.ExecuteReader())
            {
                while (fkReader.Read())
                {
                    string parentTable = fkReader.GetString(2);
                    string fromColumn = fkReader.GetString(3);
                    string toColumn = fkReader.GetString(4);

                    if (parentTable == "Guarantees" && fromColumn == "RootId" && toColumn == "Id")
                    {
                        referencesGuaranteesId = true;
                    }
                }
            }

            if (!schemaMatches || !referencesGuaranteesId)
            {
                SimpleLogger.Log("Detected outdated WorkflowRequests schema. Rebuilding safely...", "WARNING");
                RebuildWorkflowRequestsTable(connection);
                return;
            }

            EnsureWorkflowIndexes(connection);
        }

        public static void NormalizeLegacyCreatedBy(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE WorkflowRequests
                SET CreatedBy = $legacyValue
                WHERE CreatedBy IS NULL
                   OR TRIM(CreatedBy) = ''
                   OR LOWER(TRIM(CreatedBy)) = 'system'";
            command.Parameters.AddWithValue("$legacyValue", WorkflowCreatedByPolicy.LegacyValue);
            int affectedRows = command.ExecuteNonQuery();

            if (affectedRows > 0)
            {
                SimpleLogger.Log($"Normalized legacy WorkflowRequests.CreatedBy values to '{WorkflowCreatedByPolicy.LegacyValue}': {affectedRows} row(s).");
            }
        }

        private static void CreateWorkflowRequestsTable(SqliteConnection connection, SqliteTransaction? transaction = null)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS WorkflowRequests (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RootId INTEGER NOT NULL,
                    SequenceNumber INTEGER NOT NULL,
                    BaseVersionId INTEGER NOT NULL,
                    ResultVersionId INTEGER,
                    RequestType TEXT NOT NULL,
                    RequestStatus TEXT NOT NULL,
                    RequestDate TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    ResponseRecordedAt TEXT,
                    RequestedDataJson TEXT,
                    LetterOriginalFileName TEXT,
                    LetterSavedFileName TEXT,
                    ResponseOriginalFileName TEXT,
                    ResponseSavedFileName TEXT,
                    ResponseNotes TEXT,
                    Notes TEXT,
                    CreatedBy TEXT,
                    FOREIGN KEY(RootId) REFERENCES Guarantees(Id) ON DELETE CASCADE,
                    FOREIGN KEY(BaseVersionId) REFERENCES Guarantees(Id) ON DELETE RESTRICT,
                    FOREIGN KEY(ResultVersionId) REFERENCES Guarantees(Id) ON DELETE SET NULL
                );";
            command.ExecuteNonQuery();
        }

        private static void EnsureWorkflowIndexes(SqliteConnection connection, SqliteTransaction? transaction = null)
        {
            NormalizeWorkflowSequenceNumbers(connection, transaction);

            var indexCommand = connection.CreateCommand();
            indexCommand.Transaction = transaction;
            indexCommand.CommandText = @"
                CREATE INDEX IF NOT EXISTS idx_workflow_root ON WorkflowRequests(RootId);
                CREATE INDEX IF NOT EXISTS idx_workflow_status ON WorkflowRequests(RequestStatus);
                CREATE INDEX IF NOT EXISTS idx_workflow_type ON WorkflowRequests(RequestType);
                CREATE INDEX IF NOT EXISTS idx_workflow_status_type ON WorkflowRequests(RequestStatus, RequestType);
                CREATE INDEX IF NOT EXISTS idx_workflow_root_status ON WorkflowRequests(RootId, RequestStatus);
                CREATE INDEX IF NOT EXISTS idx_workflow_request_date ON WorkflowRequests(RequestDate DESC);
                CREATE INDEX IF NOT EXISTS idx_workflow_response_date ON WorkflowRequests(ResponseRecordedAt);
                CREATE UNIQUE INDEX IF NOT EXISTS ux_workflow_root_sequence
                    ON WorkflowRequests(RootId, SequenceNumber);
                CREATE UNIQUE INDEX IF NOT EXISTS ux_workflow_pending_per_type 
                    ON WorkflowRequests(RootId, RequestType) 
                    WHERE RequestStatus = 'Pending';";
            indexCommand.ExecuteNonQuery();
        }

        private static void NormalizeWorkflowSequenceNumbers(SqliteConnection connection, SqliteTransaction? transaction = null)
        {
            var affectedRootIds = new List<int>();
            var findCommand = connection.CreateCommand();
            findCommand.Transaction = transaction;
            findCommand.CommandText = @"
                SELECT RootId
                FROM WorkflowRequests
                GROUP BY RootId
                HAVING COUNT(*) <> COUNT(DISTINCT SequenceNumber)
                    OR MIN(SequenceNumber) < 1;";

            using (var reader = findCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    affectedRootIds.Add(reader.GetInt32(0));
                }
            }

            if (affectedRootIds.Count == 0)
            {
                return;
            }

            int updatedRows = 0;
            foreach (int rootId in affectedRootIds)
            {
                updatedRows += ResequenceWorkflowRequests(connection, rootId, transaction);
            }

            SimpleLogger.Log(
                $"NormalizeWorkflowSequenceNumbers: reassigned {updatedRows} sequence number(s) across {affectedRootIds.Count} workflow root(s).",
                "WARNING");
        }

        private static int ResequenceWorkflowRequests(SqliteConnection connection, int rootId, SqliteTransaction? transaction = null)
        {
            var requests = new List<(int Id, int SequenceNumber)>();
            var selectCommand = connection.CreateCommand();
            selectCommand.Transaction = transaction;
            selectCommand.CommandText = @"
                SELECT Id, SequenceNumber
                FROM WorkflowRequests
                WHERE RootId = $rid
                ORDER BY
                    CASE WHEN SequenceNumber < 1 THEN 1 ELSE 0 END,
                    SequenceNumber,
                    RequestDate,
                    CreatedAt,
                    Id;";
            selectCommand.Parameters.AddWithValue("$rid", rootId);

            using (var reader = selectCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    requests.Add((reader.GetInt32(0), reader.GetInt32(1)));
                }
            }

            int updatedRows = 0;
            for (int index = 0; index < requests.Count; index++)
            {
                int expectedSequence = index + 1;
                if (requests[index].SequenceNumber == expectedSequence)
                {
                    continue;
                }

                var updateCommand = connection.CreateCommand();
                updateCommand.Transaction = transaction;
                updateCommand.CommandText = "UPDATE WorkflowRequests SET SequenceNumber = $seq WHERE Id = $id";
                updateCommand.Parameters.AddWithValue("$seq", expectedSequence);
                updateCommand.Parameters.AddWithValue("$id", requests[index].Id);
                updateCommand.ExecuteNonQuery();
                updatedRows++;
            }

            return updatedRows;
        }

        private static string SqlLiteral(string value)
        {
            return $"'{value.Replace("'", "''")}'";
        }

        private static string GetTextValueExpression(HashSet<string> columns, string preferredColumn, params string[] fallbackColumns)
        {
            if (columns.Contains(preferredColumn))
            {
                return preferredColumn;
            }

            foreach (var fallbackColumn in fallbackColumns)
            {
                if (columns.Contains(fallbackColumn))
                {
                    return fallbackColumn;
                }
            }

            return "''";
        }

        private static string GetNullableTextValueExpression(HashSet<string> columns, string preferredColumn, params string[] fallbackColumns)
        {
            if (columns.Contains(preferredColumn))
            {
                return preferredColumn;
            }

            foreach (var fallbackColumn in fallbackColumns)
            {
                if (columns.Contains(fallbackColumn))
                {
                    return fallbackColumn;
                }
            }

            return "NULL";
        }

        private static string GetIntegerValueExpression(HashSet<string> columns, string preferredColumn, string fallback)
        {
            return columns.Contains(preferredColumn) ? preferredColumn : fallback;
        }

        private static string GetNullableIntegerValueExpression(HashSet<string> columns, string preferredColumn, params string[] fallbackColumns)
        {
            if (columns.Contains(preferredColumn))
            {
                return preferredColumn;
            }

            foreach (var fallbackColumn in fallbackColumns)
            {
                if (columns.Contains(fallbackColumn))
                {
                    return fallbackColumn;
                }
            }

            return "NULL";
        }

        private static string GetStatusExpression(HashSet<string> columns)
        {
            if (columns.Contains("RequestStatus"))
            {
                return "CASE RequestStatus WHEN 'Approved' THEN 'Executed' ELSE RequestStatus END";
            }

            return SqlLiteral("Pending");
        }

        private static void RebuildWorkflowRequestsTable(SqliteConnection connection)
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                var renameCommand = connection.CreateCommand();
                renameCommand.Transaction = transaction;
                renameCommand.CommandText = "ALTER TABLE WorkflowRequests RENAME TO WorkflowRequests_Old;";
                renameCommand.ExecuteNonQuery();

                CreateWorkflowRequestsTable(connection, transaction);

                var existingColumns = SqliteSchemaInspector.GetTableColumns(connection, "WorkflowRequests_Old", transaction);
                string requestDateExpression = GetTextValueExpression(existingColumns, "RequestDate", "CreatedAt", "UpdatedAt");
                string createdAtExpression = GetTextValueExpression(existingColumns, "CreatedAt", "RequestDate", "UpdatedAt");
                string updatedAtExpression = GetTextValueExpression(existingColumns, "UpdatedAt", "CreatedAt", "RequestDate");
                string rootIdExpression = GetIntegerValueExpression(existingColumns, "RootId", "GuaranteeId");
                string requestTypeExpression = GetTextValueExpression(existingColumns, "RequestType");
                string statusExpression = GetStatusExpression(existingColumns);
                string responseDateExpression = GetNullableTextValueExpression(existingColumns, "ResponseRecordedAt", "ApprovedAt");
                string requestedDataExpression = GetNullableTextValueExpression(existingColumns, "RequestedDataJson");
                string letterOriginalExpression = GetNullableTextValueExpression(existingColumns, "LetterOriginalFileName", "AttachmentOriginalFileName");
                string letterSavedExpression = GetNullableTextValueExpression(existingColumns, "LetterSavedFileName", "AttachmentSavedFileName");
                string responseOriginalExpression = GetNullableTextValueExpression(existingColumns, "ResponseOriginalFileName");
                string responseSavedExpression = GetNullableTextValueExpression(existingColumns, "ResponseSavedFileName");
                string responseNotesExpression = GetNullableTextValueExpression(existingColumns, "ResponseNotes");
                string notesExpression = GetNullableTextValueExpression(existingColumns, "Notes");
                string createdByExpression = GetNullableTextValueExpression(existingColumns, "CreatedBy");
                string resultVersionExpression = GetNullableIntegerValueExpression(existingColumns, "ResultVersionId", "ApprovedVersionId");
                string sequenceExpression = existingColumns.Contains("SequenceNumber")
                    ? "SequenceNumber"
                    : "ROW_NUMBER() OVER (PARTITION BY " + rootIdExpression + " ORDER BY " + requestDateExpression + ", Id)";
                string baseVersionExpression = GetIntegerValueExpression(existingColumns, "BaseVersionId", rootIdExpression);

                var migrateCommand = connection.CreateCommand();
                migrateCommand.Transaction = transaction;
                migrateCommand.CommandText = $@"
                    INSERT INTO WorkflowRequests (
                        Id, RootId, SequenceNumber, BaseVersionId, ResultVersionId, RequestType, RequestStatus,
                        RequestDate, CreatedAt, UpdatedAt, ResponseRecordedAt, RequestedDataJson,
                        LetterOriginalFileName, LetterSavedFileName, ResponseOriginalFileName, ResponseSavedFileName,
                        ResponseNotes, Notes, CreatedBy
                    )
                    SELECT
                        Id, {rootIdExpression}, {sequenceExpression}, {baseVersionExpression}, {resultVersionExpression}, {requestTypeExpression}, {statusExpression},
                        {requestDateExpression}, {createdAtExpression}, {updatedAtExpression}, {responseDateExpression}, {requestedDataExpression},
                        {letterOriginalExpression}, {letterSavedExpression}, {responseOriginalExpression}, {responseSavedExpression},
                        {responseNotesExpression}, {notesExpression}, {createdByExpression}
                    FROM WorkflowRequests_Old;";
                migrateCommand.ExecuteNonQuery();

                EnsureWorkflowIndexes(connection, transaction);

                var dropCommand = connection.CreateCommand();
                dropCommand.Transaction = transaction;
                dropCommand.CommandText = "DROP TABLE WorkflowRequests_Old;";
                dropCommand.ExecuteNonQuery();

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}
