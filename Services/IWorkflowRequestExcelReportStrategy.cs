using System;
using System.Collections.Generic;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    internal interface IWorkflowRequestExcelReportStrategy
    {
        ExcelExportResult ExportWorkflowRequestsByStatus(RequestStatus status, IReadOnlyList<WorkflowRequestListItem> requests);
        ExcelExportResult ExportWorkflowRequestsByType(RequestType type, IReadOnlyList<WorkflowRequestListItem> requests);
        ExcelExportResult ExportWorkflowRequestsByTypeAndStatus(RequestType type, RequestStatus status, IReadOnlyList<WorkflowRequestListItem> requests);
        ExcelExportResult ExportExecutedRequestsWithEffect(IReadOnlyList<WorkflowRequestListItem> requests);
        ExcelExportResult ExportClosedWithoutExecutionRequests(IReadOnlyList<WorkflowRequestListItem> requests);
        ExcelExportResult ExportSupersededRequests(IReadOnlyList<WorkflowRequestListItem> requests);
        ExcelExportResult ExportRequestsWithoutResponseDocument(IReadOnlyList<WorkflowRequestListItem> requests);
        ExcelExportResult ExportPendingWorkflowRequestsByType(RequestType type, IReadOnlyList<WorkflowRequestListItem> requests);
        ExcelExportResult ExportPendingRequestsByBank(string bank, IReadOnlyList<WorkflowRequestListItem> requests);
        ExcelExportResult ExportWorkflowRequestsByBank(string bank, IReadOnlyList<WorkflowRequestListItem> requests);
        ExcelExportResult ExportOldestPendingRequests(IReadOnlyList<WorkflowRequestListItem> requests, int topCount = 10);
        ExcelExportResult ExportExecutedExtensionsThisMonth(IReadOnlyList<WorkflowRequestListItem> requests, DateTime periodStart, DateTime periodEnd);
        ExcelExportResult ExportContractRelatedReleasedInPeriod(IReadOnlyList<WorkflowRequestListItem> requests, DateTime periodStart, DateTime periodEnd);
        ExcelExportResult ExportEmployeeContractRequestsInPeriod(string employeeName, IReadOnlyList<WorkflowRequestListItem> requests, DateTime periodStart, DateTime periodEnd);
        bool ExportWorkflowRequestsReportToPath(
            IReadOnlyList<WorkflowRequestListItem> requests,
            string reportTitle,
            string reportSubtitle,
            string outputPath);
    }
}
