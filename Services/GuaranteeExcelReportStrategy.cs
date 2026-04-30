using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    internal sealed class GuaranteeExcelReportStrategy : IGuaranteeExcelReportStrategy
    {
        public ExcelExportResult ExportGuarantees(IReadOnlyList<Guarantee> guarantees)
        {
            return ExportGuaranteesReport(
                guarantees,
                "حفظ تقرير السجلات الحالية",
                $"Bank_Guarantees_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "تقرير السجلات الحالية",
                $"يعرض السجلات الحالية كما تظهر في الشاشة. عدد السجلات: {guarantees.Count}");
        }

        public ExcelExportResult ExportGuaranteesByBank(string bank, IReadOnlyList<Guarantee> guarantees)
        {
            return ExportGuaranteesReport(
                guarantees,
                "تصدير الضمانات حسب البنك",
                $"Guarantees_Bank_{ExcelReportSupport.MakeSafeFileName(bank)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                $"تقرير ضمانات البنك: {bank}",
                $"يعرض جميع الضمانات الحالية التابعة للبنك المحدد. عدد السجلات: {guarantees.Count}");
        }

        public ExcelExportResult ExportGuaranteesBySupplier(string supplier, IReadOnlyList<Guarantee> guarantees)
        {
            return ExportGuaranteesReport(
                guarantees,
                "تصدير الضمانات حسب المورد",
                $"Guarantees_Supplier_{ExcelReportSupport.MakeSafeFileName(supplier)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                $"تقرير ضمانات المورد: {supplier}",
                $"يعرض جميع الضمانات الحالية الخاصة بالمورد المحدد. عدد السجلات: {guarantees.Count}");
        }

        public ExcelExportResult ExportGuaranteesByTemporalStatus(string temporalStatus, IReadOnlyList<Guarantee> guarantees)
        {
            return ExportGuaranteesReport(
                guarantees,
                "تصدير الضمانات حسب الحالة الزمنية",
                $"Guarantees_Status_{ExcelReportSupport.MakeSafeFileName(temporalStatus)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                $"تقرير الضمانات حسب الحالة الزمنية: {temporalStatus}",
                $"يعرض جميع الضمانات الحالية المطابقة للحالة الزمنية المحددة. عدد السجلات: {guarantees.Count}");
        }

        public ExcelExportResult ExportGuaranteesByLifecycleStatus(string lifecycleStatus, IReadOnlyList<Guarantee> guarantees)
        {
            return ExportGuaranteesReport(
                guarantees,
                "تصدير الضمانات حسب الحالة التشغيلية",
                $"Guarantees_Lifecycle_{ExcelReportSupport.MakeSafeFileName(lifecycleStatus)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                $"تقرير الضمانات حسب الحالة التشغيلية: {lifecycleStatus}",
                $"يعرض جميع الضمانات الحالية المطابقة للحالة التشغيلية المحددة. عدد السجلات: {guarantees.Count}");
        }

        public ExcelExportResult ExportGuaranteesByType(string guaranteeType, IReadOnlyList<Guarantee> guarantees)
        {
            return ExportGuaranteesReport(
                guarantees,
                "تصدير الضمانات حسب النوع",
                $"Guarantees_Type_{ExcelReportSupport.MakeSafeFileName(guaranteeType)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                $"تقرير الضمانات حسب النوع: {guaranteeType}",
                $"يعرض جميع الضمانات الحالية المطابقة لنوع الضمان المحدد. عدد السجلات: {guarantees.Count}");
        }

        public ExcelExportResult ExportSingleGuaranteeReport(Guarantee guarantee)
        {
            try
            {
                var saveFileDialog = ExcelReportSupport.BuildSaveDialog(
                    $"Guarantee_{ExcelReportSupport.MakeSafeFileName(guarantee.GuaranteeNo)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                    "حفظ تقرير الضمان");

                if (saveFileDialog.ShowDialog() != true)
                {
                    return ExcelExportResult.Cancelled;
                }

                return ExportSingleGuaranteeReportToPath(guarantee, saveFileDialog.FileName)
                    ? ExcelExportResult.Saved(saveFileDialog.FileName)
                    : ExcelExportResult.Cancelled;
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "Excel.ExportSingleGuaranteeReport",
                    "تعذر تصدير تقرير الضمان إلى Excel.");
            }
        }

        public bool ExportSingleGuaranteeReportToPath(Guarantee guarantee, string outputPath)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var detailsSheet = workbook.Worksheets.Add("بيانات الضمان");
                detailsSheet.SetRightToLeft(true);

                ExcelReportSupport.WriteTitle(detailsSheet, 1, 13, "تقرير الضمان", $"رقم الضمان: {guarantee.GuaranteeNo}");

                int row = 4;
                ExcelReportSupport.WriteSummaryPair(detailsSheet, row++, "رقم الضمان", guarantee.GuaranteeNo, "الإصدار", guarantee.VersionLabel);
                ExcelReportSupport.WriteSummaryPair(detailsSheet, row++, "المورد", guarantee.Supplier, "البنك", guarantee.Bank);
                ExcelReportSupport.WriteSummaryPair(detailsSheet, row++, "المبلغ", guarantee.Amount.ToString("N2"), "النوع", ExcelReportSupport.ValueOrDash(guarantee.GuaranteeType));
                ExcelReportSupport.WriteSummaryPair(detailsSheet, row++, "تاريخ الانتهاء", guarantee.ExpiryDate.ToString("yyyy-MM-dd"), "الحالة الزمنية", guarantee.StatusLabel);
                ExcelReportSupport.WriteSummaryPair(detailsSheet, row++, "الحالة التشغيلية", guarantee.LifecycleStatusLabel, "المرفقات", guarantee.AttachmentCount.ToString());
                ExcelReportSupport.WriteSummaryPair(detailsSheet, row++, "المستفيد", ExcelReportSupport.ValueOrDash(guarantee.Beneficiary), "نوع المرجع", guarantee.ReferenceTypeLabel);
                ExcelReportSupport.WriteSummaryPair(detailsSheet, row++, "رقم المرجع", ExcelReportSupport.ValueOrDash(guarantee.ReferenceNumber), "معرّف السجل", guarantee.Id.ToString());
                ExcelReportSupport.WriteSummaryPair(detailsSheet, row++, "تاريخ الإدخال", guarantee.CreatedAt.ToString("yyyy-MM-dd HH:mm"), "اكتمال المرجع", guarantee.HasReference ? "مكتمل" : "غير مكتمل");

                detailsSheet.Cell(row, 1).Value = "الملاحظات";
                ExcelReportSupport.ApplySummaryLabelStyle(detailsSheet.Cell(row, 1));
                detailsSheet.Range(row, 2, row, 13).Merge();
                detailsSheet.Cell(row, 2).Value = ExcelReportSupport.ValueOrDash(guarantee.Notes);
                ExcelReportSupport.ApplySummaryValueStyle(detailsSheet.Cell(row, 2));
                detailsSheet.Cell(row, 2).Style.Alignment.WrapText = true;
                detailsSheet.Row(row).Height = 42;

                var attachmentsSheet = workbook.Worksheets.Add("المرفقات");
                attachmentsSheet.SetRightToLeft(true);
                ExcelReportSupport.WriteTitle(attachmentsSheet, 1, 6, "مرفقات الضمان", $"رقم الضمان: {guarantee.GuaranteeNo}");

                string[] headers = { "اسم الملف", "نوع المستند", "الامتداد", "تاريخ الإضافة", "الحالة", "الاسم المحفوظ" };
                ExcelReportSupport.WriteHeaderRow(attachmentsSheet, 4, headers);

                if (guarantee.Attachments.Any())
                {
                    for (int i = 0; i < guarantee.Attachments.Count; i++)
                    {
                        var attachment = guarantee.Attachments[i];
                        int attachmentRow = i + 5;

                        attachmentsSheet.Cell(attachmentRow, 1).Value = attachment.OriginalFileName;
                        attachmentsSheet.Cell(attachmentRow, 2).Value = attachment.DocumentTypeLabel;
                        attachmentsSheet.Cell(attachmentRow, 3).Value = attachment.FileExtension;
                        attachmentsSheet.Cell(attachmentRow, 4).Value = attachment.UploadedAt;
                        attachmentsSheet.Cell(attachmentRow, 4).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
                        attachmentsSheet.Cell(attachmentRow, 5).Value = attachment.Exists ? "موجود" : "غير موجود";
                        attachmentsSheet.Cell(attachmentRow, 6).Value = attachment.SavedFileName;

                        attachmentsSheet.Cell(attachmentRow, 5).Style.Font.FontColor =
                            attachment.Exists ? XLColor.FromHtml("#006847") : XLColor.FromHtml("#111111");
                    }

                    ExcelReportSupport.ApplyTableStyle(attachmentsSheet, 4, guarantee.Attachments.Count + 4, headers.Length);
                }
                else
                {
                    attachmentsSheet.Range(5, 1, 5, 6).Merge();
                    attachmentsSheet.Cell(5, 1).Value = "لا توجد مرفقات محفوظة لهذا الضمان.";
                    attachmentsSheet.Cell(5, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    attachmentsSheet.Cell(5, 1).Style.Font.FontColor = XLColor.FromHtml("#666666");
                }

                detailsSheet.Columns().AdjustToContents();
                attachmentsSheet.Columns().AdjustToContents();
                return ExcelReportSupport.SaveWorkbook(workbook, outputPath).Exported;
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "Excel.ExportSingleGuaranteeReportToPath",
                    "تعذر إنشاء ملف تقرير الضمان.");
            }
        }

        public ExcelExportResult ExportExpiringSoonGuarantees(IReadOnlyList<Guarantee> guarantees)
        {
            var filteredGuarantees = guarantees
                .Where(g => g.IsExpiringSoon)
                .OrderBy(g => g.ExpiryDate)
                .ThenBy(g => g.GuaranteeNo)
                .ToList();

            return ExportGuaranteesReport(
                filteredGuarantees,
                "تصدير الضمانات القريبة من الانتهاء",
                $"Guarantees_ExpiringSoon_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "الضمانات القريبة من الانتهاء",
                $"يعرض الضمانات التي تقترب من نهاية مدتها الزمنية. عدد السجلات: {filteredGuarantees.Count}");
        }

        public ExcelExportResult ExportExpiredActiveGuarantees(IReadOnlyList<Guarantee> guarantees)
        {
            var filteredGuarantees = guarantees
                .Where(g => g.NeedsExpiryFollowUp)
                .OrderBy(g => g.ExpiryDate)
                .ThenBy(g => g.GuaranteeNo)
                .ToList();

            return ExportGuaranteesReport(
                filteredGuarantees,
                "تصدير الضمانات المنتهية التي تحتاج إفراج",
                $"Guarantees_ExpiredFollowUp_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "الضمانات المنتهية التي تحتاج إفراج/إعادة",
                $"يعرض الضمانات المنتهية زمنيًا التي ما زالت بحاجة إلى إفراج أو إعادة للبنك. عدد السجلات: {filteredGuarantees.Count}");
        }

        public ExcelExportResult ExportGuaranteeStatisticsByBank(IReadOnlyList<Guarantee> guarantees)
        {
            var rows = ExcelReportSupport.BuildGuaranteeStatisticsRows(guarantees, guarantee => guarantee.Bank);

            return ExportGuaranteeStatisticsReport(
                rows,
                "حفظ الإحصاء حسب البنك",
                $"Guarantee_Stats_By_Bank_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "إحصاء الضمانات حسب البنك",
                $"يعرض توزيع الضمانات الحالية حسب البنك. عدد البنوك: {rows.Count}",
                "البنك");
        }

        public ExcelExportResult ExportGuaranteeStatisticsBySupplier(IReadOnlyList<Guarantee> guarantees)
        {
            var rows = ExcelReportSupport.BuildGuaranteeStatisticsRows(guarantees, guarantee => guarantee.Supplier);

            return ExportGuaranteeStatisticsReport(
                rows,
                "حفظ الإحصاء حسب المورد",
                $"Guarantee_Stats_By_Supplier_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "إحصاء الضمانات حسب المورد",
                $"يعرض توزيع الضمانات الحالية حسب المورد. عدد الموردين: {rows.Count}",
                "المورد");
        }

        public ExcelExportResult ExportGuaranteeVersionCounts(IReadOnlyList<Guarantee> guarantees)
        {
            try
            {
                if (guarantees.Count == 0)
                {
                    return ExcelExportResult.Cancelled;
                }

                var saveFileDialog = ExcelReportSupport.BuildSaveDialog(
                    $"Guarantee_Version_Counts_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                    "حفظ تقرير عدد الإصدارات");

                if (saveFileDialog.ShowDialog() != true)
                {
                    return ExcelExportResult.Cancelled;
                }

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("عدد الإصدارات");
                worksheet.SetRightToLeft(true);
                ExcelReportSupport.WriteTitle(worksheet, 1, 8, "عدد الإصدارات لكل ضمان", $"يعرض عدد الإصدارات الحالية لكل ضمان. عدد السجلات: {guarantees.Count}");
                ExcelReportSupport.WriteHeaderRow(worksheet, 4, new[]
                {
                    "رقم الضمان",
                    "المورد",
                    "البنك",
                    "عدد الإصدارات",
                    "الإصدار الحالي",
                    "الحالة الزمنية",
                    "الحالة التشغيلية",
                    "تاريخ الإدخال"
                });

                var orderedGuarantees = guarantees
                    .OrderByDescending(g => g.VersionNumber)
                    .ThenBy(g => g.GuaranteeNo)
                    .ToList();

                for (int i = 0; i < orderedGuarantees.Count; i++)
                {
                    Guarantee guarantee = orderedGuarantees[i];
                    int row = i + 5;

                    worksheet.Cell(row, 1).Value = guarantee.GuaranteeNo;
                    worksheet.Cell(row, 2).Value = guarantee.Supplier;
                    worksheet.Cell(row, 3).Value = guarantee.Bank;
                    worksheet.Cell(row, 4).Value = guarantee.VersionNumber;
                    worksheet.Cell(row, 5).Value = guarantee.VersionLabel;
                    worksheet.Cell(row, 6).Value = guarantee.StatusLabel;
                    worksheet.Cell(row, 7).Value = guarantee.LifecycleStatusLabel;
                    worksheet.Cell(row, 8).Value = guarantee.CreatedAt;
                    worksheet.Cell(row, 8).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";

                    worksheet.Cell(row, 6).Style.Font.FontColor = guarantee.StatusLabel switch
                    {
                        "منتهي" => XLColor.FromHtml("#111111"),
                        "قريب الانتهاء" => XLColor.FromHtml("#666666"),
                        _ => XLColor.FromHtml("#006847")
                    };

                    worksheet.Cell(row, 7).Style.Font.FontColor = ExcelReportSupport.GetLifecycleStatusColor(guarantee.LifecycleStatus);
                }

                ExcelReportSupport.ApplyTableStyle(worksheet, 4, orderedGuarantees.Count + 4, 8);
                worksheet.Columns().AdjustToContents();
                worksheet.SheetView.FreezeRows(4);
                return ExcelReportSupport.SaveWorkbook(workbook, saveFileDialog.FileName);
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "Excel.ExportGuaranteeVersionCounts",
                    "تعذر تصدير تقرير عدد الإصدارات.");
            }
        }

        public ExcelExportResult ExportGuaranteesWithoutAttachments(IReadOnlyList<Guarantee> guarantees)
        {
            var filteredGuarantees = guarantees
                .Where(g => g.AttachmentCount == 0)
                .OrderByDescending(g => g.CreatedAt)
                .ThenBy(g => g.GuaranteeNo)
                .ToList();

            return ExportGuaranteesReport(
                filteredGuarantees,
                "حفظ تقرير الضمانات الحالية بلا مرفقات",
                $"Guarantees_Without_Attachments_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "الضمانات الحالية بلا مرفقات",
                $"يعرض السجلات الحالية التي لا تحتوي على أي مرفقات محفوظة. عدد السجلات: {filteredGuarantees.Count}");
        }

        public ExcelExportResult ExportGuaranteeVersionsWithoutAttachments(IReadOnlyList<Guarantee> guarantees)
        {
            var filteredGuarantees = guarantees
                .Where(g => g.AttachmentCount == 0)
                .OrderByDescending(g => g.CreatedAt)
                .ThenBy(g => g.GuaranteeNo)
                .ThenByDescending(g => g.VersionNumber)
                .ToList();

            return ExportGuaranteesReport(
                filteredGuarantees,
                "حفظ تقرير جميع الإصدارات بلا مرفقات",
                $"Guarantee_Versions_Without_Attachments_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "جميع الإصدارات بلا مرفقات",
                $"يعرض كل إصدار في السجل الكامل لا يحتوي على أي مرفقات محفوظة. عدد الإصدارات: {filteredGuarantees.Count}");
        }

        public ExcelExportResult ExportActivePurchaseOrderOnlyGuarantees(IReadOnlyList<Guarantee> guarantees)
        {
            var filteredGuarantees = guarantees
                .Where(g => !g.IsExpired &&
                            g.LifecycleStatus == GuaranteeLifecycleStatus.Active &&
                            g.IsPurchaseOrderReference)
                .OrderBy(g => g.ExpiryDate)
                .ThenBy(g => g.GuaranteeNo)
                .ToList();

            return ExportGuaranteesReport(
                filteredGuarantees,
                "حفظ تقرير الضمانات السارية الخاصة بأوامر الشراء فقط",
                $"Guarantees_PO_Only_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "الضمانات السارية - أوامر الشراء فقط",
                $"يعرض الضمانات السارية ذات الحالة التشغيلية النشطة والمرتبطة بأوامر الشراء فقط. عدد السجلات: {filteredGuarantees.Count}");
        }

        public ExcelExportResult ExportExpiredPurchaseOrderOnlyWithoutExecutedExtension(IReadOnlyList<Guarantee> guarantees)
        {
            var filteredGuarantees = guarantees
                .OrderBy(g => g.ExpiryDate)
                .ThenBy(g => g.GuaranteeNo)
                .ToList();

            return ExportGuaranteesReport(
                filteredGuarantees,
                "حفظ تقرير ضمانات أوامر الشراء المنتهية التي تحتاج إفراج",
                $"Guarantees_PO_Expired_Needing_Release_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                "ضمانات أوامر الشراء المنتهية التي تحتاج إفراج",
                $"يعرض الضمانات المرتبطة بأوامر الشراء فقط والمنتهية زمنيًا، والتي تحتاج إفراجًا أو إعادة للبنك. عدد السجلات: {filteredGuarantees.Count}");
        }

        public bool ExportGuaranteesReportToPath(
            IReadOnlyList<Guarantee> guarantees,
            string reportTitle,
            string reportSubtitle,
            string outputPath)
        {
            if (guarantees.Count == 0)
            {
                return false;
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("الضمانات");
            worksheet.SetRightToLeft(true);

            ExcelReportSupport.WriteGuaranteesWorksheet(worksheet, guarantees, reportTitle, reportSubtitle);
            return ExcelReportSupport.SaveWorkbook(workbook, outputPath).Exported;
        }

        private ExcelExportResult ExportGuaranteesReport(
            IReadOnlyList<Guarantee> guarantees,
            string saveDialogTitle,
            string defaultFileName,
            string reportTitle,
            string reportSubtitle)
        {
            try
            {
                if (guarantees.Count == 0)
                {
                    return ExcelExportResult.Cancelled;
                }

                var saveFileDialog = ExcelReportSupport.BuildSaveDialog(defaultFileName, saveDialogTitle);

                if (saveFileDialog.ShowDialog() != true)
                {
                    return ExcelExportResult.Cancelled;
                }

                return ExportGuaranteesReportToPath(guarantees, reportTitle, reportSubtitle, saveFileDialog.FileName)
                    ? ExcelExportResult.Saved(saveFileDialog.FileName)
                    : ExcelExportResult.Cancelled;
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "Excel.ExportGuaranteesReport",
                    "تعذر تصدير تقرير الضمانات إلى Excel.");
            }
        }

        private ExcelExportResult ExportGuaranteeStatisticsReport(
            IReadOnlyList<GuaranteeStatisticRow> rows,
            string saveDialogTitle,
            string defaultFileName,
            string reportTitle,
            string reportSubtitle,
            string firstColumnHeader)
        {
            try
            {
                if (rows.Count == 0)
                {
                    return ExcelExportResult.Cancelled;
                }

                var saveFileDialog = ExcelReportSupport.BuildSaveDialog(defaultFileName, saveDialogTitle);
                if (saveFileDialog.ShowDialog() != true)
                {
                    return ExcelExportResult.Cancelled;
                }

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("الإحصاء");
                worksheet.SetRightToLeft(true);
                ExcelReportSupport.WriteGuaranteeStatisticsWorksheet(worksheet, rows, reportTitle, reportSubtitle, firstColumnHeader);
                return ExcelReportSupport.SaveWorkbook(workbook, saveFileDialog.FileName);
            }
            catch (Exception ex)
            {
                throw OperationFailure.LogAndWrap(
                    ex,
                    "Excel.ExportGuaranteeStatisticsReport",
                    "تعذر تصدير تقرير الإحصاءات إلى Excel.");
            }
        }
    }
}
