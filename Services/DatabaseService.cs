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
        private static bool _runtimeInitialized;
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
            }
        }

        internal static void ResetRuntimeInitializationForTesting()
        {
            lock (RuntimeInitializationLock)
            {
                _runtimeInitialized = false;
            }
        }

        public void SaveGuarantee(Guarantee g, List<string> tempFilePaths)
        {
            _guaranteeRepository.SaveGuarantee(g, tempFilePaths);
        }

        public int UpdateGuarantee(Guarantee g, List<string> newTempFiles, List<AttachmentRecord> removedAttachments)
        {
            return _guaranteeRepository.UpdateGuarantee(g, newTempFiles, removedAttachments);
        }

        public List<Guarantee> QueryGuarantees(GuaranteeQueryOptions options)
        {
            return _guaranteeRepository.QueryGuarantees(options);
        }

        public int CountGuarantees(GuaranteeQueryOptions? options = null)
        {
            return _guaranteeRepository.CountGuarantees(options);
        }

        public int CountAttachments()
        {
            return _guaranteeRepository.CountAttachments();
        }

        public List<Guarantee> GetGuaranteeHistory(int guaranteeId)
        {
            return _guaranteeRepository.GetGuaranteeHistory(guaranteeId);
        }

        public int SaveWorkflowRequest(WorkflowRequest req)
        {
            return _workflowRequestRepository.SaveWorkflowRequest(req);
        }

        public bool HasPendingWorkflowRequest(int rootId, RequestType requestType)
        {
            return _workflowRequestRepository.HasPendingWorkflowRequest(rootId, requestType);
        }

        public int GetPendingWorkflowRequestCount()
        {
            return _workflowRequestRepository.GetPendingWorkflowRequestCount();
        }

        public WorkflowRequest? GetWorkflowRequestById(int requestId)
        {
            return _workflowRequestRepository.GetWorkflowRequestById(requestId);
        }

        public List<WorkflowRequest> GetWorkflowRequestsByRootId(int rootId)
        {
            return _workflowRequestRepository.GetWorkflowRequestsByRootId(rootId);
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
            return _workflowExecutionProcessor.ExecuteExtensionWorkflowRequest(
                requestId,
                newExpiryDate,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                responseAttachmentSourcePath);
        }

        public int ExecuteReductionWorkflowRequest(
            int requestId,
            decimal newAmount,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null)
        {
            return _workflowExecutionProcessor.ExecuteReductionWorkflowRequest(
                requestId,
                newAmount,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                responseAttachmentSourcePath);
        }

        public int ExecuteReleaseWorkflowRequest(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null)
        {
            return _workflowExecutionProcessor.ExecuteReleaseWorkflowRequest(
                requestId,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                responseAttachmentSourcePath);
        }

        public int ExecuteLiquidationWorkflowRequest(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null)
        {
            return _workflowExecutionProcessor.ExecuteLiquidationWorkflowRequest(
                requestId,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                responseAttachmentSourcePath);
        }

        public int? ExecuteVerificationWorkflowRequest(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null,
            bool promoteResponseDocumentToOfficialAttachment = false)
        {
            return _workflowExecutionProcessor.ExecuteVerificationWorkflowRequest(
                requestId,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                responseAttachmentSourcePath,
                promoteResponseDocumentToOfficialAttachment);
        }

        public int ExecuteAnnulmentWorkflowRequest(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null)
        {
            return _workflowExecutionProcessor.ExecuteAnnulmentWorkflowRequest(
                requestId,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                responseAttachmentSourcePath);
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
            return _workflowExecutionProcessor.ExecuteReplacementWorkflowRequest(
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
        }

        public void DeleteAttachment(AttachmentRecord att)
        {
            _guaranteeRepository.DeleteAttachment(att);
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
                cmd.CommandText = $"SELECT DISTINCT {safeColumnName} FROM Guarantees WHERE {safeColumnName} IS NOT NULL AND {safeColumnName} != '' ORDER BY {safeColumnName}";
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
            return _guaranteeRepository.CreateNewVersion(newG, sourceId, newTempFiles, inheritedAttachments);
        }
    }
}
