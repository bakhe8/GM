using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    internal class WorkflowExecutionProcessor
    {
        private readonly WorkflowNewVersionExecutor _newVersionExecutor;
        private readonly WorkflowVerificationExecutor _verificationExecutor;
        private readonly WorkflowAnnulmentExecutor _annulmentExecutor;
        private readonly WorkflowReplacementExecutor _replacementExecutor;

        public WorkflowExecutionProcessor(string connectionString, AttachmentStorageService attachmentStorage)
        {
            _newVersionExecutor = new WorkflowNewVersionExecutor(connectionString, attachmentStorage);
            _verificationExecutor = new WorkflowVerificationExecutor(connectionString, _newVersionExecutor);
            _annulmentExecutor = new WorkflowAnnulmentExecutor(connectionString, attachmentStorage);
            _replacementExecutor = new WorkflowReplacementExecutor(connectionString, attachmentStorage);
        }

        public int ExecuteExtensionWorkflowRequest(
            int requestId,
            DateTime newExpiryDate,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null)
        {
            return _newVersionExecutor.Execute(
                requestId,
                RequestType.Extension,
                newExpiryDate,
                null,
                null,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                responseAttachmentSourcePath,
                false);
        }

        public int ExecuteReductionWorkflowRequest(
            int requestId,
            decimal newAmount,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null)
        {
            return _newVersionExecutor.Execute(
                requestId,
                RequestType.Reduction,
                null,
                newAmount,
                null,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                responseAttachmentSourcePath,
                false);
        }

        public int ExecuteReleaseWorkflowRequest(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null)
        {
            return _newVersionExecutor.Execute(
                requestId,
                RequestType.Release,
                null,
                null,
                GuaranteeLifecycleStatus.Released,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                responseAttachmentSourcePath,
                true,
                "أُسقط الطلب تلقائيًا بسبب تنفيذ طلب الإفراج.");
        }

        public int ExecuteLiquidationWorkflowRequest(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null)
        {
            return _newVersionExecutor.Execute(
                requestId,
                RequestType.Liquidation,
                null,
                null,
                GuaranteeLifecycleStatus.Liquidated,
                responseNotes,
                responseOriginalFileName,
                responseSavedFileName,
                responseAttachmentSourcePath,
                true,
                "أُسقط الطلب تلقائيًا بسبب تنفيذ طلب التسييل.");
        }

        public int? ExecuteVerificationWorkflowRequest(
            int requestId,
            string responseNotes,
            string responseOriginalFileName,
            string responseSavedFileName,
            string? responseAttachmentSourcePath = null,
            bool promoteResponseDocumentToOfficialAttachment = false)
        {
            return _verificationExecutor.Execute(
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
            return _annulmentExecutor.Execute(
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
            return _replacementExecutor.Execute(
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
    }
}
