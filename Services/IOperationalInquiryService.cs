using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    public interface IOperationalInquiryService
    {
        OperationalInquiryResult GetLastEventForGuarantee(int guaranteeId);
        OperationalInquiryResult GetExtensionTimingForGuarantee(int guaranteeId);
        OperationalInquiryResult GetOutstandingReasonForGuarantee(int guaranteeId, RequestType requestType);
        OperationalInquiryResult GetPendingRequestsForBank(string bank);
        OperationalInquiryResult GetLatestActivityForSupplier(string supplier);
        OperationalInquiryResult GetExpiredWithoutExtensionReasonForGuarantee(int guaranteeId);
        OperationalInquiryResult GetReleaseEvidenceForGuarantee(int guaranteeId);
        OperationalInquiryResult GetLiquidationEvidenceForGuarantee(int guaranteeId);
        OperationalInquiryResult GetReductionSourceForGuarantee(int guaranteeId);
        OperationalInquiryResult GetResponseDocumentLinkStatusForGuarantee(int guaranteeId);
        OperationalInquiryResult GetBankConfirmationSummary(string bank);
        OperationalInquiryResult GetTopOldestPendingRequests(int topCount = 10);
        OperationalInquiryResult GetExecutedExtensionsThisMonth();
        OperationalInquiryResult GetActivePurchaseOrderOnlyGuarantees();
        OperationalInquiryResult GetContractRelatedReleasedLastWeek();
        OperationalInquiryResult GetEmployeeCreatedContractRequestsLastMonth(string employeeName);
        OperationalInquiryResult GetExpiredPurchaseOrderOnlyWithoutExtensionAmount();
    }
}
