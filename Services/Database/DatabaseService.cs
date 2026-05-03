using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    public class DatabaseService : IDatabaseService
    {
        private static readonly object RuntimeInitializationLock = new();
        private static readonly object TimelineEventBackfillLock = new();
        private static bool _runtimeInitialized;
        private static bool _timelineEventsDirty = true;
        private static readonly Dictionary<string, string> SupportedUniqueValueColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Supplier"] = "Supplier",
            ["Bank"] = "Bank",
            ["GuaranteeType"] = "GuaranteeType"
        };

        private readonly string _connectionString;
        private readonly GuaranteeRepository _guaranteeRepository;
        private readonly WorkflowRequestRepository _workflowRequestRepository;
        private readonly WorkflowExecutionProcessor _workflowExecutionProcessor;

        public DatabaseService() : this(new AttachmentStorageService())
        {
        }

        internal DatabaseService(AttachmentStorageService attachmentStorage)
        {
            _connectionString = $"Data Source={AppPaths.DatabasePath}";
            _guaranteeRepository = new GuaranteeRepository(_connectionString, attachmentStorage);
            _workflowRequestRepository = new WorkflowRequestRepository(_connectionString);
            _workflowExecutionProcessor = new WorkflowExecutionProcessor(_connectionString, attachmentStorage);
        }

        public static void InitializeRuntime()
        {
            lock (RuntimeInitializationLock)
            {
                if (_runtimeInitialized)
                {
                    return;
                }

                var initializer = new DatabaseRuntimeInitializer(
                    $"Data Source={AppPaths.DatabasePath}",
                    new AttachmentStorageService(),
                    new WorkflowResponseStorageService());
                initializer.Initialize();
                _runtimeInitialized = true;
                MarkTimelineEventsClean();
            }
        }

        internal static void ResetRuntimeInitializationForTesting()
        {
            lock (RuntimeInitializationLock)
            {
                _runtimeInitialized = false;
                MarkTimelineEventsDirty();
            }
        }

        private static void MarkTimelineEventsDirty()
        {
            lock (TimelineEventBackfillLock)
            {
                _timelineEventsDirty = true;
            }
        }

        private static void MarkTimelineEventsClean()
        {
            lock (TimelineEventBackfillLock)
            {
                _timelineEventsDirty = false;
            }
        }

        private static void EnsureTimelineEventsCurrent(SqliteConnection connection)
        {
            lock (TimelineEventBackfillLock)
            {
                if (!_timelineEventsDirty)
                {
                    return;
                }

                GuaranteeEventStore.BackfillMissingEvents(connection);
                _timelineEventsDirty = false;
            }
        }

        public void SaveGuarantee(Guarantee g, List<string> tempFilePaths)
        {
            _guaranteeRepository.SaveGuarantee(g, tempFilePaths);
            MarkTimelineEventsDirty();
        }

        public void SaveGuaranteeWithAttachments(Guarantee g, List<AttachmentInput> attachments)
        {
            _guaranteeRepository.SaveGuaranteeWithAttachments(g, attachments);
            MarkTimelineEventsDirty();
        }

        public void AddGuaranteeAttachments(int guaranteeId, List<AttachmentInput> attachments)
        {
            _guaranteeRepository.AddAttachments(guaranteeId, attachments);
            MarkTimelineEventsDirty();
        }

        public int UpdateGuarantee(Guarantee g, List<string> newTempFiles, List<AttachmentRecord> removedAttachments)
        {
            int newGuaranteeId = _guaranteeRepository.UpdateGuarantee(g, newTempFiles, removedAttachments);
            MarkTimelineEventsDirty();
            return newGuaranteeId;
        }

        public int UpdateGuaranteeWithAttachments(Guarantee g, List<AttachmentInput> newAttachments, List<AttachmentRecord> removedAttachments)
        {
            int newGuaranteeId = _guaranteeRepository.UpdateGuaranteeWithAttachments(g, newAttachments, removedAttachments);
            MarkTimelineEventsDirty();
            return newGuaranteeId;
        }

        public List<Guarantee> QueryGuarantees(GuaranteeQueryOptions options)
        {
            return _guaranteeRepository.QueryGuarantees(options);
        }

        public int CountGuarantees(GuaranteeQueryOptions? options = null)
        {
            return _guaranteeRepository.CountGuarantees(options);
        }

        public decimal SumGuaranteeAmounts(GuaranteeQueryOptions? options = null)
        {
            return _guaranteeRepository.SumGuaranteeAmounts(options);
        }

        public List<BankPortfolioSummary> GetBankPortfolioSummaries()
        {
            return _guaranteeRepository.GetBankPortfolioSummaries();
        }

        public int CountAttachments()
        {
            return _guaranteeRepository.CountAttachments();
        }

        public List<Guarantee> GetGuaranteeHistory(int guaranteeId)
        {
            return _guaranteeRepository.GetGuaranteeHistory(guaranteeId);
        }

        public Dictionary<int, IReadOnlyList<AttachmentRecord>> GetSeriesAttachmentsByRootIds(IReadOnlyCollection<int> rootIds)
        {
            return _guaranteeRepository.GetSeriesAttachmentsByRootIds(rootIds);
        }

        public List<GuaranteeTimelineEvent> GetGuaranteeTimelineEvents(int guaranteeId)
        {
            using var connection = SqliteConnectionFactory.Open(_connectionString);
            GuaranteeEventStore.EnsureSchema(connection);
            EnsureTimelineEventsCurrent(connection);
            return GuaranteeEventStore.GetEventsForGuarantee(connection, guaranteeId, backfillMissing: false);
        }

        public int SaveWorkflowRequest(WorkflowRequest req)
        {
            int requestId = _workflowRequestRepository.SaveWorkflowRequest(req);
            MarkTimelineEventsDirty();
            return requestId;
        }

        public bool HasPendingWorkflowRequest(int rootId, RequestType requestType)
        {
            return _workflowRequestRepository.HasPendingWorkflowRequest(rootId, requestType);
        }

        public int GetPendingWorkflowRequestCount()
        {
            return _workflowRequestRepository.GetPendingWorkflowRequestCount();
        }

        public List<int> GetPendingWorkflowRequestRootIds()
        {
            return _workflowRequestRepository.GetPendingWorkflowRequestRootIds();
        }

        public WorkflowRequest? GetWorkflowRequestById(int requestId)
        {
            return _workflowRequestRepository.GetWorkflowRequestById(requestId);
        }

        public List<WorkflowRequest> GetWorkflowRequestsByRootId(int rootId)
        {
            return _workflowRequestRepository.GetWorkflowRequestsByRootId(rootId);
        }

        public Dictionary<int, List<WorkflowRequest>> GetWorkflowRequestsByRootIds(IReadOnlyCollection<int> rootIds)
        {
            return _workflowRequestRepository.GetWorkflowRequestsByRootIds(rootIds);
        }

        public List<WorkflowRequestListItem> QueryWorkflowRequests(WorkflowRequestQueryOptions options)
        {
            return _workflowRequestRepository.QueryWorkflowRequests(options);
        }

        public int CountWorkflowRequests(WorkflowRequestQueryOptions? options = null)
        {
            return _workflowRequestRepository.CountWorkflowRequests(options);
        }

        public void RecordWorkflowResponse(
            int requestId,
            RequestStatus newStatus,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            int? resultVersionId = null)
        {
            _workflowRequestRepository.RecordWorkflowResponse(
                requestId,
                newStatus,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                resultVersionId);
            MarkTimelineEventsDirty();
        }

        public void AttachWorkflowResponseDocument(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName)
        {
            _workflowRequestRepository.AttachWorkflowResponseDocument(
                requestId,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName);
            MarkTimelineEventsDirty();
        }

        public List<Guarantee> SearchGuarantees(string query)
        {
            return _guaranteeRepository.SearchGuarantees(query);
        }

        public List<WorkflowRequestListItem> SearchWorkflowRequests(string query)
        {
            return _workflowRequestRepository.SearchWorkflowRequests(query);
        }

        public int ExecuteExtensionWorkflowRequest(
            int requestId,
            DateTime newExpiryDate,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null)
        {
            int resultVersionId = _workflowExecutionProcessor.ExecuteExtensionWorkflowRequest(
                requestId,
                newExpiryDate,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                responseAttachmentSourcePath);
            MarkTimelineEventsDirty();
            return resultVersionId;
        }

        public int ExecuteReductionWorkflowRequest(
            int requestId,
            decimal newAmount,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null)
        {
            int resultVersionId = _workflowExecutionProcessor.ExecuteReductionWorkflowRequest(
                requestId,
                newAmount,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                responseAttachmentSourcePath);
            MarkTimelineEventsDirty();
            return resultVersionId;
        }

        public int ExecuteReleaseWorkflowRequest(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null)
        {
            int resultGuaranteeId = _workflowExecutionProcessor.ExecuteReleaseWorkflowRequest(
                requestId,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                responseAttachmentSourcePath);
            MarkTimelineEventsDirty();
            return resultGuaranteeId;
        }

        public int ExecuteLiquidationWorkflowRequest(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null)
        {
            int resultGuaranteeId = _workflowExecutionProcessor.ExecuteLiquidationWorkflowRequest(
                requestId,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                responseAttachmentSourcePath);
            MarkTimelineEventsDirty();
            return resultGuaranteeId;
        }

        public int? ExecuteVerificationWorkflowRequest(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null,
            bool promoteResponseDocumentToOfficialAttachment = false)
        {
            int? resultGuaranteeId = _workflowExecutionProcessor.ExecuteVerificationWorkflowRequest(
                requestId,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                responseAttachmentSourcePath,
                promoteResponseDocumentToOfficialAttachment);
            MarkTimelineEventsDirty();
            return resultGuaranteeId;
        }

        public int ExecuteReplacementWorkflowRequest(
            int requestId,
            string replacementGuaranteeNo,
            string replacementSupplier,
            string replacementBank,
            decimal replacementAmount,
            DateTime replacementExpiryDate,
            string replacementGuaranteeType,
            string replacementBeneficiary,
            GuaranteeReferenceType replacementReferenceType,
            string replacementReferenceNumber,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null)
        {
            int replacementGuaranteeId = _workflowExecutionProcessor.ExecuteReplacementWorkflowRequest(
                requestId,
                replacementGuaranteeNo,
                replacementSupplier,
                replacementBank,
                replacementAmount,
                replacementExpiryDate,
                replacementGuaranteeType,
                replacementBeneficiary,
                replacementReferenceType,
                replacementReferenceNumber,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                responseAttachmentSourcePath);
            MarkTimelineEventsDirty();
            return replacementGuaranteeId;
        }

        public void AddBankReference(string bankName)
        {
            string normalizedBankName = (bankName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedBankName))
            {
                throw new ArgumentException("اسم البنك مطلوب.", nameof(bankName));
            }

            using var connection = SqliteConnectionFactory.Open(_connectionString);
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO BankReferences (Name, CreatedAt)
                VALUES ($name, $createdAt)";
            command.Parameters.AddWithValue("$name", normalizedBankName);
            command.Parameters.AddWithValue("$createdAt", PersistedDateTime.FormatDateTime(DateTime.Now));
            command.ExecuteNonQuery();
        }

        public List<string> GetBankReferences()
        {
            var values = new List<string>();

            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                var command = connection.CreateCommand();
                command.CommandText = "SELECT Name FROM BankReferences WHERE Name IS NOT NULL AND Name != '' ORDER BY Name COLLATE NOCASE";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    values.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, "GetBankReferences");
            }

            return values;
        }

        public List<string> GetUniqueValues(string columnName)
        {
            var values = new List<string>();

            if (!SupportedUniqueValueColumns.TryGetValue(columnName ?? string.Empty, out var safeColumnName)
                || string.IsNullOrWhiteSpace(safeColumnName))
            {
                SimpleLogger.Log($"Warning: Rejected unsupported GetUniqueValues column '{columnName}'.", "WARNING");
                return values;
            }

            try
            {
                using var connection = SqliteConnectionFactory.Open(_connectionString);
                var cmd = connection.CreateCommand();
                cmd.CommandText = safeColumnName.Equals("Bank", StringComparison.OrdinalIgnoreCase)
                    ? @"
                        SELECT Name FROM (
                            SELECT DISTINCT Bank AS Name FROM Guarantees WHERE Bank IS NOT NULL AND Bank != ''
                            UNION
                            SELECT Name FROM BankReferences WHERE Name IS NOT NULL AND Name != ''
                        )
                        ORDER BY Name COLLATE NOCASE"
                    : $"SELECT DISTINCT {safeColumnName} FROM Guarantees WHERE {safeColumnName} IS NOT NULL AND {safeColumnName} != '' ORDER BY {safeColumnName}";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    values.Add(reader.GetString(0));
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, $"GetUniqueValues ({columnName})");
            }

            return values;
        }

        public bool IsGuaranteeNoUnique(string guaranteeNo)
        {
            return _guaranteeRepository.IsGuaranteeNoUnique(guaranteeNo);
        }

        public Guarantee? GetGuaranteeById(int guaranteeId)
        {
            return _guaranteeRepository.GetGuaranteeById(guaranteeId);
        }

        public Guarantee? GetCurrentGuaranteeByRootId(int rootId)
        {
            return _guaranteeRepository.GetCurrentGuaranteeByRootId(rootId);
        }

        public Guarantee? GetCurrentGuaranteeByNo(string guaranteeNo)
        {
            return _guaranteeRepository.GetCurrentGuaranteeByNo(guaranteeNo);
        }

        public int CreateNewVersion(Guarantee newG, int sourceId, List<string> newTempFiles, List<AttachmentRecord> inheritedAttachments)
        {
            int newGuaranteeId = _guaranteeRepository.CreateNewVersion(newG, sourceId, newTempFiles, inheritedAttachments);
            MarkTimelineEventsDirty();
            return newGuaranteeId;
        }

        public int CreateNewVersionWithAttachments(Guarantee newG, int sourceId, List<AttachmentInput> newAttachments, List<AttachmentRecord> inheritedAttachments)
        {
            int newGuaranteeId = _guaranteeRepository.CreateNewVersionWithAttachments(newG, sourceId, newAttachments, inheritedAttachments);
            MarkTimelineEventsDirty();
            return newGuaranteeId;
        }
    }
}
