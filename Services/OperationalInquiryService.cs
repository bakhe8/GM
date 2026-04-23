using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    public sealed class OperationalInquiryService : IOperationalInquiryService
    {
        private readonly GuaranteeDetailInquiryService _detail;
        private readonly CounterpartyInquiryService _counterparty;
        private readonly PortfolioStatisticsService _statistics;

        public OperationalInquiryService(IDatabaseService databaseService)
        {
            _detail = new GuaranteeDetailInquiryService(databaseService);
            _counterparty = new CounterpartyInquiryService(databaseService);
            _statistics = new PortfolioStatisticsService(databaseService);
        }

        public OperationalInquiryResult GetLastEventForGuarantee(int guaranteeId)
            => _detail.GetLastEventForGuarantee(guaranteeId);

        public OperationalInquiryResult GetExtensionTimingForGuarantee(int guaranteeId)
            => _detail.GetExtensionTimingForGuarantee(guaranteeId);

        public OperationalInquiryResult GetOutstandingReasonForGuarantee(int guaranteeId, RequestType requestType)
            => _detail.GetOutstandingReasonForGuarantee(guaranteeId, requestType);

        public OperationalInquiryResult GetExpiredWithoutExtensionReasonForGuarantee(int guaranteeId)
            => _detail.GetExpiredWithoutExtensionReasonForGuarantee(guaranteeId);

        public OperationalInquiryResult GetReleaseEvidenceForGuarantee(int guaranteeId)
            => _detail.GetReleaseEvidenceForGuarantee(guaranteeId);

        public OperationalInquiryResult GetLiquidationEvidenceForGuarantee(int guaranteeId)
            => _detail.GetLiquidationEvidenceForGuarantee(guaranteeId);

        public OperationalInquiryResult GetReductionSourceForGuarantee(int guaranteeId)
            => _detail.GetReductionSourceForGuarantee(guaranteeId);

        public OperationalInquiryResult GetResponseDocumentLinkStatusForGuarantee(int guaranteeId)
            => _detail.GetResponseDocumentLinkStatusForGuarantee(guaranteeId);

        public OperationalInquiryResult GetPendingRequestsForBank(string bank)
            => _counterparty.GetPendingRequestsForBank(bank);

        public OperationalInquiryResult GetBankConfirmationSummary(string bank)
            => _counterparty.GetBankConfirmationSummary(bank);

        public OperationalInquiryResult GetLatestActivityForSupplier(string supplier)
            => _counterparty.GetLatestActivityForSupplier(supplier);

        public OperationalInquiryResult GetTopOldestPendingRequests(int topCount = 10)
            => _statistics.GetTopOldestPendingRequests(topCount);

        public OperationalInquiryResult GetExecutedExtensionsThisMonth()
            => _statistics.GetExecutedExtensionsThisMonth();

        public OperationalInquiryResult GetActivePurchaseOrderOnlyGuarantees()
            => _statistics.GetActivePurchaseOrderOnlyGuarantees();

        public OperationalInquiryResult GetContractRelatedReleasedLastWeek()
            => _statistics.GetContractRelatedReleasedLastWeek();

        public OperationalInquiryResult GetEmployeeCreatedContractRequestsLastMonth(string employeeName)
            => _statistics.GetEmployeeCreatedContractRequestsLastMonth(employeeName);

        public OperationalInquiryResult GetExpiredPurchaseOrderOnlyWithoutExtensionAmount()
            => _statistics.GetExpiredPurchaseOrderOnlyWithoutExtensionAmount();
    }
}
