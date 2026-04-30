using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    internal class WorkflowRequestRepository
    {
        private readonly string _connectionString;

        public WorkflowRequestRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public int SaveWorkflowRequest(WorkflowRequest req)
        {
            using var scope = SimpleLogger.BeginScope("WorkflowRequestRepository.SaveWorkflowRequest");
            using var connection = SqliteConnectionFactory.Open(_connectionString);
            using var transaction = connection.BeginTransaction();

            try
            {
                // Reserve the next sequence number and insert in the same transaction.
                req.SequenceNumber = GetNextSequenceNumber(req.RootGuaranteeId, connection, transaction);

                var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO WorkflowRequests (
                        RootId, SequenceNumber, BaseVersionId, ResultVersionId, RequestType, RequestStatus,
                        RequestDate, CreatedAt, UpdatedAt, ResponseRecordedAt, RequestedDataJson,
                        LetterOriginalFileName, LetterSavedFileName, ResponseOriginalFileName, ResponseSavedFileName,
                        ResponseNotes, Notes, CreatedBy
                    )
                    VALUES (
                        $rid, $seq, $base, $result, $type, $status,
                        $requestDate, $createdAt, $updatedAt, $responseDate, $data,
                        $letterOriginal, $letterSaved, $responseOriginal, $responseSaved,
                        $responseNotes, $notes, $createdBy
                    );
                    SELECT last_insert_rowid();";

                cmd.Parameters.AddWithValue("$rid", req.RootGuaranteeId);
                cmd.Parameters.AddWithValue("$seq", req.SequenceNumber);
                cmd.Parameters.AddWithValue("$base", req.BaseVersionId);
                cmd.Parameters.AddWithValue("$result", (object?)req.ResultVersionId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$type", req.Type.ToString());
                cmd.Parameters.AddWithValue("$status", req.Status.ToString());
                cmd.Parameters.AddWithValue("$requestDate", PersistedDateTime.FormatDateTime(req.RequestDate));
                cmd.Parameters.AddWithValue("$createdAt", PersistedDateTime.FormatDateTime(req.CreatedAt));
                cmd.Parameters.AddWithValue("$updatedAt", PersistedDateTime.FormatDateTime(req.UpdatedAt));
                cmd.Parameters.AddWithValue("$responseDate", req.ResponseRecordedAt.HasValue ? PersistedDateTime.FormatDateTime(req.ResponseRecordedAt.Value) : DBNull.Value);
                cmd.Parameters.AddWithValue("$data", req.RequestedDataJson ?? string.Empty);
                cmd.Parameters.AddWithValue("$letterOriginal", req.LetterOriginalFileName ?? string.Empty);
                cmd.Parameters.AddWithValue("$letterSaved", req.LetterSavedFileName ?? string.Empty);
                cmd.Parameters.AddWithValue("$responseOriginal", req.ResponseOriginalFileName ?? string.Empty);
                cmd.Parameters.AddWithValue("$responseSaved", req.ResponseSavedFileName ?? string.Empty);
                cmd.Parameters.AddWithValue("$responseNotes", req.ResponseNotes ?? string.Empty);
                cmd.Parameters.AddWithValue("$notes", req.Notes ?? string.Empty);
                cmd.Parameters.AddWithValue("$createdBy", WorkflowCreatedByPolicy.NormalizeForNewRequest(req.CreatedBy));

                long requestId = Convert.ToInt64(cmd.ExecuteScalar());
                transaction.Commit();
                return Convert.ToInt32(requestId);
            }
            catch (Exception ex)
            {
                try
                {
                    transaction.Rollback();
                }
                catch
                {
                    // Keep the original failure as the one that surfaces to callers.
                }

                throw OperationFailure.LogAndWrap(
                    ex,
                    "WorkflowRequestRepository.SaveWorkflowRequest",
                    "تعذر حفظ طلب الإجراء الحالي.");
            }
        }

        public bool HasPendingWorkflowRequest(int rootId, RequestType requestType)
        {
            using var scope = SimpleLogger.BeginScope("WorkflowRequestRepository.HasPendingWorkflowRequest");
            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    SELECT COUNT(*)
                    FROM WorkflowRequests
                    WHERE RootId = $rid
                      AND RequestType = $type
                      AND RequestStatus = $status";
                cmd.Parameters.AddWithValue("$rid", rootId);
                cmd.Parameters.AddWithValue("$type", requestType.ToString());
                cmd.Parameters.AddWithValue("$status", RequestStatus.Pending.ToString());
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "WorkflowRequestRepository.HasPendingWorkflowRequest",
                    "تعذر التحقق من وجود طلبات معلقة لهذا الضمان.");
            }
        }

        public int GetPendingWorkflowRequestCount()
        {
            using var scope = SimpleLogger.BeginScope("WorkflowRequestRepository.GetPendingWorkflowRequestCount");
            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM WorkflowRequests WHERE RequestStatus = $status";
                cmd.Parameters.AddWithValue("$status", RequestStatus.Pending.ToString());
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "WorkflowRequestRepository.GetPendingWorkflowRequestCount",
                    "تعذر احتساب الطلبات المعلقة الحالية.");
            }
        }

        public WorkflowRequest? GetWorkflowRequestById(int requestId)
        {
            using var scope = SimpleLogger.BeginScope("WorkflowRequestRepository.GetWorkflowRequestById");
            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                return GetWorkflowRequestById(requestId, connection);
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "WorkflowRequestRepository.GetWorkflowRequestById",
                    "تعذر تحميل الطلب المطلوب.");
            }
        }

        public List<WorkflowRequest> GetWorkflowRequestsByRootId(int rootId)
        {
            using var scope = SimpleLogger.BeginScope("WorkflowRequestRepository.GetWorkflowRequestsByRootId");
            try
            {
                var list = new List<WorkflowRequest>();
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                var cmd = connection.CreateCommand();
                cmd.CommandText = $@"
                    SELECT {WorkflowRequestDataAccess.SelectColumns}
                    FROM WorkflowRequests
                    WHERE RootId = $rid
                    ORDER BY SequenceNumber DESC, RequestDate DESC";
                cmd.Parameters.AddWithValue("$rid", rootId);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(WorkflowRequestDataAccess.MapWorkflowRequest(reader));
                }

                return list;
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "WorkflowRequestRepository.GetWorkflowRequestsByRootId",
                    "تعذر تحميل تسلسل الطلبات المرتبط بالضمان المطلوب.");
            }
        }

        public List<WorkflowRequestListItem> QueryWorkflowRequests(WorkflowRequestQueryOptions options)
        {
            using var scope = SimpleLogger.BeginScope("WorkflowRequestRepository.QueryWorkflowRequests");
            WorkflowRequestQueryOptions effectiveOptions = options ?? new WorkflowRequestQueryOptions();
            List<WorkflowRequestListItem> list = new();

            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                using SqliteCommand cmd = BuildWorkflowRequestQueryCommand(connection, effectiveOptions, countOnly: false);
                using SqliteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(MapWorkflowRequestListItem(reader));
                }
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "WorkflowRequestRepository.QueryWorkflowRequests",
                    "تعذر تحميل قائمة الطلبات الحالية.");
            }

            return list;
        }

        public int CountWorkflowRequests(WorkflowRequestQueryOptions? options = null)
        {
            using var scope = SimpleLogger.BeginScope("WorkflowRequestRepository.CountWorkflowRequests");
            WorkflowRequestQueryOptions effectiveOptions = options ?? new WorkflowRequestQueryOptions();

            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                using SqliteCommand cmd = BuildWorkflowRequestQueryCommand(connection, effectiveOptions, countOnly: true);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "WorkflowRequestRepository.CountWorkflowRequests",
                    "تعذر احتساب الطلبات الحالية.");
            }
        }

        public List<WorkflowRequestListItem> SearchWorkflowRequests(string query)
        {
            return QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                SearchText = query ?? string.Empty,
                SortMode = WorkflowRequestQuerySortMode.RequestDateDescending
            });
        }

        public void RecordWorkflowResponse(
            int requestId,
            RequestStatus newStatus,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            int? resultVersionId = null)
        {
            using var scope = SimpleLogger.BeginScope("WorkflowRequestRepository.RecordWorkflowResponse");
            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    UPDATE WorkflowRequests SET
                        RequestStatus = $status,
                        UpdatedAt = $now,
                        ResponseRecordedAt = $now,
                        ResultVersionId = $resultVersionId,
                        ResponseNotes = $responseNotes,
                        ResponseOriginalFileName = $responseOriginalFileName,
                        ResponseSavedFileName = $responseSavedFileName
                    WHERE Id = $id";

                cmd.Parameters.AddWithValue("$status", newStatus.ToString());
                cmd.Parameters.AddWithValue("$now", PersistedDateTime.FormatDateTime(DateTime.Now));
                cmd.Parameters.AddWithValue("$resultVersionId", (object?)resultVersionId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$responseNotes", responseNotes ?? string.Empty);
                cmd.Parameters.AddWithValue("$responseOriginalFileName", responseOriginalFileName ?? string.Empty);
                cmd.Parameters.AddWithValue("$responseSavedFileName", responseSavedFileName ?? string.Empty);
                cmd.Parameters.AddWithValue("$id", requestId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "WorkflowRequestRepository.RecordWorkflowResponse",
                    "تعذر تحديث حالة الطلب الحالية.");
            }
        }

        public void AttachWorkflowResponseDocument(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName)
        {
            using var scope = SimpleLogger.BeginScope("WorkflowRequestRepository.AttachWorkflowResponseDocument");
            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    UPDATE WorkflowRequests SET
                        UpdatedAt = $now,
                        ResponseNotes = $responseNotes,
                        ResponseOriginalFileName = $responseOriginalFileName,
                        ResponseSavedFileName = $responseSavedFileName
                    WHERE Id = $id";

                cmd.Parameters.AddWithValue("$now", PersistedDateTime.FormatDateTime(DateTime.Now));
                cmd.Parameters.AddWithValue("$responseNotes", responseNotes ?? string.Empty);
                cmd.Parameters.AddWithValue("$responseOriginalFileName", responseOriginalFileName ?? string.Empty);
                cmd.Parameters.AddWithValue("$responseSavedFileName", responseSavedFileName ?? string.Empty);
                cmd.Parameters.AddWithValue("$id", requestId);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "WorkflowRequestRepository.AttachWorkflowResponseDocument",
                    "تعذر تحديث مستند رد البنك لهذا الطلب.");
            }
        }

        private int GetNextSequenceNumber(int rootId, SqliteConnection connection, SqliteTransaction? transaction = null)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "SELECT COALESCE(MAX(SequenceNumber), 0) + 1 FROM WorkflowRequests WHERE RootId = $rid";
            cmd.Parameters.AddWithValue("$rid", rootId);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private WorkflowRequest? GetWorkflowRequestById(int requestId, SqliteConnection connection, SqliteTransaction? transaction = null)
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

        private static SqliteCommand BuildWorkflowRequestQueryCommand(SqliteConnection connection, WorkflowRequestQueryOptions options, bool countOnly)
        {
            StringBuilder sql = new();
            if (countOnly)
            {
                sql.Append(@"
                    SELECT COUNT(*)
                    FROM WorkflowRequests wr
                    INNER JOIN Guarantees currentG
                        ON currentG.RootId = wr.RootId AND currentG.IsCurrent = 1");
            }
            else
            {
                sql.Append(@"
                    SELECT 
                        wr.Id,
                        wr.RootId,
                        wr.SequenceNumber,
                        wr.BaseVersionId,
                        wr.ResultVersionId,
                        wr.RequestType,
                        wr.RequestStatus,
                        wr.RequestDate,
                        wr.CreatedAt,
                        wr.UpdatedAt,
                        wr.ResponseRecordedAt,
                        wr.RequestedDataJson,
                        wr.LetterOriginalFileName,
                        wr.LetterSavedFileName,
                        wr.ResponseOriginalFileName,
                        wr.ResponseSavedFileName,
                        wr.ResponseNotes,
                        wr.Notes,
                        wr.CreatedBy,
                        currentG.Id,
                        currentG.GuaranteeNo,
                        currentG.Supplier,
                        currentG.Bank,
                        currentG.ReferenceType,
                        currentG.ReferenceNumber,
                        currentG.Amount,
                        currentG.ExpiryDate,
                        currentG.VersionNumber,
                        currentG.LifecycleStatus,
                        baseG.VersionNumber,
                        resultG.VersionNumber
                    FROM WorkflowRequests wr
                    INNER JOIN Guarantees currentG
                        ON currentG.RootId = wr.RootId AND currentG.IsCurrent = 1
                    LEFT JOIN Guarantees baseG
                        ON baseG.Id = wr.BaseVersionId
                    LEFT JOIN Guarantees resultG
                        ON resultG.Id = wr.ResultVersionId");
            }

            SqliteCommand command = connection.CreateCommand();
            AppendWorkflowRequestFilters(sql, command, options);

            if (!countOnly)
            {
                sql.AppendLine();
                sql.Append("ORDER BY ");
                switch (options.SortMode)
                {
                    case WorkflowRequestQuerySortMode.RequestDateAscending:
                        sql.Append("wr.RequestDate ASC, wr.SequenceNumber ASC");
                        break;
                    case WorkflowRequestQuerySortMode.RequestDateDescending:
                        sql.Append("wr.RequestDate DESC, wr.SequenceNumber DESC");
                        break;
                    case WorkflowRequestQuerySortMode.ActivityDateDescending:
                        sql.Append("COALESCE(wr.ResponseRecordedAt, wr.RequestDate) DESC, wr.SequenceNumber DESC");
                        break;
                    default:
                        sql.Append(@"
                            CASE wr.RequestStatus
                                WHEN 'Pending' THEN 0
                                WHEN 'Executed' THEN 1
                                WHEN 'Rejected' THEN 2
                                ELSE 3
                            END,
                            wr.RequestDate DESC,
                            wr.SequenceNumber DESC");
                        break;
                }

                if (options.Limit.HasValue && options.Limit.Value > 0)
                {
                    sql.Append(" LIMIT $limit");
                    command.Parameters.AddWithValue("$limit", options.Limit.Value);
                }
            }

            command.CommandText = sql.ToString();
            return command;
        }

        private static void AppendWorkflowRequestFilters(StringBuilder sql, SqliteCommand command, WorkflowRequestQueryOptions options)
        {
            string normalizedSearch = options.SearchText?.Trim().ToLowerInvariant() ?? string.Empty;
            string normalizedBank = options.Bank?.Trim().ToLowerInvariant() ?? string.Empty;
            string normalizedSupplier = options.Supplier?.Trim().ToLowerInvariant() ?? string.Empty;
            string normalizedCreatedBy = options.CreatedBy?.Trim().ToLowerInvariant() ?? string.Empty;

            sql.AppendLine();
            sql.Append("WHERE 1 = 1");

            if (options.RootGuaranteeId.HasValue)
            {
                sql.Append(" AND wr.RootId = $rootGuaranteeId");
                command.Parameters.AddWithValue("$rootGuaranteeId", options.RootGuaranteeId.Value);
            }

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                sql.Append(@"
 AND (
        LOWER(IFNULL(currentG.GuaranteeNo, '')) LIKE $search
     OR LOWER(IFNULL(currentG.Supplier, '')) LIKE $search
     OR LOWER(IFNULL(currentG.Bank, '')) LIKE $search
     OR LOWER(IFNULL(wr.RequestType, '')) LIKE $search
     OR LOWER(IFNULL(wr.RequestStatus, '')) LIKE $search
     OR LOWER(IFNULL(wr.CreatedBy, '')) LIKE $search");
                command.Parameters.AddWithValue("$search", $"%{normalizedSearch}%");

                AppendMatchingLabels(sql, command, normalizedSearch, isType: true);
                AppendMatchingLabels(sql, command, normalizedSearch, isType: false);
                sql.Append(")");
            }

            if (options.RequestType.HasValue)
            {
                sql.Append(" AND wr.RequestType = $requestType");
                command.Parameters.AddWithValue("$requestType", options.RequestType.Value.ToString());
            }

            if (options.RequestStatus.HasValue)
            {
                sql.Append(" AND wr.RequestStatus = $requestStatus");
                command.Parameters.AddWithValue("$requestStatus", options.RequestStatus.Value.ToString());
            }

            if (!string.IsNullOrWhiteSpace(normalizedBank))
            {
                sql.Append(" AND LOWER(TRIM(IFNULL(currentG.Bank, ''))) = $bank");
                command.Parameters.AddWithValue("$bank", normalizedBank);
            }

            if (!string.IsNullOrWhiteSpace(normalizedSupplier))
            {
                sql.Append(" AND LOWER(TRIM(IFNULL(currentG.Supplier, ''))) = $supplier");
                command.Parameters.AddWithValue("$supplier", normalizedSupplier);
            }

            if (!string.IsNullOrWhiteSpace(normalizedCreatedBy))
            {
                sql.Append(" AND LOWER(TRIM(IFNULL(wr.CreatedBy, ''))) = $createdBy");
                command.Parameters.AddWithValue("$createdBy", normalizedCreatedBy);
            }

            if (options.ReferenceType.HasValue)
            {
                sql.Append(" AND currentG.ReferenceType = $referenceType");
                command.Parameters.AddWithValue("$referenceType", options.ReferenceType.Value.ToString());
            }

            if (options.RequireReferenceNumber)
            {
                sql.Append(" AND TRIM(IFNULL(currentG.ReferenceNumber, '')) <> ''");
            }

            if (options.PendingOrMissingResponseOnly)
            {
                sql.Append(" AND (wr.RequestStatus = $pendingStatus OR TRIM(IFNULL(wr.ResponseSavedFileName, '')) = '')");
                command.Parameters.AddWithValue("$pendingStatus", RequestStatus.Pending.ToString());
            }

            if (options.RequestDateFrom.HasValue)
            {
                sql.Append(" AND wr.RequestDate >= $requestDateFrom");
                command.Parameters.AddWithValue("$requestDateFrom", PersistedDateTime.FormatDateTime(options.RequestDateFrom.Value));
            }

            if (options.RequestDateTo.HasValue)
            {
                sql.Append(" AND wr.RequestDate < $requestDateToExclusive");
                command.Parameters.AddWithValue("$requestDateToExclusive", PersistedDateTime.FormatDateTime(options.RequestDateTo.Value.Date.AddDays(1)));
            }

            if (options.ResponseRecordedFrom.HasValue)
            {
                sql.Append(" AND wr.ResponseRecordedAt IS NOT NULL AND wr.ResponseRecordedAt >= $responseDateFrom");
                command.Parameters.AddWithValue("$responseDateFrom", PersistedDateTime.FormatDateTime(options.ResponseRecordedFrom.Value));
            }

            if (options.ResponseRecordedTo.HasValue)
            {
                sql.Append(" AND wr.ResponseRecordedAt IS NOT NULL AND wr.ResponseRecordedAt < $responseDateToExclusive");
                command.Parameters.AddWithValue("$responseDateToExclusive", PersistedDateTime.FormatDateTime(options.ResponseRecordedTo.Value.Date.AddDays(1)));
            }
        }

        private static void AppendMatchingLabels(StringBuilder sql, SqliteCommand command, string normalizedSearch, bool isType)
        {
            int index = 0;
            if (isType)
            {
                foreach (RequestType requestType in Enum.GetValues<RequestType>())
                {
                    if (!GetTypeLabel(requestType).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string parameterName = $"$searchType{index++}";
                    sql.Append($" OR wr.RequestType = {parameterName}");
                    command.Parameters.AddWithValue(parameterName, requestType.ToString());
                }

                return;
            }

            foreach (RequestStatus requestStatus in Enum.GetValues<RequestStatus>())
            {
                if (!GetStatusLabel(requestStatus).Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string parameterName = $"$searchStatus{index++}";
                sql.Append($" OR wr.RequestStatus = {parameterName}");
                command.Parameters.AddWithValue(parameterName, requestStatus.ToString());
            }
        }

        private static WorkflowRequestListItem MapWorkflowRequestListItem(SqliteDataReader reader)
        {
            WorkflowRequest request = WorkflowRequestDataAccess.MapWorkflowRequest(reader);
            GuaranteeReferenceType rawReferenceType = reader.IsDBNull(23) ? GuaranteeReferenceType.None : GuaranteeDataAccess.ParseReferenceType(reader.GetString(23));
            string rawReferenceNumber = reader.IsDBNull(24) ? string.Empty : reader.GetString(24);
            (GuaranteeReferenceType referenceType, string referenceNumber) = GuaranteeDataAccess.NormalizeReference(rawReferenceType, rawReferenceNumber);

            return new WorkflowRequestListItem
            {
                Request = request,
                CurrentGuaranteeId = reader.GetInt32(19),
                RootGuaranteeId = request.RootGuaranteeId,
                GuaranteeNo = reader.IsDBNull(20) ? string.Empty : reader.GetString(20),
                Supplier = reader.IsDBNull(21) ? string.Empty : reader.GetString(21),
                Bank = reader.IsDBNull(22) ? string.Empty : reader.GetString(22),
                ReferenceType = referenceType,
                ReferenceNumber = referenceNumber,
                CurrentAmount = reader.IsDBNull(25) ? 0m : reader.GetDecimal(25),
                CurrentExpiryDate = reader.IsDBNull(26) ? DateTime.MinValue : PersistedDateTime.Parse(reader.GetString(26)),
                CurrentVersionNumber = reader.IsDBNull(27) ? 1 : reader.GetInt32(27),
                LifecycleStatus = reader.IsDBNull(28) ? GuaranteeLifecycleStatus.Active : GuaranteeDataAccess.ParseLifecycleStatus(reader.GetString(28)),
                BaseVersionNumber = reader.IsDBNull(29) ? 1 : reader.GetInt32(29),
                ResultVersionNumber = reader.IsDBNull(30) ? null : reader.GetInt32(30)
            };
        }

        private static string GetTypeLabel(RequestType requestType)
        {
            return requestType switch
            {
                RequestType.Extension => "طلب تمديد",
                RequestType.Release => "طلب إفراج",
                RequestType.Liquidation => "طلب تسييل",
                RequestType.Reduction => "طلب تخفيض",
                RequestType.Verification => "طلب تحقق",
                RequestType.Replacement => "طلب استبدال",
                RequestType.Annulment => "طلب قديم ملغى",
                _ => requestType.ToString()
            };
        }

        private static string GetStatusLabel(RequestStatus requestStatus)
        {
            return requestStatus switch
            {
                RequestStatus.Pending => "قيد الانتظار",
                RequestStatus.Executed => "منفذ",
                RequestStatus.Rejected => "مرفوض",
                RequestStatus.Cancelled => "مُلغى",
                RequestStatus.Superseded => "مُسقط آليًا",
                _ => requestStatus.ToString()
            };
        }
    }
}
