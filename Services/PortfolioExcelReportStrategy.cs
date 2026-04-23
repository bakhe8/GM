using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    internal sealed class PortfolioExcelReportStrategy : IPortfolioExcelReportStrategy
    {
        public ExcelExportResult ExportDailyFollowUpReport(IReadOnlyList<Guarantee> guarantees, IReadOnlyList<WorkflowRequestListItem> requests)
        {
            try
            {
                var expiringSoon = guarantees
                    .Where(g => g.IsExpiringSoon)
                    .OrderBy(g => g.ExpiryDate)
                    .ThenBy(g => g.GuaranteeNo)
                    .ToList();

                var expiredActive = guarantees
                    .Where(g => g.IsExpired && g.LifecycleStatus == GuaranteeLifecycleStatus.Active)
                    .OrderBy(g => g.ExpiryDate)
                    .ThenBy(g => g.GuaranteeNo)
                    .ToList();

                var pendingRequests = requests
                    .Where(item => item.Request.Status == RequestStatus.Pending)
                    .OrderBy(item => item.Request.RequestDate)
                    .ThenBy(item => item.GuaranteeNo)
                    .ToList();

                var executedWithoutResponse = requests
                    .Where(item => item.Request.Status == RequestStatus.Executed && !item.Request.HasResponseDocument)
                    .OrderByDescending(item => item.Request.ResponseRecordedAt)
                    .ThenBy(item => item.GuaranteeNo)
                    .ToList();

                if (!expiringSoon.Any() && !expiredActive.Any() && !pendingRequests.Any() && !executedWithoutResponse.Any())
                {
                    return ExcelExportResult.Cancelled;
                }

                var saveFileDialog = ExcelReportSupport.BuildSaveDialog(
                    $"Daily_Follow_Up_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                    "حفظ تقرير المتابعة اليومي");

                if (saveFileDialog.ShowDialog() != true)
                {
                    return ExcelExportResult.Cancelled;
                }

                using var workbook = new XLWorkbook();

                var summarySheet = workbook.Worksheets.Add("الملخص");
                summarySheet.SetRightToLeft(true);
                ExcelReportSupport.WriteTitle(summarySheet, 1, 4, "تقرير المتابعة اليومي", "يجمع أهم العناصر التي تحتاج متابعة تشغيلية الآن.");
                ExcelReportSupport.WriteHeaderRow(summarySheet, 4, new[] { "البند", "العدد", "الوصف", "الإجراء المقترح" });

                string[,] rows =
                {
                    { "قريب الانتهاء", expiringSoon.Count.ToString("N0"), "ضمانات تقترب من نهاية مدتها الزمنية.", "المراجعة والتواصل قبل الانتهاء." },
                    { "منتهي وما زال نشطًا", expiredActive.Count.ToString("N0"), "ضمانات منتهية زمنيًا ولكن حالتها التشغيلية ما زالت نشطة.", "مراجعة الإغلاق أو طلب الإجراء المناسب." },
                    { "طلبات معلقة", pendingRequests.Count.ToString("N0"), "طلبات لم يسجل لها رد بنك بعد.", "المتابعة مع البنك أو الجهة المعنية." },
                    { "منفذ بلا مستند رد بنك", executedWithoutResponse.Count.ToString("N0"), "طلبات منفذة لم يُحفظ لها مستند رد بنك.", "استكمال مستندات الإثبات عند الحاجة." }
                };

                for (int i = 0; i < rows.GetLength(0); i++)
                {
                    int row = i + 5;
                    summarySheet.Cell(row, 1).Value = rows[i, 0];
                    summarySheet.Cell(row, 2).Value = rows[i, 1];
                    summarySheet.Cell(row, 3).Value = rows[i, 2];
                    summarySheet.Cell(row, 4).Value = rows[i, 3];
                }

                ExcelReportSupport.ApplyTableStyle(summarySheet, 4, 8, 4);
                summarySheet.Columns().AdjustToContents();

                if (expiringSoon.Any())
                {
                    var sheet = workbook.Worksheets.Add("قريب الانتهاء");
                    sheet.SetRightToLeft(true);
                    ExcelReportSupport.WriteGuaranteesWorksheet(sheet, expiringSoon, "الضمانات القريبة من الانتهاء", $"عدد السجلات: {expiringSoon.Count}");
                }

                if (expiredActive.Any())
                {
                    var sheet = workbook.Worksheets.Add("منتهي ونشط");
                    sheet.SetRightToLeft(true);
                    ExcelReportSupport.WriteGuaranteesWorksheet(sheet, expiredActive, "المنتهي زمنيًا وما زال نشطًا", $"عدد السجلات: {expiredActive.Count}");
                }

                if (pendingRequests.Any())
                {
                    var sheet = workbook.Worksheets.Add("طلبات معلقة");
                    sheet.SetRightToLeft(true);
                    ExcelReportSupport.WriteWorkflowRequestsWorksheet(sheet, pendingRequests, "الطلبات المعلقة", $"عدد الطلبات: {pendingRequests.Count}");
                }

                if (executedWithoutResponse.Any())
                {
                    var sheet = workbook.Worksheets.Add("بلا مستند رد بنك");
                    sheet.SetRightToLeft(true);
                    ExcelReportSupport.WriteWorkflowRequestsWorksheet(sheet, executedWithoutResponse, "طلبات منفذة بلا مستند رد بنك", $"عدد الطلبات: {executedWithoutResponse.Count}");
                }

                return ExcelReportSupport.SaveWorkbook(workbook, saveFileDialog.FileName);
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "Excel.ExportDailyFollowUpReport",
                    "تعذر تصدير تقرير المتابعة اليومي إلى Excel.");
            }
        }

        public ExcelExportResult ExportGuaranteePortfolioSummary(IReadOnlyList<Guarantee> guarantees, IReadOnlyList<WorkflowRequestListItem> requests)
        {
            try
            {
                if (guarantees.Count == 0 && requests.Count == 0)
                {
                    return ExcelExportResult.Cancelled;
                }

                var saveFileDialog = ExcelReportSupport.BuildSaveDialog(
                    $"Guarantee_Portfolio_Summary_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                    "حفظ ملخص المحفظة");

                if (saveFileDialog.ShowDialog() != true)
                {
                    return ExcelExportResult.Cancelled;
                }

                using var workbook = new XLWorkbook();

                var overviewSheet = workbook.Worksheets.Add("الملخص العام");
                overviewSheet.SetRightToLeft(true);
                ExcelReportSupport.WriteTitle(overviewSheet, 1, 4, "ملخص محفظة الضمانات", "يعرض أهم المؤشرات العامة للضمانات والطلبات.");
                ExcelReportSupport.WriteHeaderRow(overviewSheet, 4, new[] { "المؤشر", "القيمة", "الوصف", "ملاحظات" });

                int overviewRow = 5;
                ExcelReportSupport.WriteOverviewRow(overviewSheet, overviewRow++, "عدد الضمانات الحالية", guarantees.Count.ToString("N0"), "إجمالي السجلات الحالية", "---");
                ExcelReportSupport.WriteOverviewRow(overviewSheet, overviewRow++, "إجمالي المبالغ", guarantees.Sum(g => g.Amount).ToString("N2"), "مجموع مبالغ الضمانات الحالية", "---");
                ExcelReportSupport.WriteOverviewRow(overviewSheet, overviewRow++, "قريب الانتهاء", guarantees.Count(g => g.IsExpiringSoon).ToString("N0"), "ضمانات تقترب من نهاية مدتها الزمنية", "---");
                ExcelReportSupport.WriteOverviewRow(overviewSheet, overviewRow++, "منتهي زمنيًا", guarantees.Count(g => g.IsExpired).ToString("N0"), "ضمانات منتهية زمنيًا", "---");
                ExcelReportSupport.WriteOverviewRow(overviewSheet, overviewRow++, "نشط تشغيليًا", guarantees.Count(g => g.LifecycleStatus == GuaranteeLifecycleStatus.Active).ToString("N0"), "الضمانات ذات الحالة التشغيلية النشطة", "---");
                ExcelReportSupport.WriteOverviewRow(overviewSheet, overviewRow++, "مفرج", guarantees.Count(g => g.LifecycleStatus == GuaranteeLifecycleStatus.Released).ToString("N0"), "ضمانات مفرج عنها", "---");
                ExcelReportSupport.WriteOverviewRow(overviewSheet, overviewRow++, "مسيّل", guarantees.Count(g => g.LifecycleStatus == GuaranteeLifecycleStatus.Liquidated).ToString("N0"), "ضمانات جرى تسييلها أو مصادرتها", "---");
                ExcelReportSupport.WriteOverviewRow(overviewSheet, overviewRow++, "مستبدل", guarantees.Count(g => g.LifecycleStatus == GuaranteeLifecycleStatus.Replaced).ToString("N0"), "ضمانات مستبدلة", "---");
                ExcelReportSupport.WriteOverviewRow(overviewSheet, overviewRow++, "مغلق", guarantees.Count(g => g.LifecycleStatus == GuaranteeLifecycleStatus.Closed).ToString("N0"), "ضمانات مغلقة", "---");
                ExcelReportSupport.WriteOverviewRow(overviewSheet, overviewRow++, "الطلبات قيد الانتظار", requests.Count(r => r.Request.Status == RequestStatus.Pending).ToString("N0"), "طلبات لم يسجل لها رد بنك بعد", "---");
                ExcelReportSupport.WriteOverviewRow(overviewSheet, overviewRow++, "الطلبات المنفذة", requests.Count(r => r.Request.Status == RequestStatus.Executed).ToString("N0"), "طلبات أثرت على السجلات أو أغلقت بالتنفيذ", "---");
                ExcelReportSupport.WriteOverviewRow(overviewSheet, overviewRow++, "منفذ بلا مستند رد بنك", requests.Count(r => r.Request.Status == RequestStatus.Executed && !r.Request.HasResponseDocument).ToString("N0"), "حالات تستحق مراجعة حوكمة", "---");
                ExcelReportSupport.ApplyTableStyle(overviewSheet, 4, overviewRow - 1, 4);
                overviewSheet.Columns().AdjustToContents();

                var topBanks = ExcelReportSupport.BuildGuaranteeStatisticsRows(guarantees, guarantee => guarantee.Bank)
                    .OrderByDescending(row => row.TotalAmount)
                    .ThenByDescending(row => row.Count)
                    .Take(10)
                    .ToList();
                var topBanksSheet = workbook.Worksheets.Add("أعلى البنوك");
                topBanksSheet.SetRightToLeft(true);
                ExcelReportSupport.WriteGuaranteeStatisticsWorksheet(topBanksSheet, topBanks, "أعلى البنوك", "يعرض أعلى البنوك من حيث إجمالي مبالغ الضمانات.", "البنك");

                var topSuppliers = ExcelReportSupport.BuildGuaranteeStatisticsRows(guarantees, guarantee => guarantee.Supplier)
                    .OrderByDescending(row => row.TotalAmount)
                    .ThenByDescending(row => row.Count)
                    .Take(10)
                    .ToList();
                var topSuppliersSheet = workbook.Worksheets.Add("أعلى الموردين");
                topSuppliersSheet.SetRightToLeft(true);
                ExcelReportSupport.WriteGuaranteeStatisticsWorksheet(topSuppliersSheet, topSuppliers, "أعلى الموردين", "يعرض أعلى الموردين من حيث إجمالي مبالغ الضمانات.", "المورد");

                return ExcelReportSupport.SaveWorkbook(workbook, saveFileDialog.FileName);
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "Excel.ExportGuaranteePortfolioSummary",
                    "تعذر تصدير ملخص المحفظة إلى Excel.");
            }
        }
    }
}
