using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    internal static class GuaranteeSchemaManager
    {
        public static void EnsureAttachmentsSchema(SqliteConnection connection)
        {
            if (!SqliteSchemaInspector.TableExists(connection, "Attachments"))
            {
                MigrateToMultiAttachment(connection);
            }

            EnsureAttachmentDocumentTypeColumn(connection);
            EnsureAttachmentTimelineEventColumn(connection);
        }

        public static void EnsureBaseSchema(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = @$"
                CREATE TABLE IF NOT EXISTS Guarantees (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Supplier TEXT NOT NULL,
                    Bank TEXT NOT NULL,
                    GuaranteeNo TEXT NOT NULL,
                    Amount DECIMAL NOT NULL CHECK(Amount >= 0),
                    ExpiryDate TEXT NOT NULL,
                    GuaranteeType TEXT NOT NULL,
                    Beneficiary TEXT,
                    Notes TEXT,
                    CreatedAt TEXT NOT NULL,
                    RootId INTEGER,
                    VersionNumber INTEGER DEFAULT 1,
                    IsCurrent INTEGER DEFAULT 1,
                    ReferenceType TEXT NOT NULL DEFAULT 'None',
                    ReferenceNumber TEXT,
                    LifecycleStatus TEXT NOT NULL DEFAULT 'Active',
                    ReplacesRootId INTEGER,
                    ReplacedByRootId INTEGER,
                    DateCalendar TEXT NOT NULL DEFAULT 'Gregorian'
                );
                CREATE TABLE IF NOT EXISTS Attachments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GuaranteeId INTEGER NOT NULL,
                    OriginalFileName TEXT NOT NULL,
                    SavedFileName TEXT NOT NULL,
                    FileExtension TEXT NOT NULL,
                    UploadedAt TEXT NOT NULL,
                    DocumentType TEXT NOT NULL DEFAULT 'SupportingDocument',
                    FOREIGN KEY(GuaranteeId) REFERENCES Guarantees(Id) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS BankReferences (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    CreatedAt TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_guarantee_no ON Guarantees(GuaranteeNo);
                CREATE UNIQUE INDEX IF NOT EXISTS ux_guarantee_no_single_current ON Guarantees({GuaranteeDataAccess.NormalizedGuaranteeNoSqlExpression}) WHERE IsCurrent = 1;
                CREATE INDEX IF NOT EXISTS idx_guarantee_root_current ON Guarantees(RootId, IsCurrent);
                CREATE INDEX IF NOT EXISTS idx_guarantee_supplier ON Guarantees(Supplier);
                CREATE INDEX IF NOT EXISTS idx_guarantee_bank ON Guarantees(Bank);
                CREATE INDEX IF NOT EXISTS idx_guarantee_expiry ON Guarantees(ExpiryDate);
                CREATE INDEX IF NOT EXISTS idx_guarantee_lifecycle ON Guarantees(LifecycleStatus);
                CREATE INDEX IF NOT EXISTS idx_guarantee_created_at ON Guarantees(CreatedAt DESC);
                CREATE INDEX IF NOT EXISTS idx_guarantee_current_bank ON Guarantees(IsCurrent, Bank);
                CREATE INDEX IF NOT EXISTS idx_guarantee_current_lifecycle ON Guarantees(IsCurrent, LifecycleStatus);
                CREATE INDEX IF NOT EXISTS idx_guarantee_reference ON Guarantees(ReferenceType, ReferenceNumber);
                CREATE INDEX IF NOT EXISTS idx_attachment_guarantee ON Attachments(GuaranteeId);
                CREATE INDEX IF NOT EXISTS idx_bank_references_name ON BankReferences(Name);
            ";
            command.ExecuteNonQuery();
        }

        public static void EnsureVersioningAndMetadataSchema(SqliteConnection connection)
        {
            EnsureVersioningSchema(connection);
            HashSet<string> guaranteeColumns = SqliteSchemaInspector.GetTableColumns(connection, "Guarantees");
            EnsureBeneficiaryColumn(connection, guaranteeColumns);
            NormalizeGuaranteeBeneficiaries(connection);
            EnsureReferenceColumns(connection, guaranteeColumns);
            EnsureLifecycleStatusColumn(connection, guaranteeColumns);
            EnsureReplacementRelationColumns(connection, guaranteeColumns);
            EnsureDateCalendarColumn(connection, guaranteeColumns);
        }

        public static void EnsureCurrentGuaranteeIntegrity(SqliteConnection connection)
        {
            GuaranteeDataAccess.NormalizeGuaranteeRoots(connection);
            NormalizeGuaranteeVersionNumbers(connection);
            EnsureGuaranteeUniqueCurrentIndex(connection);
            EnsureGuaranteeRootCurrentIndex(connection);
            EnsureGuaranteeRootVersionIndex(connection);
            EnsurePerformanceIndexes(connection);
            EnsureGuaranteeHardeningTriggers(connection);
            FlagExpiredGuaranteesLifecycleStatus(connection);
        }

        private static void EnsureVersioningSchema(SqliteConnection connection)
        {
            bool isUnique = false;
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "PRAGMA index_list('Guarantees')";
            using (var reader = checkCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (reader.GetString(1).Contains("GuaranteeNo") && reader.GetInt32(2) == 1)
                    {
                        isUnique = true;
                    }
                }
            }

            bool hasIsCurrent = SqliteSchemaInspector.GetTableColumns(connection, "Guarantees").Contains("IsCurrent");

            if (isUnique || !hasIsCurrent)
            {
                SimpleLogger.Log("Detected legacy schema (UNIQUE constraint or missing versioning columns). Performing safe migration...", "WARNING");
                MigrateToVersioning(connection);
            }
        }

        private static void MigrateToVersioning(SqliteConnection connection)
        {
            try
            {
                string backupPath = Path.Combine(
                    AppPaths.BackupFolder,
                    $"pre_versioning_backup_{DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.db");
                AppPaths.EnsureDirectoriesExist();
                SqliteBackupUtility.CreateBackup(connection, backupPath);
                SimpleLogger.Log($"Versioning migration backup created: {backupPath}");

                using var transaction = connection.BeginTransaction();
                try
                {
                    var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;

                    cmd.CommandText = "ALTER TABLE Guarantees RENAME TO Guarantees_PreVersioning;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"
                        CREATE TABLE Guarantees (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            Supplier TEXT NOT NULL,
                            Bank TEXT NOT NULL,
                            GuaranteeNo TEXT NOT NULL,
                            Amount DECIMAL NOT NULL CHECK(Amount >= 0),
                            ExpiryDate TEXT NOT NULL,
                            GuaranteeType TEXT NOT NULL,
                            Notes TEXT,
                            CreatedAt TEXT NOT NULL,
                            RootId INTEGER,
                            VersionNumber INTEGER DEFAULT 1,
                            IsCurrent INTEGER DEFAULT 1,
                            ReferenceType TEXT NOT NULL DEFAULT 'None',
                            ReferenceNumber TEXT,
                            LifecycleStatus TEXT NOT NULL DEFAULT 'Active',
                            ReplacesRootId INTEGER,
                            ReplacedByRootId INTEGER,
                            DateCalendar TEXT NOT NULL DEFAULT 'Gregorian'
                        );";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = @"
                        INSERT INTO Guarantees (Id, Supplier, Bank, GuaranteeNo, Amount, ExpiryDate, GuaranteeType, Notes, CreatedAt, RootId, VersionNumber, IsCurrent, ReferenceType, ReferenceNumber, LifecycleStatus, DateCalendar)
                        SELECT Id, Supplier, Bank, GuaranteeNo, Amount, ExpiryDate, GuaranteeType, Notes, CreatedAt, Id, 1, 1, 'None', '', 'Active', 'Gregorian'
                        FROM Guarantees_PreVersioning;";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "DROP TABLE Guarantees_PreVersioning;";
                    cmd.ExecuteNonQuery();

                    transaction.Commit();
                    SimpleLogger.Log("Database successfully migrated to Versioning Schema.");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    SimpleLogger.LogError(ex, "MigrateToVersioning failed");
                    throw;
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, "Critical Migration Failure");
                throw;
            }
        }

        private static void FlagExpiredGuaranteesLifecycleStatus(SqliteConnection connection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Guarantees
                SET LifecycleStatus = 'Expired'
                WHERE IsCurrent = 1
                  AND LifecycleStatus = 'Active'
                  AND date(ExpiryDate) < date('now')";
            int updated = cmd.ExecuteNonQuery();
            if (updated > 0)
            {
                SimpleLogger.Log($"FlagExpiredGuaranteesLifecycleStatus: marked {updated} guarantee(s) as Expired.");
            }
        }

        private static void EnsureGuaranteeUniqueCurrentIndex(SqliteConnection connection)
        {
            var cleanupCmd = connection.CreateCommand();
            cleanupCmd.CommandText = @$"
                UPDATE Guarantees SET IsCurrent = 0
                WHERE IsCurrent = 1
                  AND Id NOT IN (
                    SELECT MAX(Id) FROM Guarantees
                    WHERE IsCurrent = 1
                    GROUP BY {GuaranteeDataAccess.NormalizedGuaranteeNoSqlExpression}
                  )";
            int cleaned = cleanupCmd.ExecuteNonQuery();
            if (cleaned > 0)
            {
                SimpleLogger.Log($"EnsureGuaranteeUniqueCurrentIndex: deactivated {cleaned} duplicate IsCurrent entries.", "WARNING");
            }

            string? existingIndexSql = SqliteSchemaInspector.GetIndexSql(connection, "ux_guarantee_no_single_current");
            if (!IsGuaranteeCurrentIndexNormalized(existingIndexSql))
            {
                var dropCmd = connection.CreateCommand();
                dropCmd.CommandText = "DROP INDEX IF EXISTS ux_guarantee_no_single_current";
                dropCmd.ExecuteNonQuery();
            }

            var indexCmd = connection.CreateCommand();
            indexCmd.CommandText = $"CREATE UNIQUE INDEX IF NOT EXISTS ux_guarantee_no_single_current ON Guarantees({GuaranteeDataAccess.NormalizedGuaranteeNoSqlExpression}) WHERE IsCurrent = 1";
            indexCmd.ExecuteNonQuery();
        }

        private static void NormalizeGuaranteeVersionNumbers(SqliteConnection connection)
        {
            var select = connection.CreateCommand();
            select.CommandText = @"
                SELECT Id, COALESCE(RootId, Id) AS NormalizedRootId, VersionNumber
                FROM Guarantees
                ORDER BY NormalizedRootId ASC, VersionNumber ASC, CreatedAt ASC, Id ASC";

            var rows = new List<(int Id, int RootId, int VersionNumber)>();
            using (var reader = select.ExecuteReader())
            {
                while (reader.Read())
                {
                    rows.Add((reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)));
                }
            }

            int updated = 0;
            using var transaction = connection.BeginTransaction();
            foreach (IGrouping<int, (int Id, int RootId, int VersionNumber)> group in rows.GroupBy(row => row.RootId))
            {
                int expectedVersion = 1;
                foreach ((int id, _, int versionNumber) in group)
                {
                    if (versionNumber != expectedVersion)
                    {
                        var update = connection.CreateCommand();
                        update.Transaction = transaction;
                        update.CommandText = "UPDATE Guarantees SET VersionNumber = $version WHERE Id = $id";
                        update.Parameters.AddWithValue("$version", expectedVersion);
                        update.Parameters.AddWithValue("$id", id);
                        update.ExecuteNonQuery();
                        updated++;
                    }

                    expectedVersion++;
                }
            }

            transaction.Commit();
            if (updated > 0)
            {
                SimpleLogger.Log($"NormalizeGuaranteeVersionNumbers: renumbered {updated} guarantee version row(s).", "WARNING");
            }
        }

        private static void EnsureGuaranteeRootCurrentIndex(SqliteConnection connection)
        {
            var cleanup = connection.CreateCommand();
            cleanup.CommandText = @"
                UPDATE Guarantees SET IsCurrent = 0
                WHERE IsCurrent = 1
                  AND Id NOT IN (
                    SELECT Id
                    FROM (
                        SELECT Id,
                               ROW_NUMBER() OVER (
                                   PARTITION BY COALESCE(RootId, Id)
                                   ORDER BY VersionNumber DESC, CreatedAt DESC, Id DESC
                               ) AS RowNumber
                        FROM Guarantees
                        WHERE IsCurrent = 1
                    )
                    WHERE RowNumber = 1
                  )";
            int cleaned = cleanup.ExecuteNonQuery();
            if (cleaned > 0)
            {
                SimpleLogger.Log($"EnsureGuaranteeRootCurrentIndex: deactivated {cleaned} duplicate current version row(s).", "WARNING");
            }

            var index = connection.CreateCommand();
            index.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS ux_guarantee_root_current_single ON Guarantees(RootId) WHERE IsCurrent = 1";
            index.ExecuteNonQuery();
        }

        private static void EnsureGuaranteeRootVersionIndex(SqliteConnection connection)
        {
            var index = connection.CreateCommand();
            index.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS ux_guarantee_root_version ON Guarantees(RootId, VersionNumber)";
            index.ExecuteNonQuery();
        }

        private static void EnsurePerformanceIndexes(SqliteConnection connection)
        {
            var index = connection.CreateCommand();
            index.CommandText = @"
                CREATE INDEX IF NOT EXISTS idx_guarantee_created_at ON Guarantees(CreatedAt DESC);
                CREATE INDEX IF NOT EXISTS idx_guarantee_current_bank ON Guarantees(IsCurrent, Bank);
                CREATE INDEX IF NOT EXISTS idx_guarantee_current_lifecycle ON Guarantees(IsCurrent, LifecycleStatus);
                CREATE INDEX IF NOT EXISTS idx_guarantee_current_expiry ON Guarantees(IsCurrent, ExpiryDate);";
            index.ExecuteNonQuery();
        }

        private static void EnsureGuaranteeHardeningTriggers(SqliteConnection connection)
        {
            var trigger = connection.CreateCommand();
            trigger.CommandText = @"
                CREATE TRIGGER IF NOT EXISTS trg_guarantees_amount_nonnegative_insert
                BEFORE INSERT ON Guarantees
                WHEN NEW.Amount < 0
                BEGIN
                    SELECT RAISE(ABORT, 'Guarantee amount cannot be negative.');
                END;

                CREATE TRIGGER IF NOT EXISTS trg_guarantees_amount_nonnegative_update
                BEFORE UPDATE OF Amount ON Guarantees
                WHEN NEW.Amount < 0
                BEGIN
                    SELECT RAISE(ABORT, 'Guarantee amount cannot be negative.');
                END;

                CREATE TRIGGER IF NOT EXISTS trg_guarantees_valid_root_insert
                BEFORE INSERT ON Guarantees
                WHEN NEW.RootId IS NOT NULL
                     AND NOT EXISTS (SELECT 1 FROM Guarantees WHERE Id = NEW.RootId)
                BEGIN
                    SELECT RAISE(ABORT, 'Guarantee RootId must reference an existing guarantee.');
                END;

                CREATE TRIGGER IF NOT EXISTS trg_guarantees_valid_root_update
                BEFORE UPDATE OF RootId ON Guarantees
                WHEN NEW.RootId IS NOT NULL
                     AND NOT EXISTS (SELECT 1 FROM Guarantees WHERE Id = NEW.RootId)
                BEGIN
                    SELECT RAISE(ABORT, 'Guarantee RootId must reference an existing guarantee.');
                END;

                CREATE TRIGGER IF NOT EXISTS trg_guarantees_valid_replaces_root_insert
                BEFORE INSERT ON Guarantees
                WHEN NEW.ReplacesRootId IS NOT NULL
                     AND NOT EXISTS (SELECT 1 FROM Guarantees WHERE Id = NEW.ReplacesRootId)
                BEGIN
                    SELECT RAISE(ABORT, 'Replacement source root must reference an existing guarantee.');
                END;

                CREATE TRIGGER IF NOT EXISTS trg_guarantees_valid_replaces_root_update
                BEFORE UPDATE OF ReplacesRootId ON Guarantees
                WHEN NEW.ReplacesRootId IS NOT NULL
                     AND NOT EXISTS (SELECT 1 FROM Guarantees WHERE Id = NEW.ReplacesRootId)
                BEGIN
                    SELECT RAISE(ABORT, 'Replacement source root must reference an existing guarantee.');
                END;

                CREATE TRIGGER IF NOT EXISTS trg_guarantees_valid_replaced_by_root_insert
                BEFORE INSERT ON Guarantees
                WHEN NEW.ReplacedByRootId IS NOT NULL
                     AND NOT EXISTS (SELECT 1 FROM Guarantees WHERE Id = NEW.ReplacedByRootId)
                BEGIN
                    SELECT RAISE(ABORT, 'Replacement target root must reference an existing guarantee.');
                END;

                CREATE TRIGGER IF NOT EXISTS trg_guarantees_valid_replaced_by_root_update
                BEFORE UPDATE OF ReplacedByRootId ON Guarantees
                WHEN NEW.ReplacedByRootId IS NOT NULL
                     AND NOT EXISTS (SELECT 1 FROM Guarantees WHERE Id = NEW.ReplacedByRootId)
                BEGIN
                    SELECT RAISE(ABORT, 'Replacement target root must reference an existing guarantee.');
                END;

                CREATE TRIGGER IF NOT EXISTS trg_guarantees_no_hard_delete
                BEFORE DELETE ON Guarantees
                BEGIN
                    SELECT RAISE(ABORT, 'Guarantee records are append-only and cannot be hard-deleted.');
                END;

                CREATE TRIGGER IF NOT EXISTS trg_attachments_no_hard_delete
                BEFORE DELETE ON Attachments
                BEGIN
                    SELECT RAISE(ABORT, 'Attachment records are append-only and cannot be hard-deleted.');
                END;";
            trigger.ExecuteNonQuery();
        }

        private static bool IsGuaranteeCurrentIndexNormalized(string? indexSql)
        {
            if (string.IsNullOrWhiteSpace(indexSql))
            {
                return false;
            }

            string compactSql = string.Concat(indexSql.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
            string expectedExpression = string.Concat(GuaranteeDataAccess.NormalizedGuaranteeNoSqlExpression.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

            return compactSql.Contains("CREATEUNIQUEINDEX")
                && compactSql.Contains("UX_GUARANTEE_NO_SINGLE_CURRENT")
                && compactSql.Contains(expectedExpression)
                && compactSql.Contains("WHEREISCURRENT=1");
        }

        private static void MigrateToMultiAttachment(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Attachments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GuaranteeId INTEGER NOT NULL,
                    OriginalFileName TEXT NOT NULL,
                    SavedFileName TEXT NOT NULL,
                    FileExtension TEXT NOT NULL,
                    UploadedAt TEXT NOT NULL,
                    DocumentType TEXT NOT NULL DEFAULT 'SupportingDocument',
                    TimelineEventKey TEXT,
                    FOREIGN KEY(GuaranteeId) REFERENCES Guarantees(Id) ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS idx_attachment_guarantee ON Attachments(GuaranteeId);
                CREATE INDEX IF NOT EXISTS idx_attachment_timeline_event ON Attachments(TimelineEventKey);
            ";
            command.ExecuteNonQuery();

            bool hasOldAttachmentColumn = SqliteSchemaInspector.GetTableColumns(connection, "Guarantees").Contains("AttachmentFileName");

            if (!hasOldAttachmentColumn)
            {
                return;
            }

            var selectCmd = connection.CreateCommand();
            selectCmd.CommandText = "SELECT Id, AttachmentFileName FROM Guarantees WHERE AttachmentFileName IS NOT NULL AND AttachmentFileName != ''";

            var attachmentsToMigrate = new List<(int GuaranteeId, string FileName)>();
            using (var reader = selectCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    attachmentsToMigrate.Add((reader.GetInt32(0), reader.GetString(1)));
                }
            }

            foreach (var att in attachmentsToMigrate)
            {
                string ext = Path.GetExtension(att.FileName);
                var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO Attachments (GuaranteeId, OriginalFileName, SavedFileName, FileExtension, UploadedAt, DocumentType)
                    VALUES ($gid, $orig, $saved, $ext, $now, 'SupportingDocument')";
                insertCmd.Parameters.AddWithValue("$gid", att.GuaranteeId);
                insertCmd.Parameters.AddWithValue("$orig", att.FileName);
                insertCmd.Parameters.AddWithValue("$saved", att.FileName);
                insertCmd.Parameters.AddWithValue("$ext", ext);
                insertCmd.Parameters.AddWithValue("$now", PersistedDateTime.FormatDateTime(DateTime.Now));
                insertCmd.ExecuteNonQuery();
            }

            SimpleLogger.Log($"Migrated {attachmentsToMigrate.Count} old attachments to the new multi-attachment table.");
        }

        private static void EnsureAttachmentDocumentTypeColumn(SqliteConnection connection)
        {
            HashSet<string> columns = SqliteSchemaInspector.GetTableColumns(connection, "Attachments");
            if (columns.Contains("DocumentType"))
            {
                return;
            }

            var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE Attachments ADD COLUMN DocumentType TEXT NOT NULL DEFAULT 'SupportingDocument'";
            alter.ExecuteNonQuery();
            SimpleLogger.Log("Added DocumentType column to Attachments table.");
        }

        private static void EnsureAttachmentTimelineEventColumn(SqliteConnection connection)
        {
            HashSet<string> columns = SqliteSchemaInspector.GetTableColumns(connection, "Attachments");
            if (!columns.Contains("TimelineEventKey"))
            {
                var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE Attachments ADD COLUMN TimelineEventKey TEXT";
                alter.ExecuteNonQuery();
                SimpleLogger.Log("Added TimelineEventKey column to Attachments table.");
            }

            var index = connection.CreateCommand();
            index.CommandText = "CREATE INDEX IF NOT EXISTS idx_attachment_timeline_event ON Attachments(TimelineEventKey)";
            index.ExecuteNonQuery();
        }

        private static void EnsureBeneficiaryColumn(SqliteConnection connection, HashSet<string> columns)
        {
            if (!columns.Contains("Beneficiary"))
            {
                var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE Guarantees ADD COLUMN Beneficiary TEXT";
                alter.ExecuteNonQuery();
                columns.Add("Beneficiary");
            }
        }

        private static void NormalizeGuaranteeBeneficiaries(SqliteConnection connection)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE Guarantees
                SET Beneficiary = $beneficiary
                WHERE TRIM(IFNULL(Beneficiary, '')) <> $beneficiary";
            cmd.Parameters.AddWithValue("$beneficiary", BusinessPartyDefaults.DefaultBeneficiaryName);
            int updated = cmd.ExecuteNonQuery();
            if (updated > 0)
            {
                SimpleLogger.Log($"NormalizeGuaranteeBeneficiaries: normalized {updated} guarantee beneficiary value(s).");
            }
        }

        private static void EnsureReferenceColumns(SqliteConnection connection, HashSet<string> columns)
        {
            bool hasReferenceType = columns.Contains("ReferenceType");
            bool hasReferenceNumber = columns.Contains("ReferenceNumber");

            if (!hasReferenceType)
            {
                var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE Guarantees ADD COLUMN ReferenceType TEXT NOT NULL DEFAULT 'None'";
                alter.ExecuteNonQuery();
                columns.Add("ReferenceType");
            }

            if (!hasReferenceNumber)
            {
                var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE Guarantees ADD COLUMN ReferenceNumber TEXT";
                alter.ExecuteNonQuery();
                columns.Add("ReferenceNumber");
            }
        }

        private static void EnsureLifecycleStatusColumn(SqliteConnection connection, HashSet<string> columns)
        {
            if (!columns.Contains("LifecycleStatus"))
            {
                var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE Guarantees ADD COLUMN LifecycleStatus TEXT NOT NULL DEFAULT 'Active'";
                alter.ExecuteNonQuery();
                columns.Add("LifecycleStatus");
                SimpleLogger.Log("Added LifecycleStatus column to Guarantees table.");
            }
        }

        private static void EnsureReplacementRelationColumns(SqliteConnection connection, HashSet<string> columns)
        {
            bool hasReplacesRootId = columns.Contains("ReplacesRootId");
            bool hasReplacedByRootId = columns.Contains("ReplacedByRootId");

            if (!hasReplacesRootId)
            {
                var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE Guarantees ADD COLUMN ReplacesRootId INTEGER";
                alter.ExecuteNonQuery();
                columns.Add("ReplacesRootId");
            }

            if (!hasReplacedByRootId)
            {
                var alter = connection.CreateCommand();
                alter.CommandText = "ALTER TABLE Guarantees ADD COLUMN ReplacedByRootId INTEGER";
                alter.ExecuteNonQuery();
                columns.Add("ReplacedByRootId");
            }

            if (!hasReplacesRootId || !hasReplacedByRootId)
            {
                SimpleLogger.Log("Added replacement relation columns (ReplacesRootId, ReplacedByRootId) to Guarantees table.");
            }
        }

        private static void EnsureDateCalendarColumn(SqliteConnection connection, HashSet<string> columns)
        {
            if (columns.Contains("DateCalendar"))
            {
                return;
            }

            var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE Guarantees ADD COLUMN DateCalendar TEXT NOT NULL DEFAULT 'Gregorian'";
            alter.ExecuteNonQuery();
            columns.Add("DateCalendar");
            SimpleLogger.Log("Added DateCalendar column to Guarantees table.");
        }
    }
}
