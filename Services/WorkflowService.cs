using System.Collections.Generic;
using System.Linq;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    public class WorkflowService : IWorkflowService
    {
        private readonly IDatabaseService _databaseService;
        private readonly WorkflowLetterService _letterService;
        private readonly WorkflowRequestCreator _requestCreator;
        private readonly WorkflowResponseRecorder _responseRecorder;

        public WorkflowService(IDatabaseService databaseService)
            : this(databaseService, new WorkflowLetterService(), new WorkflowResponseStorageService())
        {
        }

        internal WorkflowService(
            IDatabaseService databaseService,
            WorkflowLetterService letterService,
            WorkflowResponseStorageService responseStorage)
            : this(
                databaseService,
                letterService,
                new WorkflowRequestCreator(databaseService, letterService),
                new WorkflowResponseRecorder(databaseService, responseStorage))
        {
        }

        internal WorkflowService(
            IDatabaseService databaseService,
            WorkflowLetterService letterService,
            WorkflowRequestCreator requestCreator,
            WorkflowResponseRecorder responseRecorder)
        {
            _databaseService = databaseService;
            _letterService = letterService;
            _requestCreator = requestCreator;
            _responseRecorder = responseRecorder;
        }

        public List<Guarantee> GetGuaranteesEligibleForExtension()
        {
            return GetActiveGuarantees();
        }

        public List<Guarantee> GetGuaranteesEligibleForReduction()
        {
            return _databaseService
                .QueryGuarantees(new GuaranteeQueryOptions
                {
                    LifecycleStatus = GuaranteeLifecycleStatus.Active,
                    SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
                })
                .Where(g => g.Amount > 0)
                .OrderBy(g => g.GuaranteeNo)
                .ToList();
        }

        public List<Guarantee> GetGuaranteesEligibleForRelease()
        {
            return GetActiveGuarantees();
        }

        public List<Guarantee> GetGuaranteesEligibleForLiquidation()
        {
            return GetActiveGuarantees();
        }

        public List<Guarantee> GetGuaranteesEligibleForVerification()
        {
            return GetActiveGuarantees();
        }

        public List<Guarantee> GetGuaranteesEligibleForReplacement()
        {
            return GetActiveGuarantees();
        }

        public List<Guarantee> GetGuaranteesEligibleForAnnulment()
        {
            return GetGuaranteesByLifecycleStatuses(
                GuaranteeLifecycleStatus.Released,
                GuaranteeLifecycleStatus.Liquidated);
        }

        public WorkflowRequest CreateAnnulmentRequest(int guaranteeId, string reason, string createdBy = "")
        {
            return _requestCreator.CreateAnnulmentRequest(guaranteeId, reason, createdBy);
        }

        public WorkflowRequest CreateExtensionRequest(int guaranteeId, DateTime requestedExpiryDate, string notes, string createdBy = "")
        {
            return _requestCreator.CreateExtensionRequest(guaranteeId, requestedExpiryDate, notes, createdBy);
        }

        public WorkflowRequest CreateReductionRequest(int guaranteeId, decimal requestedAmount, string notes, string createdBy = "")
        {
            return _requestCreator.CreateReductionRequest(guaranteeId, requestedAmount, notes, createdBy);
        }

        public WorkflowRequest CreateReleaseRequest(int guaranteeId, string notes, string createdBy = "")
        {
            return _requestCreator.CreateReleaseRequest(guaranteeId, notes, createdBy);
        }

        public WorkflowRequest CreateLiquidationRequest(int guaranteeId, string notes, string createdBy = "")
        {
            return _requestCreator.CreateLiquidationRequest(guaranteeId, notes, createdBy);
        }

        public WorkflowRequest CreateVerificationRequest(int guaranteeId, string notes, string createdBy = "")
        {
            return _requestCreator.CreateVerificationRequest(guaranteeId, notes, createdBy);
        }

        public WorkflowRequest CreateReplacementRequest(
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
            string createdBy = "")
        {
            return _requestCreator.CreateReplacementRequest(
                guaranteeId,
                replacementGuaranteeNo,
                replacementSupplier,
                replacementBank,
                replacementAmount,
                replacementExpiryDate,
                replacementGuaranteeType,
                replacementBeneficiary,
                replacementReferenceType,
                replacementReferenceNumber,
                notes,
                createdBy);
        }

        public void RecordBankResponse(
            int requestId,
            RequestStatus resultStatus,
            string responseNotes,
            string? responseDocumentPath = null,
            bool promoteResponseDocumentToOfficialAttachment = false)
        {
            _responseRecorder.RecordBankResponse(
                requestId,
                resultStatus,
                responseNotes,
                responseDocumentPath,
                promoteResponseDocumentToOfficialAttachment);
        }

        public void AttachResponseDocumentToClosedRequest(
            int requestId,
            string responseDocumentPath,
            string additionalNotes = "")
        {
            _responseRecorder.AttachResponseDocumentToClosedRequest(
                requestId,
                responseDocumentPath,
                additionalNotes);
        }

        public void OpenRequestLetter(WorkflowRequest request)
        {
            _letterService.OpenLetter(request);
        }

        public void OpenResponseDocument(WorkflowRequest request)
        {
            _letterService.OpenResponseDocument(request);
        }

        private List<Guarantee> GetGuaranteesByLifecycleStatuses(params GuaranteeLifecycleStatus[] statuses)
        {
            return statuses
                .SelectMany(status => _databaseService.QueryGuarantees(new GuaranteeQueryOptions
                {
                    LifecycleStatus = status,
                    SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
                }))
                .OrderBy(item => item.GuaranteeNo)
                .ToList();
        }

        private List<Guarantee> GetActiveGuarantees()
        {
            return _databaseService
                .QueryGuarantees(new GuaranteeQueryOptions
                {
                    LifecycleStatus = GuaranteeLifecycleStatus.Active,
                    SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
                })
                .OrderBy(item => item.GuaranteeNo)
                .ToList();
        }

    }
}
