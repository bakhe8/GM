using System;
using System.Collections.Generic;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    public interface IDatabaseService
    {
        void SaveGuarantee(Guarantee g, List<string> tempFilePaths);
        void SaveGuaranteeWithAttachments(Guarantee g, List<AttachmentInput> attachments);
        void AddGuaranteeAttachments(int guaranteeId, List<AttachmentInput> attachments);
        int UpdateGuarantee(Guarantee g, List<string> newTempFiles, List<AttachmentRecord> removedAttachments);
        int UpdateGuaranteeWithAttachments(Guarantee g, List<AttachmentInput> newAttachments, List<AttachmentRecord> removedAttachments);
        List<Guarantee> QueryGuarantees(GuaranteeQueryOptions options);
        int CountGuarantees(GuaranteeQueryOptions? options = null);
        int CountAttachments();
        List<Guarantee> SearchGuarantees(string query);
        List<Guarantee> GetGuaranteeHistory(int guaranteeId);
        List<GuaranteeTimelineEvent> GetGuaranteeTimelineEvents(int guaranteeId);
        int SaveWorkflowRequest(WorkflowRequest req);
        bool HasPendingWorkflowRequest(int rootId, RequestType requestType);
        int GetPendingWorkflowRequestCount();
        WorkflowRequest? GetWorkflowRequestById(int requestId);
        List<WorkflowRequest> GetWorkflowRequestsByRootId(int rootId);
        List<WorkflowRequestListItem> QueryWorkflowRequests(WorkflowRequestQueryOptions options);
        int CountWorkflowRequests(WorkflowRequestQueryOptions? options = null);
        List<WorkflowRequestListItem> SearchWorkflowRequests(string query);
        void RecordWorkflowResponse(
            int requestId,
            RequestStatus newStatus,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            int? resultVersionId = null);
        void AttachWorkflowResponseDocument(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName);
        int ExecuteExtensionWorkflowRequest(
            int requestId,
            DateTime newExpiryDate,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null);
        int ExecuteReductionWorkflowRequest(
            int requestId,
            decimal newAmount,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null);
        int ExecuteReleaseWorkflowRequest(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null);
        int ExecuteLiquidationWorkflowRequest(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null);
        int? ExecuteVerificationWorkflowRequest(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null,
            bool promoteResponseDocumentToOfficialAttachment = false);
        int ExecuteReplacementWorkflowRequest(
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
            string? responseAttachmentSourcePath = null);
        void DeleteAttachment(AttachmentRecord att);
        void AddBankReference(string bankName);
        List<string> GetBankReferences();
        List<string> GetUniqueValues(string columnName);
        bool IsGuaranteeNoUnique(string guaranteeNo);
        Guarantee? GetGuaranteeById(int guaranteeId);
        Guarantee? GetCurrentGuaranteeByRootId(int rootId);
        Guarantee? GetCurrentGuaranteeByNo(string guaranteeNo);
        int CreateNewVersion(Guarantee newG, int sourceId, List<string> newTempFiles, List<AttachmentRecord> inheritedAttachments);
        int CreateNewVersionWithAttachments(Guarantee newG, int sourceId, List<AttachmentInput> newAttachments, List<AttachmentRecord> inheritedAttachments);
    }
}
