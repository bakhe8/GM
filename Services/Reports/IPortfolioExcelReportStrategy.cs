using System.Collections.Generic;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    internal interface IPortfolioExcelReportStrategy
    {
        ExcelExportResult ExportDailyFollowUpReport(IReadOnlyList<Guarantee> guarantees, IReadOnlyList<WorkflowRequestListItem> requests);
        ExcelExportResult ExportGuaranteePortfolioSummary(IReadOnlyList<Guarantee> guarantees, IReadOnlyList<WorkflowRequestListItem> requests);
    }
}
