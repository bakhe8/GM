using System.Collections.Generic;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    public interface IExcelService
    {
        string? LastOutputPath { get; }

        bool ExportGuarantees(IReadOnlyList<Guarantee> guarantees);
        bool ExportGuaranteesByBank(string bank, IReadOnlyList<Guarantee> guarantees);
        bool ExportGuaranteesBySupplier(string supplier, IReadOnlyList<Guarantee> guarantees);
        bool ExportGuaranteesByTemporalStatus(string temporalStatus, IReadOnlyList<Guarantee> guarantees);
        bool ExportGuaranteesByLifecycleStatus(string lifecycleStatus, IReadOnlyList<Guarantee> guarantees);
        bool ExportGuaranteesByType(string guaranteeType, IReadOnlyList<Guarantee> guarantees);
        bool ExportSingleGuaranteeReport(Guarantee guarantee);
        bool ExportDailyFollowUpReport(IReadOnlyList<Guarantee> guarantees, IReadOnlyList<WorkflowRequestListItem> requests);
        bool ExportExpiringSoonGuarantees(IReadOnlyList<Guarantee> guarantees);
        bool ExportExpiredActiveGuarantees(IReadOnlyList<Guarantee> guarantees);
        bool ExportGuaranteeStatisticsByBank(IReadOnlyList<Guarantee> guarantees);
        bool ExportGuaranteeStatisticsBySupplier(IReadOnlyList<Guarantee> guarantees);
        bool ExportGuaranteePortfolioSummary(IReadOnlyList<Guarantee> guarantees, IReadOnlyList<WorkflowRequestListItem> requests);
        bool ExportGuaranteeVersionCounts(IReadOnlyList<Guarantee> guarantees);
        bool ExportGuaranteesWithoutAttachments(IReadOnlyList<Guarantee> guarantees);
        bool ExportGuaranteeVersionsWithoutAttachments(IReadOnlyList<Guarantee> guarantees);
        bool ExportWorkflowRequestsByStatus(RequestStatus status, IReadOnlyList<WorkflowRequestListItem> requests);
        bool ExportWorkflowRequestsByType(RequestType type, IReadOnlyList<WorkflowRequestListItem> requests);
        bool ExportWorkflowRequestsByTypeAndStatus(RequestType type, RequestStatus status, IReadOnlyList<WorkflowRequestListItem> requests);
        bool ExportExecutedRequestsWithEffect(IReadOnlyList<WorkflowRequestListItem> requests);
        bool ExportClosedWithoutExecutionRequests(IReadOnlyList<WorkflowRequestListItem> requests);
        bool ExportSupersededRequests(IReadOnlyList<WorkflowRequestListItem> requests);
        bool ExportRequestsWithoutResponseDocument(IReadOnlyList<WorkflowRequestListItem> requests);
        bool ExportPendingWorkflowRequestsByType(RequestType type, IReadOnlyList<WorkflowRequestListItem> requests);
        bool ExportPendingRequestsByBank(string bank, IReadOnlyList<WorkflowRequestListItem> requests);
        bool ExportWorkflowRequestsByBank(string bank, IReadOnlyList<WorkflowRequestListItem> requests);
        bool ExportOldestPendingRequests(IReadOnlyList<WorkflowRequestListItem> requests, int topCount = 10);
        bool ExportExecutedExtensionsThisMonth(IReadOnlyList<WorkflowRequestListItem> requests, System.DateTime periodStart, System.DateTime periodEnd);
        bool ExportActivePurchaseOrderOnlyGuarantees(IReadOnlyList<Guarantee> guarantees);
        bool ExportContractRelatedReleasedInPeriod(IReadOnlyList<WorkflowRequestListItem> requests, System.DateTime periodStart, System.DateTime periodEnd);
        bool ExportEmployeeContractRequestsInPeriod(string employeeName, IReadOnlyList<WorkflowRequestListItem> requests, System.DateTime periodStart, System.DateTime periodEnd);
        bool ExportExpiredPurchaseOrderOnlyWithoutExecutedExtension(IReadOnlyList<Guarantee> guarantees);
    }
}
