using System;
using System.Collections.Generic;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    public class ExcelService : IExcelService
    {
        private readonly IGuaranteeExcelReportStrategy _guaranteeReports;
        private readonly IWorkflowRequestExcelReportStrategy _workflowReports;
        private readonly IPortfolioExcelReportStrategy _portfolioReports;

        public ExcelService()
            : this(
                new GuaranteeExcelReportStrategy(),
                new WorkflowRequestExcelReportStrategy(),
                new PortfolioExcelReportStrategy())
        {
        }

        internal ExcelService(
            IGuaranteeExcelReportStrategy guaranteeReports,
            IWorkflowRequestExcelReportStrategy workflowReports,
            IPortfolioExcelReportStrategy portfolioReports)
        {
            _guaranteeReports = guaranteeReports;
            _workflowReports = workflowReports;
            _portfolioReports = portfolioReports;
        }

        public string? LastOutputPath { get; private set; }

        public bool ExportGuarantees(IReadOnlyList<Guarantee> guarantees) => Complete(_guaranteeReports.ExportGuarantees(guarantees));
        public bool ExportGuaranteesByBank(string bank, IReadOnlyList<Guarantee> guarantees) => Complete(_guaranteeReports.ExportGuaranteesByBank(bank, guarantees));
        public bool ExportGuaranteesBySupplier(string supplier, IReadOnlyList<Guarantee> guarantees) => Complete(_guaranteeReports.ExportGuaranteesBySupplier(supplier, guarantees));
        public bool ExportGuaranteesByTemporalStatus(string temporalStatus, IReadOnlyList<Guarantee> guarantees) => Complete(_guaranteeReports.ExportGuaranteesByTemporalStatus(temporalStatus, guarantees));
        public bool ExportGuaranteesByLifecycleStatus(string lifecycleStatus, IReadOnlyList<Guarantee> guarantees) => Complete(_guaranteeReports.ExportGuaranteesByLifecycleStatus(lifecycleStatus, guarantees));
        public bool ExportGuaranteesByType(string guaranteeType, IReadOnlyList<Guarantee> guarantees) => Complete(_guaranteeReports.ExportGuaranteesByType(guaranteeType, guarantees));
        public bool ExportSingleGuaranteeReport(Guarantee guarantee) => Complete(_guaranteeReports.ExportSingleGuaranteeReport(guarantee));
        public bool ExportDailyFollowUpReport(IReadOnlyList<Guarantee> guarantees, IReadOnlyList<WorkflowRequestListItem> requests) => Complete(_portfolioReports.ExportDailyFollowUpReport(guarantees, requests));
        public bool ExportExpiringSoonGuarantees(IReadOnlyList<Guarantee> guarantees) => Complete(_guaranteeReports.ExportExpiringSoonGuarantees(guarantees));
        public bool ExportExpiredActiveGuarantees(IReadOnlyList<Guarantee> guarantees) => Complete(_guaranteeReports.ExportExpiredActiveGuarantees(guarantees));
        public bool ExportGuaranteeStatisticsByBank(IReadOnlyList<Guarantee> guarantees) => Complete(_guaranteeReports.ExportGuaranteeStatisticsByBank(guarantees));
        public bool ExportGuaranteeStatisticsBySupplier(IReadOnlyList<Guarantee> guarantees) => Complete(_guaranteeReports.ExportGuaranteeStatisticsBySupplier(guarantees));
        public bool ExportGuaranteePortfolioSummary(IReadOnlyList<Guarantee> guarantees, IReadOnlyList<WorkflowRequestListItem> requests) => Complete(_portfolioReports.ExportGuaranteePortfolioSummary(guarantees, requests));
        public bool ExportGuaranteeVersionCounts(IReadOnlyList<Guarantee> guarantees) => Complete(_guaranteeReports.ExportGuaranteeVersionCounts(guarantees));
        public bool ExportGuaranteesWithoutAttachments(IReadOnlyList<Guarantee> guarantees) => Complete(_guaranteeReports.ExportGuaranteesWithoutAttachments(guarantees));
        public bool ExportGuaranteeVersionsWithoutAttachments(IReadOnlyList<Guarantee> guarantees) => Complete(_guaranteeReports.ExportGuaranteeVersionsWithoutAttachments(guarantees));
        public bool ExportWorkflowRequestsByStatus(RequestStatus status, IReadOnlyList<WorkflowRequestListItem> requests) => Complete(_workflowReports.ExportWorkflowRequestsByStatus(status, requests));
        public bool ExportWorkflowRequestsByType(RequestType type, IReadOnlyList<WorkflowRequestListItem> requests) => Complete(_workflowReports.ExportWorkflowRequestsByType(type, requests));
        public bool ExportWorkflowRequestsByTypeAndStatus(RequestType type, RequestStatus status, IReadOnlyList<WorkflowRequestListItem> requests) => Complete(_workflowReports.ExportWorkflowRequestsByTypeAndStatus(type, status, requests));
        public bool ExportExecutedRequestsWithEffect(IReadOnlyList<WorkflowRequestListItem> requests) => Complete(_workflowReports.ExportExecutedRequestsWithEffect(requests));
        public bool ExportClosedWithoutExecutionRequests(IReadOnlyList<WorkflowRequestListItem> requests) => Complete(_workflowReports.ExportClosedWithoutExecutionRequests(requests));
        public bool ExportSupersededRequests(IReadOnlyList<WorkflowRequestListItem> requests) => Complete(_workflowReports.ExportSupersededRequests(requests));
        public bool ExportRequestsWithoutResponseDocument(IReadOnlyList<WorkflowRequestListItem> requests) => Complete(_workflowReports.ExportRequestsWithoutResponseDocument(requests));
        public bool ExportPendingWorkflowRequestsByType(RequestType type, IReadOnlyList<WorkflowRequestListItem> requests) => Complete(_workflowReports.ExportPendingWorkflowRequestsByType(type, requests));
        public bool ExportPendingRequestsByBank(string bank, IReadOnlyList<WorkflowRequestListItem> requests) => Complete(_workflowReports.ExportPendingRequestsByBank(bank, requests));
        public bool ExportWorkflowRequestsByBank(string bank, IReadOnlyList<WorkflowRequestListItem> requests) => Complete(_workflowReports.ExportWorkflowRequestsByBank(bank, requests));
        public bool ExportOldestPendingRequests(IReadOnlyList<WorkflowRequestListItem> requests, int topCount = 10) => Complete(_workflowReports.ExportOldestPendingRequests(requests, topCount));
        public bool ExportExecutedExtensionsThisMonth(IReadOnlyList<WorkflowRequestListItem> requests, DateTime periodStart, DateTime periodEnd) => Complete(_workflowReports.ExportExecutedExtensionsThisMonth(requests, periodStart, periodEnd));
        public bool ExportActivePurchaseOrderOnlyGuarantees(IReadOnlyList<Guarantee> guarantees) => Complete(_guaranteeReports.ExportActivePurchaseOrderOnlyGuarantees(guarantees));
        public bool ExportContractRelatedReleasedInPeriod(IReadOnlyList<WorkflowRequestListItem> requests, DateTime periodStart, DateTime periodEnd) => Complete(_workflowReports.ExportContractRelatedReleasedInPeriod(requests, periodStart, periodEnd));
        public bool ExportEmployeeContractRequestsInPeriod(string employeeName, IReadOnlyList<WorkflowRequestListItem> requests, DateTime periodStart, DateTime periodEnd) => Complete(_workflowReports.ExportEmployeeContractRequestsInPeriod(employeeName, requests, periodStart, periodEnd));
        public bool ExportExpiredPurchaseOrderOnlyWithoutExecutedExtension(IReadOnlyList<Guarantee> guarantees) => Complete(_guaranteeReports.ExportExpiredPurchaseOrderOnlyWithoutExecutedExtension(guarantees));

        internal bool ExportSingleGuaranteeReportToPath(Guarantee guarantee, string outputPath)
        {
            ResetLastOutputPath();
            bool exported = _guaranteeReports.ExportSingleGuaranteeReportToPath(guarantee, outputPath);
            if (exported)
            {
                LastOutputPath = outputPath;
            }

            return exported;
        }

        internal bool ExportGuaranteesReportToPath(
            IReadOnlyList<Guarantee> guarantees,
            string reportTitle,
            string reportSubtitle,
            string outputPath)
        {
            ResetLastOutputPath();
            bool exported = _guaranteeReports.ExportGuaranteesReportToPath(guarantees, reportTitle, reportSubtitle, outputPath);
            if (exported)
            {
                LastOutputPath = outputPath;
            }

            return exported;
        }

        internal bool ExportWorkflowRequestsReportToPath(
            IReadOnlyList<WorkflowRequestListItem> requests,
            string reportTitle,
            string reportSubtitle,
            string outputPath)
        {
            ResetLastOutputPath();
            bool exported = _workflowReports.ExportWorkflowRequestsReportToPath(requests, reportTitle, reportSubtitle, outputPath);
            if (exported)
            {
                LastOutputPath = outputPath;
            }

            return exported;
        }

        private bool Complete(ExcelExportResult result)
        {
            ResetLastOutputPath();

            if (result.Exported && !string.IsNullOrWhiteSpace(result.OutputPath))
            {
                LastOutputPath = result.OutputPath;
            }

            return result.Exported;
        }

        private void ResetLastOutputPath()
        {
            LastOutputPath = null;
        }
    }
}
