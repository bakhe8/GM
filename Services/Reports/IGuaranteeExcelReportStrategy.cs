using System.Collections.Generic;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    internal interface IGuaranteeExcelReportStrategy
    {
        ExcelExportResult ExportGuarantees(IReadOnlyList<Guarantee> guarantees);
        ExcelExportResult ExportGuaranteesByBank(string bank, IReadOnlyList<Guarantee> guarantees);
        ExcelExportResult ExportGuaranteesBySupplier(string supplier, IReadOnlyList<Guarantee> guarantees);
        ExcelExportResult ExportGuaranteesByTemporalStatus(string temporalStatus, IReadOnlyList<Guarantee> guarantees);
        ExcelExportResult ExportGuaranteesByLifecycleStatus(string lifecycleStatus, IReadOnlyList<Guarantee> guarantees);
        ExcelExportResult ExportGuaranteesByType(string guaranteeType, IReadOnlyList<Guarantee> guarantees);
        ExcelExportResult ExportSingleGuaranteeReport(Guarantee guarantee);
        bool ExportSingleGuaranteeReportToPath(Guarantee guarantee, string outputPath);
        ExcelExportResult ExportExpiringSoonGuarantees(IReadOnlyList<Guarantee> guarantees);
        ExcelExportResult ExportExpiredActiveGuarantees(IReadOnlyList<Guarantee> guarantees);
        ExcelExportResult ExportGuaranteeStatisticsByBank(IReadOnlyList<Guarantee> guarantees);
        ExcelExportResult ExportGuaranteeStatisticsBySupplier(IReadOnlyList<Guarantee> guarantees);
        ExcelExportResult ExportGuaranteeVersionCounts(IReadOnlyList<Guarantee> guarantees);
        ExcelExportResult ExportGuaranteesWithoutAttachments(IReadOnlyList<Guarantee> guarantees);
        ExcelExportResult ExportGuaranteeVersionsWithoutAttachments(IReadOnlyList<Guarantee> guarantees);
        ExcelExportResult ExportActivePurchaseOrderOnlyGuarantees(IReadOnlyList<Guarantee> guarantees);
        ExcelExportResult ExportExpiredPurchaseOrderOnlyWithoutExecutedExtension(IReadOnlyList<Guarantee> guarantees);
        bool ExportGuaranteesReportToPath(
            IReadOnlyList<Guarantee> guarantees,
            string reportTitle,
            string reportSubtitle,
            string outputPath);
    }
}
