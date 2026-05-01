using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    internal class GuaranteeRepository
    {
        private readonly string _connectionString;
        private readonly AttachmentStorageService _attachmentStorage;

        public GuaranteeRepository(string connectionString, AttachmentStorageService attachmentStorage)
        {
            _connectionString = connectionString;
            _attachmentStorage = attachmentStorage;
        }

        public void SaveGuarantee(Guarantee g, List<string> tempFilePaths)
        {
            SaveGuaranteeWithAttachments(g, tempFilePaths.Select(AttachmentInput.SupportingDocument).ToList());
        }

        public void SaveGuaranteeWithAttachments(Guarantee g, List<AttachmentInput> attachments)
        {
            using var scope = SimpleLogger.BeginScope("GuaranteeRepository.SaveGuarantee");
            List<AttachmentInput> attachmentInputs = attachments ?? new List<AttachmentInput>();
            List<StagedAttachmentFile> stagedAttachments = _attachmentStorage.StageCopies(attachmentInputs.Select(attachment => attachment.FilePath));
            bool committed = false;
            using var connection = SqliteConnectionFactory.Open(_connectionString);
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO Guarantees (Supplier, Bank, GuaranteeNo, Amount, ExpiryDate, GuaranteeType, Beneficiary, Notes, CreatedAt, RootId, VersionNumber, IsCurrent, ReferenceType, ReferenceNumber, LifecycleStatus, ReplacesRootId, ReplacedByRootId)
                        VALUES ($sup, $bank, $no, $amt, $exp, $type, $ben, $notes, $now, $pid, $ver, $curr, $referenceType, $referenceNumber, $lifecycle, $replacesRootId, $replacedByRootId);
                        SELECT last_insert_rowid();";

                    cmd.Parameters.AddWithValue("$referenceType", g.ReferenceType.ToString());
                    cmd.Parameters.AddWithValue("$referenceNumber", g.ReferenceNumber ?? string.Empty);
                    cmd.Parameters.AddWithValue("$ben", BusinessPartyDefaults.NormalizeBeneficiary(g.Beneficiary));
                    cmd.Parameters.AddWithValue("$sup", g.Supplier);
                    cmd.Parameters.AddWithValue("$bank", g.Bank);
                    cmd.Parameters.AddWithValue("$no", g.GuaranteeNo);
                    cmd.Parameters.AddWithValue("$amt", g.Amount);
                    cmd.Parameters.AddWithValue("$exp", PersistedDateTime.FormatDate(g.ExpiryDate));
                    cmd.Parameters.AddWithValue("$type", g.GuaranteeType);
                    cmd.Parameters.AddWithValue("$notes", g.Notes ?? "");
                    cmd.Parameters.AddWithValue("$now", PersistedDateTime.FormatDateTime(DateTime.Now));
                    cmd.Parameters.AddWithValue("$pid", g.RootId.HasValue ? g.RootId.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("$ver", g.VersionNumber);
                    cmd.Parameters.AddWithValue("$curr", g.IsCurrent ? 1 : 0);
                    cmd.Parameters.AddWithValue("$lifecycle", g.LifecycleStatus.ToString());
                    cmd.Parameters.AddWithValue("$replacesRootId", (object?)g.ReplacesRootId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("$replacedByRootId", (object?)g.ReplacedByRootId ?? DBNull.Value);

                    long guaranteeId = Convert.ToInt64(cmd.ExecuteScalar());

                    if (g.RootId == null)
                    {
                        var rootUpdate = connection.CreateCommand();
                        rootUpdate.Transaction = transaction;
                        rootUpdate.CommandText = "UPDATE Guarantees SET RootId = Id WHERE Id = $id";
                        rootUpdate.Parameters.AddWithValue("$id", guaranteeId);
                        rootUpdate.ExecuteNonQuery();
                    }

                    for (int i = 0; i < stagedAttachments.Count; i++)
                    {
                        StagedAttachmentFile stagedAttachment = stagedAttachments[i];
                        AttachmentDocumentType documentType = i < attachmentInputs.Count
                            ? attachmentInputs[i].DocumentType
                            : AttachmentDocumentType.SupportingDocument;
                        var attCmd = connection.CreateCommand();
                        attCmd.Transaction = transaction;
                        attCmd.CommandText = @"
                            INSERT INTO Attachments (GuaranteeId, OriginalFileName, SavedFileName, FileExtension, UploadedAt, DocumentType)
                            VALUES ($gid, $orig, $saved, $ext, $now, $documentType)";
                        attCmd.Parameters.AddWithValue("$gid", guaranteeId);
                        attCmd.Parameters.AddWithValue("$orig", stagedAttachment.OriginalFileName);
                        attCmd.Parameters.AddWithValue("$saved", stagedAttachment.SavedFileName);
                        attCmd.Parameters.AddWithValue("$ext", stagedAttachment.FileExtension);
                        attCmd.Parameters.AddWithValue("$now", PersistedDateTime.FormatDateTime(DateTime.Now));
                        attCmd.Parameters.AddWithValue("$documentType", documentType.ToString());
                        attCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    committed = true;
                    SimpleLogger.Log($"Saved new guarantee with {attachmentInputs.Count} attachments.");
                    SimpleLogger.LogAudit("SaveGuarantee", g.GuaranteeNo, $"Amount={g.Amount}, Expiry={g.ExpiryDate:yyyy-MM-dd}, Bank={g.Bank}");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    _attachmentStorage.CleanupStagedCopies(stagedAttachments);
                    throw OperationFailure.LogAndWrap(
                        ex,
                        "GuaranteeRepository.SaveGuarantee",
                        "تعذر حفظ بيانات الضمان الحالية.");
                }
            }

            if (committed)
            {
                _attachmentStorage.FinalizeStagedCopies(stagedAttachments, "SaveGuarantee");
            }
        }

        public void AddAttachments(int guaranteeId, List<AttachmentInput> attachments)
        {
            using var scope = SimpleLogger.BeginScope("GuaranteeRepository.AddAttachments");
            List<AttachmentInput> attachmentInputs = attachments ?? new List<AttachmentInput>();
            if (attachmentInputs.Count == 0)
            {
                return;
            }

            List<StagedAttachmentFile> stagedAttachments = _attachmentStorage.StageCopies(attachmentInputs.Select(attachment => attachment.FilePath));
            bool committed = false;
            using var connection = SqliteConnectionFactory.Open(_connectionString);
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    var guaranteeCommand = connection.CreateCommand();
                    guaranteeCommand.Transaction = transaction;
                    guaranteeCommand.CommandText = "SELECT GuaranteeNo FROM Guarantees WHERE Id = $id LIMIT 1";
                    guaranteeCommand.Parameters.AddWithValue("$id", guaranteeId);
                    object? guaranteeNoValue = guaranteeCommand.ExecuteScalar();
                    if (guaranteeNoValue == null || guaranteeNoValue == DBNull.Value)
                    {
                        throw new InvalidOperationException("تعذر العثور على الضمان المرتبط بالمرفق.");
                    }

                    for (int i = 0; i < stagedAttachments.Count; i++)
                    {
                        StagedAttachmentFile stagedAttachment = stagedAttachments[i];
                        AttachmentDocumentType documentType = i < attachmentInputs.Count
                            ? attachmentInputs[i].DocumentType
                            : AttachmentDocumentType.SupportingDocument;

                        var attachmentCommand = connection.CreateCommand();
                        attachmentCommand.Transaction = transaction;
                        attachmentCommand.CommandText = @"
                            INSERT INTO Attachments (GuaranteeId, OriginalFileName, SavedFileName, FileExtension, UploadedAt, DocumentType, TimelineEventKey)
                            VALUES ($gid, $orig, $saved, $ext, $now, $documentType, $timelineEventKey)";
                        attachmentCommand.Parameters.AddWithValue("$gid", guaranteeId);
                        attachmentCommand.Parameters.AddWithValue("$orig", stagedAttachment.OriginalFileName);
                        attachmentCommand.Parameters.AddWithValue("$saved", stagedAttachment.SavedFileName);
                        attachmentCommand.Parameters.AddWithValue("$ext", stagedAttachment.FileExtension);
                        attachmentCommand.Parameters.AddWithValue("$now", PersistedDateTime.FormatDateTime(DateTime.Now));
                        attachmentCommand.Parameters.AddWithValue("$documentType", documentType.ToString());
                        attachmentCommand.Parameters.AddWithValue("$timelineEventKey", attachmentInputs[i].TimelineEventKey ?? string.Empty);
                        attachmentCommand.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    committed = true;
                    string guaranteeNo = Convert.ToString(guaranteeNoValue, CultureInfo.InvariantCulture) ?? guaranteeId.ToString(CultureInfo.InvariantCulture);
                    SimpleLogger.Log($"Added {attachmentInputs.Count} attachments to guarantee {guaranteeNo} (id={guaranteeId}).");
                    SimpleLogger.LogAudit("AddGuaranteeAttachments", guaranteeNo, $"Count={attachmentInputs.Count}");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    _attachmentStorage.CleanupStagedCopies(stagedAttachments);
                    throw OperationFailure.LogAndWrap(
                        ex,
                        "GuaranteeRepository.AddAttachments",
                        "تعذر إرفاق المستند بالضمان المحدد.");
                }
            }

            if (committed)
            {
                _attachmentStorage.FinalizeStagedCopies(stagedAttachments, "AddGuaranteeAttachments");
            }
        }

        public int UpdateGuarantee(Guarantee g, List<string> newTempFiles, List<AttachmentRecord> removedAttachments)
        {
            return UpdateGuaranteeWithAttachments(
                g,
                newTempFiles.Select(AttachmentInput.SupportingDocument).ToList(),
                removedAttachments);
        }

        public int UpdateGuaranteeWithAttachments(Guarantee g, List<AttachmentInput> newAttachments, List<AttachmentRecord> removedAttachments)
        {
            using var scope = SimpleLogger.BeginScope("GuaranteeRepository.UpdateGuarantee");
            // بدلاً من تعديل الصف الحالي مباشرة، ننشئ نسخة جديدة لحفظ سلسلة التدقيق (Audit Trail).
            // المرفقات المحذوفة لا تُحذف من النسخة القديمة — تبقى في السجل التاريخي.
            var inheritedAttachments = g.Attachments
                .Where(a => !removedAttachments.Any(r => r.Id == a.Id))
                .ToList();

            int newId = CreateNewVersionWithAttachments(g, g.Id, newAttachments, inheritedAttachments);
            SimpleLogger.Log($"UpdateGuarantee: created version for {g.GuaranteeNo} (oldId={g.Id}, newId={newId}, removedCount={removedAttachments.Count}).");
            SimpleLogger.LogAudit("UpdateGuarantee", g.GuaranteeNo, $"NewId={newId}, Amount={g.Amount}, Expiry={g.ExpiryDate:yyyy-MM-dd}");
            return newId;
        }

        public List<Guarantee> QueryGuarantees(GuaranteeQueryOptions options)
        {
            using var scope = SimpleLogger.BeginScope("GuaranteeRepository.QueryGuarantees");
            GuaranteeQueryOptions effectiveOptions = options ?? new GuaranteeQueryOptions();
            List<Guarantee> list = new();

            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                SqliteCommand cmd = BuildGuaranteeQueryCommand(connection, effectiveOptions, countOnly: false);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(GuaranteeDataAccess.MapGuarantee(reader));
                }

                if (effectiveOptions.IncludeAttachments)
                {
                    LoadAttachmentsForGuarantees(list, connection);
                }
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "GuaranteeRepository.QueryGuarantees",
                    "تعذر تحميل قائمة الضمانات الحالية.");
            }

            return list;
        }

        public int CountGuarantees(GuaranteeQueryOptions? options = null)
        {
            using var scope = SimpleLogger.BeginScope("GuaranteeRepository.CountGuarantees");
            GuaranteeQueryOptions effectiveOptions = options ?? new GuaranteeQueryOptions();

            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                using SqliteCommand cmd = BuildGuaranteeQueryCommand(connection, effectiveOptions, countOnly: true);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "GuaranteeRepository.CountGuarantees",
                    "تعذر احتساب الضمانات الحالية.");
            }
        }

        public int CountAttachments()
        {
            using var scope = SimpleLogger.BeginScope("GuaranteeRepository.CountAttachments");

            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT COUNT(*)
                    FROM Attachments a
                    INNER JOIN Guarantees g ON g.Id = a.GuaranteeId
                    WHERE g.IsCurrent = 1";
                return Convert.ToInt32(command.ExecuteScalar());
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "GuaranteeRepository.CountAttachments",
                    "تعذر احتساب المرفقات الحالية.");
            }
        }

        public List<Guarantee> GetGuaranteeHistory(int guaranteeId)
        {
            using var scope = SimpleLogger.BeginScope("GuaranteeRepository.GetGuaranteeHistory");
            var list = new List<Guarantee>();
            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                var rootCmd = connection.CreateCommand();
                rootCmd.CommandText = "SELECT COALESCE(RootId, Id) FROM Guarantees WHERE Id = $id LIMIT 1";
                rootCmd.Parameters.AddWithValue("$id", guaranteeId);

                object? rootValue = rootCmd.ExecuteScalar();
                if (rootValue == null || rootValue == DBNull.Value)
                {
                    return list;
                }

                int rootId = Convert.ToInt32(rootValue);
                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT " + GuaranteeDataAccess.SelectColumns + @"
                    FROM Guarantees
                    WHERE COALESCE(RootId, Id) = $rid
                    ORDER BY VersionNumber DESC, CreatedAt DESC";
                cmd.Parameters.AddWithValue("$rid", rootId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(GuaranteeDataAccess.MapGuarantee(reader));
                }

                LoadAttachmentsForGuarantees(list, connection);
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "GuaranteeRepository.GetGuaranteeHistory",
                    "تعذر تحميل تاريخ الضمان المطلوب.");
            }

            return list;
        }

        public Dictionary<int, IReadOnlyList<AttachmentRecord>> GetSeriesAttachmentsByRootIds(IReadOnlyCollection<int> rootIds)
        {
            using var scope = SimpleLogger.BeginScope("GuaranteeRepository.GetSeriesAttachmentsByRootIds");
            List<int> distinctRootIds = (rootIds ?? Array.Empty<int>())
                .Where(rootId => rootId > 0)
                .Distinct()
                .ToList();

            Dictionary<int, IReadOnlyList<AttachmentRecord>> result = distinctRootIds
                .ToDictionary(rootId => rootId, _ => (IReadOnlyList<AttachmentRecord>)new List<AttachmentRecord>());

            if (distinctRootIds.Count == 0)
            {
                return result;
            }

            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                var command = connection.CreateCommand();

                var parameterNames = new List<string>();
                for (int index = 0; index < distinctRootIds.Count; index++)
                {
                    string parameterName = $"$root{index}";
                    parameterNames.Add(parameterName);
                    command.Parameters.AddWithValue(parameterName, distinctRootIds[index]);
                }

                command.CommandText = $@"
                    SELECT a.Id, a.GuaranteeId, a.OriginalFileName, a.SavedFileName, a.FileExtension,
                           a.UploadedAt, a.DocumentType, a.TimelineEventKey, COALESCE(g.RootId, g.Id) AS RootId
                    FROM Attachments a
                    INNER JOIN Guarantees g ON g.Id = a.GuaranteeId
                    WHERE COALESCE(g.RootId, g.Id) IN ({string.Join(", ", parameterNames)})
                    ORDER BY RootId, a.UploadedAt DESC, a.Id DESC";

                var attachmentsByRoot = new Dictionary<int, List<AttachmentRecord>>();
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    AttachmentRecord attachment = GuaranteeDataAccess.MapAttachment(reader);
                    int rootId = reader.GetInt32(8);
                    if (!attachmentsByRoot.TryGetValue(rootId, out List<AttachmentRecord>? bucket))
                    {
                        bucket = new List<AttachmentRecord>();
                        attachmentsByRoot[rootId] = bucket;
                    }

                    bucket.Add(attachment);
                }

                foreach ((int rootId, List<AttachmentRecord> attachments) in attachmentsByRoot)
                {
                    result[rootId] = BuildSeriesAttachments(attachments);
                }
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "GuaranteeRepository.GetSeriesAttachmentsByRootIds",
                    "تعذر تحميل مرفقات سلاسل الضمانات المعروضة.");
            }

            return result;
        }

        public List<Guarantee> SearchGuarantees(string query)
        {
            return QueryGuarantees(new GuaranteeQueryOptions
            {
                SearchText = query ?? string.Empty,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            });
        }

        public void DeleteAttachment(AttachmentRecord att)
        {
            using var scope = SimpleLogger.BeginScope("GuaranteeRepository.DeleteAttachment");
            using var connection = SqliteConnectionFactory.Open(_connectionString);
            using var transaction = connection.BeginTransaction();
            try
            {
                var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM Attachments WHERE Id = $id";
                cmd.Parameters.AddWithValue("$id", att.Id);
                cmd.ExecuteNonQuery();

                transaction.Commit();
                _attachmentStorage.DeletePhysicalFileIfUnreferenced(att.SavedFileName, connection);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw OperationFailure.LogAndWrap(
                    ex,
                    "GuaranteeRepository.DeleteAttachment",
                    "تعذر حذف المرفق المحدد.");
            }
        }

        public bool IsGuaranteeNoUnique(string guaranteeNo)
        {
            using var scope = SimpleLogger.BeginScope("GuaranteeRepository.IsGuaranteeNoUnique");
            try
            {
                string normalizedGuaranteeNo = GuaranteeDataAccess.NormalizeGuaranteeNo(guaranteeNo);
                if (string.IsNullOrWhiteSpace(normalizedGuaranteeNo))
                {
                    return false;
                }

                using var connection = SqliteConnectionFactory.Open(_connectionString);
                var cmd = connection.CreateCommand();
                cmd.CommandText = $"SELECT COUNT(*) FROM Guarantees WHERE {GuaranteeDataAccess.NormalizedGuaranteeNoSqlExpression} = $no AND IsCurrent = 1";
                cmd.Parameters.AddWithValue("$no", normalizedGuaranteeNo);
                return Convert.ToInt32(cmd.ExecuteScalar()) == 0;
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "GuaranteeRepository.IsGuaranteeNoUnique",
                    "تعذر التحقق من تكرار رقم الضمان.");
            }
        }

        public Guarantee? GetGuaranteeById(int guaranteeId)
        {
            using var scope = SimpleLogger.BeginScope("GuaranteeRepository.GetGuaranteeById");
            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                return GetGuaranteeById(guaranteeId, connection);
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "GuaranteeRepository.GetGuaranteeById",
                    "تعذر تحميل ملف الضمان المطلوب.");
            }
        }

        public Guarantee? GetCurrentGuaranteeByRootId(int rootId)
        {
            using var scope = SimpleLogger.BeginScope("GuaranteeRepository.GetCurrentGuaranteeByRootId");
            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                return GetCurrentGuaranteeByRootId(rootId, connection);
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "GuaranteeRepository.GetCurrentGuaranteeByRootId",
                    "تعذر تحميل النسخة الحالية للضمان المطلوب.");
            }
        }

        public Guarantee? GetCurrentGuaranteeByNo(string guaranteeNo)
        {
            using var scope = SimpleLogger.BeginScope("GuaranteeRepository.GetCurrentGuaranteeByNo");
            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                return GetCurrentGuaranteeByNo(guaranteeNo, connection);
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "GuaranteeRepository.GetCurrentGuaranteeByNo",
                    "تعذر تحميل الضمان المطلوب برقم الضمان.");
            }
        }

        public int CreateNewVersion(Guarantee newG, int sourceId, List<string> newTempFiles, List<AttachmentRecord> inheritedAttachments)
        {
            return CreateNewVersionWithAttachments(
                newG,
                sourceId,
                newTempFiles.Select(AttachmentInput.SupportingDocument).ToList(),
                inheritedAttachments);
        }

        public int CreateNewVersionWithAttachments(Guarantee newG, int sourceId, List<AttachmentInput> newAttachments, List<AttachmentRecord> inheritedAttachments)
        {
            using var scope = SimpleLogger.BeginScope("GuaranteeRepository.CreateNewVersion");
            List<AttachmentInput> attachmentInputs = newAttachments ?? new List<AttachmentInput>();
            List<StagedAttachmentFile> stagedAttachments = _attachmentStorage.StageCopies(attachmentInputs.Select(attachment => attachment.FilePath));
            int newGuaranteeId = 0;
            bool committed = false;
            using var connection = SqliteConnectionFactory.Open(_connectionString);
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    GuaranteeDataAccess.NormalizeGuaranteeRoots(connection, transaction);

                    int rootId = sourceId;
                    var rootCmd = connection.CreateCommand();
                    rootCmd.Transaction = transaction;
                    rootCmd.CommandText = "SELECT COALESCE(RootId, Id) FROM Guarantees WHERE Id = $id";
                    rootCmd.Parameters.AddWithValue("$id", sourceId);
                    rootId = Convert.ToInt32(rootCmd.ExecuteScalar());

                    var verCmd = connection.CreateCommand();
                    verCmd.Transaction = transaction;
                    verCmd.CommandText = "SELECT IFNULL(MAX(VersionNumber), 0) FROM Guarantees WHERE COALESCE(RootId, Id) = $rid";
                    verCmd.Parameters.AddWithValue("$rid", rootId);
                    int nextVer = Convert.ToInt32(verCmd.ExecuteScalar()) + 1;

                    var resetCmd = connection.CreateCommand();
                    resetCmd.Transaction = transaction;
                    resetCmd.CommandText = "UPDATE Guarantees SET IsCurrent = 0 WHERE COALESCE(RootId, Id) = $rid";
                    resetCmd.Parameters.AddWithValue("$rid", rootId);
                    resetCmd.ExecuteNonQuery();

                    var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO Guarantees (Supplier, Bank, GuaranteeNo, Amount, ExpiryDate, GuaranteeType, Beneficiary, Notes, CreatedAt, RootId, VersionNumber, IsCurrent, ReferenceType, ReferenceNumber, LifecycleStatus, ReplacesRootId, ReplacedByRootId)
                        VALUES ($sup, $bank, $no, $amt, $exp, $type, $ben, $notes, $now, $pid, $ver, 1, $referenceType, $referenceNumber, $lifecycle, $replacesRootId, $replacedByRootId);
                        SELECT last_insert_rowid();";

                    cmd.Parameters.AddWithValue("$ben", BusinessPartyDefaults.NormalizeBeneficiary(newG.Beneficiary));
                    cmd.Parameters.AddWithValue("$referenceType", newG.ReferenceType.ToString());
                    cmd.Parameters.AddWithValue("$referenceNumber", newG.ReferenceNumber ?? string.Empty);
                    cmd.Parameters.AddWithValue("$lifecycle", newG.LifecycleStatus.ToString());
                    cmd.Parameters.AddWithValue("$replacesRootId", (object?)newG.ReplacesRootId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("$replacedByRootId", (object?)newG.ReplacedByRootId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("$sup", newG.Supplier);
                    cmd.Parameters.AddWithValue("$bank", newG.Bank);
                    cmd.Parameters.AddWithValue("$no", newG.GuaranteeNo);
                    cmd.Parameters.AddWithValue("$amt", newG.Amount);
                    cmd.Parameters.AddWithValue("$exp", PersistedDateTime.FormatDate(newG.ExpiryDate));
                    cmd.Parameters.AddWithValue("$type", newG.GuaranteeType);
                    cmd.Parameters.AddWithValue("$notes", newG.Notes ?? "");
                    cmd.Parameters.AddWithValue("$now", PersistedDateTime.FormatDateTime(DateTime.Now));
                    cmd.Parameters.AddWithValue("$pid", rootId);
                    cmd.Parameters.AddWithValue("$ver", nextVer);

                    newGuaranteeId = Convert.ToInt32(cmd.ExecuteScalar());

                    foreach (var att in inheritedAttachments)
                    {
                        var linkCmd = connection.CreateCommand();
                        linkCmd.Transaction = transaction;
                        linkCmd.CommandText = @"
                            INSERT INTO Attachments (GuaranteeId, OriginalFileName, SavedFileName, FileExtension, UploadedAt, DocumentType)
                            VALUES ($gid, $orig, $saved, $ext, $now, $documentType)";
                        linkCmd.Parameters.AddWithValue("$gid", newGuaranteeId);
                        linkCmd.Parameters.AddWithValue("$orig", att.OriginalFileName);
                        linkCmd.Parameters.AddWithValue("$saved", att.SavedFileName);
                        linkCmd.Parameters.AddWithValue("$ext", att.FileExtension);
                        linkCmd.Parameters.AddWithValue("$now", PersistedDateTime.FormatDateTime(DateTime.Now));
                        linkCmd.Parameters.AddWithValue("$documentType", att.DocumentType.ToString());
                        linkCmd.ExecuteNonQuery();
                    }

                    for (int i = 0; i < stagedAttachments.Count; i++)
                    {
                        StagedAttachmentFile stagedAttachment = stagedAttachments[i];
                        AttachmentDocumentType documentType = i < attachmentInputs.Count
                            ? attachmentInputs[i].DocumentType
                            : AttachmentDocumentType.SupportingDocument;
                        var attCmd = connection.CreateCommand();
                        attCmd.Transaction = transaction;
                        attCmd.CommandText = @"
                            INSERT INTO Attachments (GuaranteeId, OriginalFileName, SavedFileName, FileExtension, UploadedAt, DocumentType)
                            VALUES ($gid, $orig, $saved, $ext, $now, $documentType)";
                        attCmd.Parameters.AddWithValue("$gid", newGuaranteeId);
                        attCmd.Parameters.AddWithValue("$orig", stagedAttachment.OriginalFileName);
                        attCmd.Parameters.AddWithValue("$saved", stagedAttachment.SavedFileName);
                        attCmd.Parameters.AddWithValue("$ext", stagedAttachment.FileExtension);
                        attCmd.Parameters.AddWithValue("$now", PersistedDateTime.FormatDateTime(DateTime.Now));
                        attCmd.Parameters.AddWithValue("$documentType", documentType.ToString());
                        attCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    committed = true;
                    SimpleLogger.Log($"Created new version {nextVer} for Guarantee {newG.GuaranteeNo}. Parent Root: {rootId}. Inherited: {inheritedAttachments.Count}.");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    _attachmentStorage.CleanupStagedCopies(stagedAttachments);
                    throw OperationFailure.LogAndWrap(
                        ex,
                        "GuaranteeRepository.CreateNewVersion",
                        "تعذر إنشاء نسخة جديدة للضمان المطلوب.");
                }
            }

            if (committed)
            {
                _attachmentStorage.FinalizeStagedCopies(stagedAttachments, "CreateNewVersion");
            }

            return newGuaranteeId;
        }

        private static void PromoteNextBestVersion(int rootId, SqliteConnection connection, SqliteTransaction? transaction = null)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                UPDATE Guarantees
                SET IsCurrent = 0
                WHERE COALESCE(RootId, Id) = $rid;

                UPDATE Guarantees 
                SET IsCurrent = 1 
                WHERE Id = (
                    SELECT Id FROM Guarantees 
                    WHERE COALESCE(RootId, Id) = $rid
                    ORDER BY VersionNumber DESC 
                    LIMIT 1
                )";
            cmd.Parameters.AddWithValue("$rid", rootId);
            cmd.ExecuteNonQuery();
        }

        private Guarantee? GetGuaranteeById(int guaranteeId, SqliteConnection connection, SqliteTransaction? transaction = null)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {GuaranteeDataAccess.SelectColumns} FROM Guarantees WHERE Id = $id LIMIT 1";
            cmd.Parameters.AddWithValue("$id", guaranteeId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            var guarantee = GuaranteeDataAccess.MapGuarantee(reader);
            guarantee.Attachments = GuaranteeDataAccess.GetAttachmentsForGuarantee(guarantee.Id, connection, transaction);
            return guarantee;
        }

        private Guarantee? GetCurrentGuaranteeByRootId(int rootId, SqliteConnection connection, SqliteTransaction? transaction = null)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $@"
                SELECT {GuaranteeDataAccess.SelectColumns}
                FROM Guarantees
                WHERE COALESCE(RootId, Id) = $rid AND IsCurrent = 1
                LIMIT 1";
            cmd.Parameters.AddWithValue("$rid", rootId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            var guarantee = GuaranteeDataAccess.MapGuarantee(reader);
            guarantee.Attachments = GuaranteeDataAccess.GetAttachmentsForGuarantee(guarantee.Id, connection, transaction);
            return guarantee;
        }

        private Guarantee? GetCurrentGuaranteeByNo(string guaranteeNo, SqliteConnection connection, SqliteTransaction? transaction = null)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {GuaranteeDataAccess.SelectColumns} FROM Guarantees WHERE {GuaranteeDataAccess.NormalizedGuaranteeNoSqlExpression} = $no AND IsCurrent = 1 LIMIT 1";
            cmd.Parameters.AddWithValue("$no", GuaranteeDataAccess.NormalizeGuaranteeNo(guaranteeNo));

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            var guarantee = GuaranteeDataAccess.MapGuarantee(reader);
            guarantee.Attachments = GuaranteeDataAccess.GetAttachmentsForGuarantee(guarantee.Id, connection, transaction);
            return guarantee;
        }

        private static SqliteCommand BuildGuaranteeQueryCommand(SqliteConnection connection, GuaranteeQueryOptions options, bool countOnly)
        {
            StringBuilder sql = new();
            if (countOnly)
            {
                sql.Append("SELECT COUNT(*) FROM Guarantees");
            }
            else
            {
                sql.Append($"SELECT {GuaranteeDataAccess.SelectColumns} FROM Guarantees");
            }

            SqliteCommand command = connection.CreateCommand();
            AppendGuaranteeFilters(sql, command, options);

            if (!countOnly)
            {
                sql.AppendLine();
                sql.Append("ORDER BY ");
                switch (options.SortMode)
                {
                    case GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo:
                        sql.Append("ExpiryDate ASC, GuaranteeNo ASC");
                        break;
                    default:
                        sql.Append("CreatedAt DESC, Id DESC");
                        break;
                }

                if (options.Limit.HasValue && options.Limit.Value > 0)
                {
                    sql.Append(" LIMIT $limit");
                    command.Parameters.AddWithValue("$limit", options.Limit.Value);
                }

                if (options.Offset.HasValue && options.Offset.Value > 0)
                {
                    if (!options.Limit.HasValue || options.Limit.Value <= 0)
                    {
                        sql.Append(" LIMIT -1");
                    }

                    sql.Append(" OFFSET $offset");
                    command.Parameters.AddWithValue("$offset", options.Offset.Value);
                }
            }

            command.CommandText = sql.ToString();
            return command;
        }

        private static void AppendGuaranteeFilters(StringBuilder sql, SqliteCommand command, GuaranteeQueryOptions options)
        {
            string normalizedSearch = options.SearchText?.Trim().ToLowerInvariant() ?? string.Empty;
            string normalizedBank = options.Bank?.Trim().ToLowerInvariant() ?? string.Empty;
            string normalizedSupplier = options.Supplier?.Trim().ToLowerInvariant() ?? string.Empty;
            string normalizedGuaranteeType = options.GuaranteeType?.Trim().ToLowerInvariant() ?? string.Empty;
            string today = PersistedDateTime.FormatDate(DateTime.Today);
            string expiringSoonCutoff = PersistedDateTime.FormatDate(DateTime.Today.AddDays(30));

            sql.AppendLine();
            sql.Append("WHERE IsCurrent = 1");

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                sql.Append(@"
 AND (
        LOWER(IFNULL(GuaranteeNo, '')) LIKE $search
     OR LOWER(IFNULL(Supplier, '')) LIKE $search
     OR LOWER(IFNULL(Bank, '')) LIKE $search
     OR LOWER(IFNULL(GuaranteeType, '')) LIKE $search
     OR LOWER(IFNULL(ReferenceNumber, '')) LIKE $search
     OR LOWER(IFNULL(Beneficiary, '')) LIKE $search
 )");
                command.Parameters.AddWithValue("$search", $"%{normalizedSearch}%");
            }

            if (!string.IsNullOrWhiteSpace(normalizedBank))
            {
                sql.Append(" AND LOWER(TRIM(IFNULL(Bank, ''))) = $bank");
                command.Parameters.AddWithValue("$bank", normalizedBank);
            }

            if (!string.IsNullOrWhiteSpace(normalizedSupplier))
            {
                sql.Append(" AND LOWER(TRIM(IFNULL(Supplier, ''))) = $supplier");
                command.Parameters.AddWithValue("$supplier", normalizedSupplier);
            }

            if (!string.IsNullOrWhiteSpace(normalizedGuaranteeType))
            {
                sql.Append(" AND LOWER(TRIM(IFNULL(GuaranteeType, ''))) = $guaranteeType");
                command.Parameters.AddWithValue("$guaranteeType", normalizedGuaranteeType);
            }

            if (options.LifecycleStatuses is { Count: > 0 } lifecycleStatuses)
            {
                List<string> parameterNames = new();
                int index = 0;
                foreach (GuaranteeLifecycleStatus lifecycleStatus in lifecycleStatuses.Distinct())
                {
                    string parameterName = $"$lifecycleStatus{index.ToString(CultureInfo.InvariantCulture)}";
                    parameterNames.Add(parameterName);
                    command.Parameters.AddWithValue(parameterName, lifecycleStatus.ToString());
                    index++;
                }

                sql.Append($" AND LifecycleStatus IN ({string.Join(", ", parameterNames)})");
            }
            else if (options.LifecycleStatus.HasValue)
            {
                sql.Append(" AND LifecycleStatus = $lifecycleStatus");
                command.Parameters.AddWithValue("$lifecycleStatus", options.LifecycleStatus.Value.ToString());
            }

            if (options.ReferenceType.HasValue)
            {
                sql.Append(" AND ReferenceType = $referenceType");
                command.Parameters.AddWithValue("$referenceType", options.ReferenceType.Value.ToString());
            }

            if (options.RequireReferenceNumber)
            {
                sql.Append(" AND TRIM(IFNULL(ReferenceNumber, '')) <> ''");
            }

            if (options.NotExpiredOnly)
            {
                sql.Append(" AND ExpiryDate >= $todayDate");
            }

            if (options.NeedsExpiryFollowUpOnly)
            {
                sql.Append(" AND ExpiryDate < $todayDate AND LifecycleStatus IN ($expiryActiveStatus, $expiryExpiredStatus)");
                command.Parameters.AddWithValue("$expiryActiveStatus", GuaranteeLifecycleStatus.Active.ToString());
                command.Parameters.AddWithValue("$expiryExpiredStatus", GuaranteeLifecycleStatus.Expired.ToString());
            }
            else if (options.UrgentOnly)
            {
                sql.Append(" AND (ExpiryDate < $todayDate OR (ExpiryDate >= $todayDate AND ExpiryDate <= $expiringSoonCutoff))");
            }
            else if (options.TimeStatus.HasValue)
            {
                switch (options.TimeStatus.Value)
                {
                    case GuaranteeTimeStatus.Active:
                        sql.Append(" AND ExpiryDate > $expiringSoonCutoff");
                        break;
                    case GuaranteeTimeStatus.ExpiringSoon:
                        sql.Append(" AND ExpiryDate >= $todayDate AND ExpiryDate <= $expiringSoonCutoff");
                        break;
                    case GuaranteeTimeStatus.Expired:
                        sql.Append(" AND ExpiryDate < $todayDate");
                        break;
                }
            }

            if (sql.ToString().Contains("$todayDate", StringComparison.Ordinal))
            {
                command.Parameters.AddWithValue("$todayDate", today);
            }

            if (sql.ToString().Contains("$expiringSoonCutoff", StringComparison.Ordinal))
            {
                command.Parameters.AddWithValue("$expiringSoonCutoff", expiringSoonCutoff);
            }
        }

        private static void LoadAttachmentsForGuarantees(IReadOnlyList<Guarantee> guarantees, SqliteConnection connection, SqliteTransaction? transaction = null)
        {
            if (guarantees.Count == 0)
            {
                return;
            }

            Dictionary<int, List<AttachmentRecord>> attachmentMap = new();
            SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;

            List<string> parameterNames = new();
            for (int i = 0; i < guarantees.Count; i++)
            {
                string parameterName = $"$gid{i}";
                parameterNames.Add(parameterName);
                command.Parameters.AddWithValue(parameterName, guarantees[i].Id);
                attachmentMap[guarantees[i].Id] = new List<AttachmentRecord>();
            }

            command.CommandText = $@"
                SELECT Id, GuaranteeId, OriginalFileName, SavedFileName, FileExtension, UploadedAt, DocumentType, TimelineEventKey
                FROM Attachments
                WHERE GuaranteeId IN ({string.Join(", ", parameterNames)})
                ORDER BY UploadedAt ASC, Id ASC";

            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                AttachmentRecord attachment = GuaranteeDataAccess.MapAttachment(reader);
                if (attachmentMap.TryGetValue(attachment.GuaranteeId, out List<AttachmentRecord>? bucket))
                {
                    bucket.Add(attachment);
                }
            }

            foreach (Guarantee guarantee in guarantees)
            {
                guarantee.Attachments = attachmentMap.TryGetValue(guarantee.Id, out List<AttachmentRecord>? attachments)
                    ? attachments
                    : new List<AttachmentRecord>();
            }
        }

        private static IReadOnlyList<AttachmentRecord> BuildSeriesAttachments(IReadOnlyList<AttachmentRecord> attachments)
        {
            return attachments
                .GroupBy(
                    attachment => string.IsNullOrWhiteSpace(attachment.SavedFileName)
                        ? $"attachment:{attachment.Id.ToString(CultureInfo.InvariantCulture)}"
                        : attachment.SavedFileName,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(attachment => attachment.UploadedAt)
                    .First())
                .OrderByDescending(attachment => attachment.UploadedAt)
                .ToList();
        }
    }
}
