using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    internal static class WorkflowExecutionDataAccess
    {
        public static WorkflowExecutionContext LoadContext(
            int requestId,
            RequestType expectedType,
            string typeMismatchMessage,
            string nonPendingMessage,
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            GuaranteeDataAccess.NormalizeGuaranteeRoots(connection, transaction);

            WorkflowRequest request = GetWorkflowRequestById(requestId, connection, transaction)
                ?? throw new InvalidOperationException("الطلب المطلوب غير موجود.");

            if (request.Type != expectedType)
            {
                throw new InvalidOperationException(typeMismatchMessage);
            }

            if (request.Status != RequestStatus.Pending)
            {
                throw new InvalidOperationException(nonPendingMessage);
            }

            Guarantee currentGuarantee = GetCurrentGuaranteeByRootId(request.RootGuaranteeId, connection, transaction)
                ?? throw new InvalidOperationException("تعذر تحميل النسخة الحالية للضمان.");
            Guarantee baseGuarantee = GetGuaranteeById(request.BaseVersionId, connection, transaction)
                ?? throw new InvalidOperationException("تعذر تحميل النسخة المرجعية التي أُنشئ عليها الطلب.");

            ValidateRequestExecutionState(request, baseGuarantee, currentGuarantee);
            return new WorkflowExecutionContext(request, baseGuarantee, currentGuarantee);
        }

        public static int GetNextVersionNumber(int rootId, SqliteConnection connection, SqliteTransaction transaction)
        {
            var nextVersionCommand = connection.CreateCommand();
            nextVersionCommand.Transaction = transaction;
            nextVersionCommand.CommandText = "SELECT IFNULL(MAX(VersionNumber), 0) + 1 FROM Guarantees WHERE COALESCE(RootId, Id) = $rid";
            nextVersionCommand.Parameters.AddWithValue("$rid", rootId);
            return Convert.ToInt32(nextVersionCommand.ExecuteScalar());
        }

        public static void ClearCurrentGuaranteeFlag(int rootId, SqliteConnection connection, SqliteTransaction transaction)
        {
            var resetCommand = connection.CreateCommand();
            resetCommand.Transaction = transaction;
            resetCommand.CommandText = "UPDATE Guarantees SET IsCurrent = 0 WHERE COALESCE(RootId, Id) = $rid";
            resetCommand.Parameters.AddWithValue("$rid", rootId);
            resetCommand.ExecuteNonQuery();
        }

        public static int InsertGuaranteeVersion(
            Guarantee sourceGuarantee,
            int rootId,
            int versionNumber,
            decimal amount,
            DateTime expiryDate,
            string notes,
            GuaranteeLifecycleStatus lifecycleStatus,
            int? replacesRootId,
            int? replacedByRootId,
            DateTime createdAt,
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            var insertGuaranteeCommand = connection.CreateCommand();
            insertGuaranteeCommand.Transaction = transaction;
            insertGuaranteeCommand.CommandText = @"
                INSERT INTO Guarantees (
                    Supplier, Bank, GuaranteeNo, Amount, ExpiryDate, GuaranteeType, Beneficiary, Notes,
                    CreatedAt, RootId, VersionNumber, IsCurrent, ReferenceType, ReferenceNumber, LifecycleStatus, ReplacesRootId, ReplacedByRootId
                )
                VALUES (
                    $supplier, $bank, $guaranteeNo, $amount, $expiryDate, $guaranteeType, $beneficiary, $notes,
                    $createdAt, $rootId, $versionNumber, 1, $referenceType, $referenceNumber, $lifecycleStatus, $replacesRootId, $replacedByRootId
                );
                SELECT last_insert_rowid();";
            insertGuaranteeCommand.Parameters.AddWithValue("$supplier", sourceGuarantee.Supplier);
            insertGuaranteeCommand.Parameters.AddWithValue("$bank", sourceGuarantee.Bank);
            insertGuaranteeCommand.Parameters.AddWithValue("$guaranteeNo", sourceGuarantee.GuaranteeNo);
            insertGuaranteeCommand.Parameters.AddWithValue("$amount", amount);
            insertGuaranteeCommand.Parameters.AddWithValue("$expiryDate", PersistedDateTime.FormatDate(expiryDate));
            insertGuaranteeCommand.Parameters.AddWithValue("$guaranteeType", sourceGuarantee.GuaranteeType);
            insertGuaranteeCommand.Parameters.AddWithValue("$beneficiary", sourceGuarantee.Beneficiary ?? string.Empty);
            insertGuaranteeCommand.Parameters.AddWithValue("$notes", notes);
            insertGuaranteeCommand.Parameters.AddWithValue("$createdAt", PersistedDateTime.FormatDateTime(createdAt));
            insertGuaranteeCommand.Parameters.AddWithValue("$rootId", rootId);
            insertGuaranteeCommand.Parameters.AddWithValue("$versionNumber", versionNumber);
            insertGuaranteeCommand.Parameters.AddWithValue("$referenceType", sourceGuarantee.ReferenceType.ToString());
            insertGuaranteeCommand.Parameters.AddWithValue("$referenceNumber", sourceGuarantee.ReferenceNumber ?? string.Empty);
            insertGuaranteeCommand.Parameters.AddWithValue("$lifecycleStatus", lifecycleStatus.ToString());
            insertGuaranteeCommand.Parameters.AddWithValue("$replacesRootId", (object?)replacesRootId ?? DBNull.Value);
            insertGuaranteeCommand.Parameters.AddWithValue("$replacedByRootId", (object?)replacedByRootId ?? DBNull.Value);
            return Convert.ToInt32(insertGuaranteeCommand.ExecuteScalar());
        }

        public static int InsertStandaloneGuarantee(
            string supplier,
            string bank,
            string guaranteeNo,
            decimal amount,
            DateTime expiryDate,
            string guaranteeType,
            string beneficiary,
            string notes,
            GuaranteeReferenceType referenceType,
            string referenceNumber,
            GuaranteeLifecycleStatus lifecycleStatus,
            int? replacesRootId,
            DateTime createdAt,
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            var insertNewGuaranteeCommand = connection.CreateCommand();
            insertNewGuaranteeCommand.Transaction = transaction;
            insertNewGuaranteeCommand.CommandText = @"
                INSERT INTO Guarantees (
                    Supplier, Bank, GuaranteeNo, Amount, ExpiryDate, GuaranteeType, Beneficiary, Notes,
                    CreatedAt, RootId, VersionNumber, IsCurrent, ReferenceType, ReferenceNumber, LifecycleStatus, ReplacesRootId, ReplacedByRootId
                )
                VALUES (
                    $supplier, $bank, $guaranteeNo, $amount, $expiryDate, $guaranteeType, $beneficiary, $notes,
                    $createdAt, NULL, 1, 1, $referenceType, $referenceNumber, $lifecycleStatus, $replacesRootId, NULL
                );
                SELECT last_insert_rowid();";
            insertNewGuaranteeCommand.Parameters.AddWithValue("$supplier", supplier);
            insertNewGuaranteeCommand.Parameters.AddWithValue("$bank", bank);
            insertNewGuaranteeCommand.Parameters.AddWithValue("$guaranteeNo", guaranteeNo);
            insertNewGuaranteeCommand.Parameters.AddWithValue("$amount", amount);
            insertNewGuaranteeCommand.Parameters.AddWithValue("$expiryDate", PersistedDateTime.FormatDate(expiryDate));
            insertNewGuaranteeCommand.Parameters.AddWithValue("$guaranteeType", guaranteeType);
            insertNewGuaranteeCommand.Parameters.AddWithValue("$beneficiary", beneficiary ?? string.Empty);
            insertNewGuaranteeCommand.Parameters.AddWithValue("$notes", notes);
            insertNewGuaranteeCommand.Parameters.AddWithValue("$createdAt", PersistedDateTime.FormatDateTime(createdAt));
            insertNewGuaranteeCommand.Parameters.AddWithValue("$referenceType", referenceType.ToString());
            insertNewGuaranteeCommand.Parameters.AddWithValue("$referenceNumber", referenceNumber ?? string.Empty);
            insertNewGuaranteeCommand.Parameters.AddWithValue("$lifecycleStatus", lifecycleStatus.ToString());
            insertNewGuaranteeCommand.Parameters.AddWithValue("$replacesRootId", (object?)replacesRootId ?? DBNull.Value);
            return Convert.ToInt32(insertNewGuaranteeCommand.ExecuteScalar());
        }

        public static void SetGuaranteeRootIdToSelf(int guaranteeId, SqliteConnection connection, SqliteTransaction transaction)
        {
            var fixNewGuaranteeRootCommand = connection.CreateCommand();
            fixNewGuaranteeRootCommand.Transaction = transaction;
            fixNewGuaranteeRootCommand.CommandText = "UPDATE Guarantees SET RootId = Id WHERE Id = $id";
            fixNewGuaranteeRootCommand.Parameters.AddWithValue("$id", guaranteeId);
            fixNewGuaranteeRootCommand.ExecuteNonQuery();
        }

        public static void InsertAttachmentRecord(
            int guaranteeId,
            string originalFileName,
            string savedFileName,
            string fileExtension,
            DateTime uploadedAt,
            SqliteConnection connection,
            SqliteTransaction transaction,
            AttachmentDocumentType documentType = AttachmentDocumentType.SupportingDocument)
        {
            var insertAttachmentCommand = connection.CreateCommand();
            insertAttachmentCommand.Transaction = transaction;
            insertAttachmentCommand.CommandText = @"
                INSERT INTO Attachments (GuaranteeId, OriginalFileName, SavedFileName, FileExtension, UploadedAt, DocumentType)
                VALUES ($guaranteeId, $originalFileName, $savedFileName, $fileExtension, $uploadedAt, $documentType)";
            insertAttachmentCommand.Parameters.AddWithValue("$guaranteeId", guaranteeId);
            insertAttachmentCommand.Parameters.AddWithValue("$originalFileName", originalFileName);
            insertAttachmentCommand.Parameters.AddWithValue("$savedFileName", savedFileName);
            insertAttachmentCommand.Parameters.AddWithValue("$fileExtension", fileExtension);
            insertAttachmentCommand.Parameters.AddWithValue("$uploadedAt", PersistedDateTime.FormatDateTime(uploadedAt));
            insertAttachmentCommand.Parameters.AddWithValue("$documentType", documentType.ToString());
            insertAttachmentCommand.ExecuteNonQuery();
        }

        public static void UpdateGuaranteeLifecycleStatus(
            int guaranteeId,
            GuaranteeLifecycleStatus lifecycleStatus,
            string notes,
            int? replacedByRootId,
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            var updateGuaranteeCommand = connection.CreateCommand();
            updateGuaranteeCommand.Transaction = transaction;
            updateGuaranteeCommand.CommandText = @"
                UPDATE Guarantees SET
                    LifecycleStatus = $lifecycleStatus,
                    Notes = $notes,
                    ReplacedByRootId = $replacedByRootId
                WHERE Id = $guaranteeId";
            updateGuaranteeCommand.Parameters.AddWithValue("$lifecycleStatus", lifecycleStatus.ToString());
            updateGuaranteeCommand.Parameters.AddWithValue("$notes", notes ?? string.Empty);
            updateGuaranteeCommand.Parameters.AddWithValue("$replacedByRootId", (object?)replacedByRootId ?? DBNull.Value);
            updateGuaranteeCommand.Parameters.AddWithValue("$guaranteeId", guaranteeId);
            updateGuaranteeCommand.ExecuteNonQuery();
        }

        public static void CopyAttachments(
            IEnumerable<AttachmentRecord> attachments,
            int guaranteeId,
            DateTime uploadedAt,
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            foreach (AttachmentRecord attachment in attachments)
            {
                InsertAttachmentRecord(
                    guaranteeId,
                    attachment.OriginalFileName,
                    attachment.SavedFileName,
                    attachment.FileExtension,
                    uploadedAt,
                    connection,
                    transaction,
                    attachment.DocumentType);
            }
        }

        public static void UpdateWorkflowRequestAsExecuted(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            int? resultVersionId,
            DateTime updatedAt,
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            var updateRequestCommand = connection.CreateCommand();
            updateRequestCommand.Transaction = transaction;
            updateRequestCommand.CommandText = @"
                UPDATE WorkflowRequests SET
                    RequestStatus = $status,
                    UpdatedAt = $now,
                    ResponseRecordedAt = $now,
                    ResultVersionId = $resultVersionId,
                    ResponseNotes = $responseNotes,
                    ResponseOriginalFileName = $responseOriginalFileName,
                    ResponseSavedFileName = $responseSavedFileName
                WHERE Id = $requestId";
            updateRequestCommand.Parameters.AddWithValue("$status", RequestStatus.Executed.ToString());
            updateRequestCommand.Parameters.AddWithValue("$now", PersistedDateTime.FormatDateTime(updatedAt));
            updateRequestCommand.Parameters.AddWithValue("$resultVersionId", (object?)resultVersionId ?? DBNull.Value);
            updateRequestCommand.Parameters.AddWithValue("$responseNotes", responseNotes ?? string.Empty);
            updateRequestCommand.Parameters.AddWithValue("$responseOriginalFileName", responseOriginalFileName ?? string.Empty);
            updateRequestCommand.Parameters.AddWithValue("$responseSavedFileName", responseSavedFileName ?? string.Empty);
            updateRequestCommand.Parameters.AddWithValue("$requestId", requestId);
            updateRequestCommand.ExecuteNonQuery();
        }

        public static void SupersedePendingRequests(
            int rootId,
            int requestId,
            string cancelNote,
            DateTime updatedAt,
            SqliteConnection connection,
            SqliteTransaction transaction)
        {
            var cancelOthersCommand = connection.CreateCommand();
            cancelOthersCommand.Transaction = transaction;
            cancelOthersCommand.CommandText = @"
                UPDATE WorkflowRequests SET
                    RequestStatus = $status,
                    UpdatedAt = $now,
                    ResponseRecordedAt = $now,
                    ResponseNotes = CASE
                        WHEN TRIM(IFNULL(ResponseNotes, '')) = '' THEN $cancelNote
                        ELSE ResponseNotes || CHAR(10) || $cancelNote
                    END
                WHERE RootId = $rootId
                  AND Id <> $requestId
                  AND RequestStatus = $pendingStatus";
            cancelOthersCommand.Parameters.AddWithValue("$status", RequestStatus.Superseded.ToString());
            cancelOthersCommand.Parameters.AddWithValue("$pendingStatus", RequestStatus.Pending.ToString());
            cancelOthersCommand.Parameters.AddWithValue("$now", PersistedDateTime.FormatDateTime(updatedAt));
            cancelOthersCommand.Parameters.AddWithValue("$cancelNote", cancelNote);
            cancelOthersCommand.Parameters.AddWithValue("$rootId", rootId);
            cancelOthersCommand.Parameters.AddWithValue("$requestId", requestId);
            cancelOthersCommand.ExecuteNonQuery();
        }

        public static Guarantee? GetCurrentGuaranteeByNo(string guaranteeNo, SqliteConnection connection, SqliteTransaction transaction)
        {
            string normalizedGuaranteeNo = GuaranteeDataAccess.NormalizeGuaranteeNo(guaranteeNo) ?? string.Empty;

            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $@"
                SELECT {GuaranteeDataAccess.SelectColumns}
                FROM Guarantees
                WHERE {GuaranteeDataAccess.NormalizedGuaranteeNoSqlExpression} = $no
                  AND IsCurrent = 1
                LIMIT 1";
            cmd.Parameters.AddWithValue("$no", normalizedGuaranteeNo);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            Guarantee guarantee = GuaranteeDataAccess.MapGuarantee(reader);
            guarantee.Attachments = GuaranteeDataAccess.GetAttachmentsForGuarantee(guarantee.Id, connection, transaction);
            return guarantee;
        }

        public static string AppendNote(string existingNotes, string note)
        {
            if (string.IsNullOrWhiteSpace(existingNotes))
            {
                return note;
            }

            return $"{existingNotes}{Environment.NewLine}{Environment.NewLine}{note}";
        }

        public static string DetectReductionMismatch(WorkflowRequest request, decimal? executedAmount)
        {
            if (request.Type != RequestType.Reduction || executedAmount == null)
            {
                return string.Empty;
            }

            decimal? requestedAmount = request.RequestedAmount;
            if (requestedAmount == null)
            {
                return string.Empty;
            }

            const decimal TolerancePercent = 0.01m;
            decimal tolerance = requestedAmount.Value * TolerancePercent;

            if (Math.Abs(executedAmount.Value - requestedAmount.Value) > tolerance)
            {
                string note = $"[تنبيه مطابقة] المبلغ المنفذ ({executedAmount.Value:N2}) يختلف عن المبلغ المطلوب ({requestedAmount.Value:N2}).";
                SimpleLogger.Log($"ReductionMismatch: RequestId={request.Id}, Requested={requestedAmount.Value:N2}, Executed={executedAmount.Value:N2}", "WARN");
                return note;
            }

            return string.Empty;
        }

        private static WorkflowRequest? GetWorkflowRequestById(int requestId, SqliteConnection connection, SqliteTransaction transaction)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = $"SELECT {WorkflowRequestDataAccess.SelectColumns} FROM WorkflowRequests WHERE Id = $id LIMIT 1";
            cmd.Parameters.AddWithValue("$id", requestId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            return WorkflowRequestDataAccess.MapWorkflowRequest(reader);
        }

        private static Guarantee? GetGuaranteeById(int guaranteeId, SqliteConnection connection, SqliteTransaction transaction)
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

            Guarantee guarantee = GuaranteeDataAccess.MapGuarantee(reader);
            guarantee.Attachments = GuaranteeDataAccess.GetAttachmentsForGuarantee(guarantee.Id, connection, transaction);
            return guarantee;
        }

        private static Guarantee? GetCurrentGuaranteeByRootId(int rootId, SqliteConnection connection, SqliteTransaction transaction)
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

            Guarantee guarantee = GuaranteeDataAccess.MapGuarantee(reader);
            guarantee.Attachments = GuaranteeDataAccess.GetAttachmentsForGuarantee(guarantee.Id, connection, transaction);
            return guarantee;
        }

        private static void ValidateRequestExecutionState(WorkflowRequest request, Guarantee baseGuarantee, Guarantee currentGuarantee)
        {
            if (request.Type is RequestType.Extension or RequestType.Reduction or RequestType.Release or RequestType.Liquidation or RequestType.Verification or RequestType.Replacement)
            {
                if (currentGuarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
                {
                    throw new InvalidOperationException($"لا يمكن تنفيذ {request.TypeLabel} لأن حالة الضمان الحالية هي {currentGuarantee.LifecycleStatusLabel}.");
                }
            }

            if (currentGuarantee.Id == request.BaseVersionId)
            {
                return;
            }

            switch (request.Type)
            {
                case RequestType.Extension:
                    if (currentGuarantee.ExpiryDate.Date != baseGuarantee.ExpiryDate.Date)
                    {
                        throw new InvalidOperationException("لا يمكن تنفيذ طلب التمديد لأن تاريخ الانتهاء الحالي تغيّر منذ إنشاء الطلب. راجع السجل وأنشئ طلبًا جديدًا عند الحاجة.");
                    }

                    if (currentGuarantee.LifecycleStatus != baseGuarantee.LifecycleStatus)
                    {
                        throw new InvalidOperationException("لا يمكن تنفيذ طلب التمديد لأن الحالة التشغيلية للضمان تغيّرت منذ إنشاء الطلب.");
                    }
                    break;

                case RequestType.Reduction:
                    if (currentGuarantee.Amount != baseGuarantee.Amount)
                    {
                        throw new InvalidOperationException("لا يمكن تنفيذ طلب التخفيض لأن مبلغ الضمان الحالي تغيّر منذ إنشاء الطلب. راجع السجل وأنشئ طلبًا جديدًا عند الحاجة.");
                    }

                    if (currentGuarantee.LifecycleStatus != baseGuarantee.LifecycleStatus)
                    {
                        throw new InvalidOperationException("لا يمكن تنفيذ طلب التخفيض لأن الحالة التشغيلية للضمان تغيّرت منذ إنشاء الطلب.");
                    }
                    break;

                case RequestType.Release:
                    if (currentGuarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
                    {
                        throw new InvalidOperationException("لا يمكن تنفيذ طلب الإفراج لأن الضمان لم يعد في حالة نشطة.");
                    }
                    break;

                case RequestType.Liquidation:
                    if (currentGuarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
                    {
                        throw new InvalidOperationException("لا يمكن تنفيذ طلب التسييل لأن الضمان لم يعد في حالة نشطة.");
                    }
                    break;

                case RequestType.Verification:
                    if (currentGuarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
                    {
                        throw new InvalidOperationException("لا يمكن تنفيذ طلب التحقق لأن الضمان لم يعد في حالة نشطة.");
                    }
                    break;

                case RequestType.Replacement:
                    if (currentGuarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
                    {
                        throw new InvalidOperationException("لا يمكن تنفيذ طلب الاستبدال لأن الضمان لم يعد في حالة نشطة.");
                    }
                    break;

                default:
                    throw new InvalidOperationException("لا يمكن تنفيذ هذا الطلب لأن السجل الحالي تغيّر منذ إنشاء الطلب.");
            }
        }
    }
}
