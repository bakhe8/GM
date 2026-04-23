using System;
using System.Collections.Generic;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    public interface IWorkflowService
    {
        List<Guarantee> GetGuaranteesEligibleForExtension();
        List<Guarantee> GetGuaranteesEligibleForReduction();
        List<Guarantee> GetGuaranteesEligibleForRelease();
        List<Guarantee> GetGuaranteesEligibleForLiquidation();
        List<Guarantee> GetGuaranteesEligibleForVerification();
        List<Guarantee> GetGuaranteesEligibleForReplacement();
        List<Guarantee> GetGuaranteesEligibleForAnnulment();
        WorkflowRequest CreateAnnulmentRequest(int guaranteeId, string reason, string createdBy = "");
        WorkflowRequest CreateExtensionRequest(int guaranteeId, DateTime requestedExpiryDate, string notes, string createdBy = "");
        WorkflowRequest CreateReductionRequest(int guaranteeId, decimal requestedAmount, string notes, string createdBy = "");
        WorkflowRequest CreateReleaseRequest(int guaranteeId, string notes, string createdBy = "");
        WorkflowRequest CreateLiquidationRequest(int guaranteeId, string notes, string createdBy = "");
        WorkflowRequest CreateVerificationRequest(int guaranteeId, string notes, string createdBy = "");
        WorkflowRequest CreateReplacementRequest(
            int guaranteeId,
            string replacementGuaranteeNo,
            string replacementSupplier,
            string replacementBank,
            decimal replacementAmount,
            DateTime replacementExpiryDate,
            string replacementGuaranteeType,
            string replacementBeneficiary,
            GuaranteeReferenceType replacementReferenceType,
            string replacementReferenceNumber,
            string notes,
            string createdBy = "");
        void RecordBankResponse(
            int requestId,
            RequestStatus resultStatus,
            string responseNotes,
            string? responseDocumentPath = null,
            bool promoteResponseDocumentToOfficialAttachment = false);
        void AttachResponseDocumentToClosedRequest(
            int requestId,
            string responseDocumentPath,
            string additionalNotes = "");
        void OpenRequestLetter(WorkflowRequest request);
        void OpenResponseDocument(WorkflowRequest request);
    }
}
