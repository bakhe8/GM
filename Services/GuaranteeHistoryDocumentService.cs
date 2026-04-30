using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ClosedXML.Excel;
using GuaranteeManager.Models;
using Microsoft.Win32;

namespace GuaranteeManager.Services
{
    public sealed class GuaranteeHistoryDocumentService : IGuaranteeHistoryDocumentService
    {
        public string? LastOutputPath { get; private set; }

        public bool ExportHistoryToExcel(Guarantee guarantee, IReadOnlyList<Guarantee> history, IReadOnlyList<WorkflowRequest> requests)
        {
            ResetLastOutputPath();

            List<Guarantee> orderedHistory = OrderHistory(history);
            List<WorkflowRequest> orderedRequests = OrderRequests(requests);
            if (orderedHistory.Count == 0 && orderedRequests.Count == 0)
            {
                return false;
            }

            Guarantee summaryGuarantee = ResolveSummaryGuarantee(guarantee, orderedHistory);
            SaveFileDialog dialog = ExcelReportSupport.BuildSaveDialog(
                BuildOutputFileName(summaryGuarantee),
                $"تصدير سجل الضمان {summaryGuarantee.GuaranteeNo}");

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FileName))
            {
                return false;
            }

            return WriteHistoryWorkbookToPath(summaryGuarantee, orderedHistory, orderedRequests, dialog.FileName);
        }

        internal bool ExportHistoryToExcelToPath(
            Guarantee guarantee,
            IReadOnlyList<Guarantee> history,
            IReadOnlyList<WorkflowRequest> requests,
            string outputPath)
        {
            ResetLastOutputPath();
            List<Guarantee> orderedHistory = OrderHistory(history);
            List<WorkflowRequest> orderedRequests = OrderRequests(requests);
            if (orderedHistory.Count == 0 && orderedRequests.Count == 0)
            {
                return false;
            }

            Guarantee summaryGuarantee = ResolveSummaryGuarantee(guarantee, orderedHistory);
            return WriteHistoryWorkbookToPath(summaryGuarantee, orderedHistory, orderedRequests, outputPath);
        }

        private bool WriteHistoryWorkbookToPath(
            Guarantee summaryGuarantee,
            IReadOnlyList<Guarantee> orderedHistory,
            IReadOnlyList<WorkflowRequest> orderedRequests,
            string outputPath)
        {
            using var workbook = new XLWorkbook();
            WriteOverviewWorksheet(workbook, summaryGuarantee, orderedHistory, orderedRequests);
            WriteHealthWorksheet(workbook, summaryGuarantee, orderedHistory, orderedRequests);
            WriteVersionsWorksheet(workbook, summaryGuarantee, orderedHistory);
            WriteRequestsWorksheet(workbook, summaryGuarantee, orderedHistory, orderedRequests);

            ExcelExportResult result = ExcelReportSupport.SaveWorkbook(workbook, outputPath);
            if (!result.Exported)
            {
                return false;
            }

            LastOutputPath = outputPath;
            return true;
        }

        public bool PrintHistory(Guarantee guarantee, IReadOnlyList<Guarantee> history, IReadOnlyList<WorkflowRequest> requests)
        {
            ResetLastOutputPath();

            List<Guarantee> orderedHistory = OrderHistory(history);
            List<WorkflowRequest> orderedRequests = OrderRequests(requests);
            if (orderedHistory.Count == 0 && orderedRequests.Count == 0)
            {
                return false;
            }

            Guarantee summaryGuarantee = ResolveSummaryGuarantee(guarantee, orderedHistory);

            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true)
            {
                return false;
            }

            FlowDocument document = BuildPrintDocument(summaryGuarantee, orderedHistory, orderedRequests);
            if (!double.IsNaN(printDialog.PrintableAreaWidth) && printDialog.PrintableAreaWidth > 0)
            {
                document.PageWidth = printDialog.PrintableAreaWidth;
            }

            if (!double.IsNaN(printDialog.PrintableAreaHeight) && printDialog.PrintableAreaHeight > 0)
            {
                document.PageHeight = printDialog.PrintableAreaHeight;
            }

            document.ColumnWidth = Math.Max(1, document.PageWidth - document.PagePadding.Left - document.PagePadding.Right);
            ((IDocumentPaginatorSource)document).DocumentPaginator.PageSize = new Size(document.PageWidth, document.PageHeight);
            printDialog.PrintDocument(
                ((IDocumentPaginatorSource)document).DocumentPaginator,
                $"سجل الضمان - {summaryGuarantee.GuaranteeNo}");
            return true;
        }

        private static List<Guarantee> OrderHistory(IReadOnlyList<Guarantee> history)
        {
            return history
                .OrderByDescending(item => item.VersionNumber)
                .ThenByDescending(item => item.CreatedAt)
                .ToList();
        }

        private static List<WorkflowRequest> OrderRequests(IReadOnlyList<WorkflowRequest> requests)
        {
            return requests
                .OrderByDescending(item => item.RequestDate)
                .ThenByDescending(item => item.SequenceNumber)
                .ToList();
        }

        private static Guarantee ResolveSummaryGuarantee(Guarantee fallback, IReadOnlyList<Guarantee> orderedHistory)
        {
            return orderedHistory.FirstOrDefault(item => item.IsCurrent)
                   ?? orderedHistory.FirstOrDefault()
                   ?? fallback;
        }

        private static string BuildOutputFileName(Guarantee guarantee)
        {
            string safeGuaranteeNo = ExcelReportSupport.MakeSafeFileName(guarantee.GuaranteeNo);
            return $"GuaranteeHistory_{safeGuaranteeNo}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        }

        private static void WriteOverviewWorksheet(
            XLWorkbook workbook,
            Guarantee guarantee,
            IReadOnlyList<Guarantee> orderedHistory,
            IReadOnlyList<WorkflowRequest> orderedRequests)
        {
            IXLWorksheet worksheet = workbook.Worksheets.Add("ملخص السجل");
            worksheet.RightToLeft = true;

            Guarantee current = ResolveSummaryGuarantee(guarantee, orderedHistory);
            string supplier = ExcelReportSupport.ValueOrDash(current.Supplier);
            int pendingRequests = orderedRequests.Count(item => item.Status == RequestStatus.Pending);
            int totalAttachments = orderedHistory.Sum(item => item.AttachmentCount);
            string firstCreated = orderedHistory.LastOrDefault()?.CreatedAt.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "---";
            string lastUpdated = orderedHistory.FirstOrDefault()?.CreatedAt.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "---";

            ExcelReportSupport.WriteTitle(
                worksheet,
                1,
                8,
                $"سجل الضمان {current.GuaranteeNo}",
                $"{supplier} | {current.Bank}");

            ExcelReportSupport.WriteSummaryPair(
                worksheet,
                4,
                "عدد الإصدارات",
                orderedHistory.Count.ToString("N0", CultureInfo.InvariantCulture),
                "عدد الطلبات",
                orderedRequests.Count.ToString("N0", CultureInfo.InvariantCulture));
            ExcelReportSupport.WriteSummaryPair(
                worksheet,
                5,
                "الإصدار الحالي",
                current.VersionLabel,
                "آخر تحديث",
                lastUpdated);
            ExcelReportSupport.WriteSummaryPair(
                worksheet,
                6,
                "الحالة الزمنية",
                current.StatusLabel,
                "الحالة التشغيلية",
                current.LifecycleStatusLabel);

            string[] headers = { "المؤشر", "القيمة", "الوصف", "ملاحظات" };
            ExcelReportSupport.WriteHeaderRow(worksheet, 8, headers);

            int row = 9;
            ExcelReportSupport.WriteOverviewRow(worksheet, row++, "رقم الضمان", current.GuaranteeNo, "المرجع الرئيسي", BuildReferenceSummary(current));
            ExcelReportSupport.WriteOverviewRow(worksheet, row++, "المورد", supplier, "البنك", current.Bank);
            ExcelReportSupport.WriteOverviewRow(worksheet, row++, "نوع الضمان", ExcelReportSupport.ValueOrDash(current.GuaranteeType), "نوع المرجع", current.ReferenceTypeLabel);
            ExcelReportSupport.WriteOverviewRow(worksheet, row++, "القيمة الحالية", $"{current.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال", "تاريخ الانتهاء", current.ExpiryDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture));
            ExcelReportSupport.WriteOverviewRow(worksheet, row++, "الطلبات المعلقة", pendingRequests.ToString("N0", CultureInfo.InvariantCulture), "إجمالي المرفقات", totalAttachments.ToString("N0", CultureInfo.InvariantCulture));
            ExcelReportSupport.WriteOverviewRow(worksheet, row++, "أول إنشاء", firstCreated, "آخر تحديث", lastUpdated);
            ExcelReportSupport.WriteOverviewRow(worksheet, row, "ملاحظات الإصدار الحالي", ExcelReportSupport.ValueOrDash(current.Notes), "وصف السجل", "يشمل جميع الإصدارات والطلبات المرتبطة بالسلسلة نفسها.");

            ExcelReportSupport.ApplyTableStyle(worksheet, 8, row, headers.Length);
            worksheet.Columns().AdjustToContents();
            worksheet.SheetView.FreezeRows(8);
        }

        private static void WriteVersionsWorksheet(
            XLWorkbook workbook,
            Guarantee guarantee,
            IReadOnlyList<Guarantee> orderedHistory)
        {
            IXLWorksheet worksheet = workbook.Worksheets.Add("الإصدارات");
            worksheet.RightToLeft = true;

            string supplier = ExcelReportSupport.ValueOrDash(guarantee.Supplier);
            string[] headers =
            {
                "الإصدار",
                "الحفظ",
                "الحالة الزمنية",
                "الحالة التشغيلية",
                "المورد",
                "البنك",
                "القيمة",
                "تاريخ الإدخال",
                "تاريخ الانتهاء",
                "المرجع",
                "المرفقات",
                "الملاحظات"
            };

            ExcelReportSupport.WriteTitle(
                worksheet,
                1,
                headers.Length,
                $"إصدارات الضمان {guarantee.GuaranteeNo}",
                $"{supplier} | {guarantee.Bank}");
            ExcelReportSupport.WriteHeaderRow(worksheet, 4, headers);

            for (int i = 0; i < orderedHistory.Count; i++)
            {
                Guarantee item = orderedHistory[i];
                int row = i + 5;

                worksheet.Cell(row, 1).Value = item.VersionLabel;
                IXLCell currentCell = worksheet.Cell(row, 2);
                currentCell.Value = item.IsCurrent ? "حالي" : "محفوظ";

                IXLCell timeCell = worksheet.Cell(row, 3);
                timeCell.Value = item.StatusLabel;

                IXLCell lifecycleCell = worksheet.Cell(row, 4);
                lifecycleCell.Value = item.LifecycleStatusLabel;

                worksheet.Cell(row, 5).Value = ExcelReportSupport.ValueOrDash(item.Supplier);
                worksheet.Cell(row, 6).Value = item.Bank;

                IXLCell amountCell = worksheet.Cell(row, 7);
                amountCell.Value = item.Amount;
                amountCell.Style.NumberFormat.Format = "#,##0.00";

                IXLCell createdCell = worksheet.Cell(row, 8);
                createdCell.Value = item.CreatedAt;
                createdCell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm";

                IXLCell expiryCell = worksheet.Cell(row, 9);
                expiryCell.Value = item.ExpiryDate;
                expiryCell.Style.DateFormat.Format = "yyyy-MM-dd";

                worksheet.Cell(row, 10).Value = BuildReferenceSummary(item);
                worksheet.Cell(row, 11).Value = item.AttachmentCount;
                worksheet.Cell(row, 12).Value = ExcelReportSupport.ValueOrDash(item.Notes);

                currentCell.Style.Font.FontColor = item.IsCurrent ? XLColor.FromHtml("#16A34A") : XLColor.FromHtml("#2563EB");
                timeCell.Style.Font.FontColor = item.StatusLabel switch
                {
                    "منتهي" => XLColor.FromHtml("#EF4444"),
                    "قريب الانتهاء" => XLColor.FromHtml("#E09408"),
                    _ => XLColor.FromHtml("#16A34A")
                };
                lifecycleCell.Style.Font.FontColor = ExcelReportSupport.GetLifecycleStatusColor(item.LifecycleStatus);
            }

            ExcelReportSupport.ApplyTableStyle(worksheet, 4, Math.Max(4, orderedHistory.Count + 4), headers.Length);
            worksheet.Columns().AdjustToContents();
            worksheet.SheetView.FreezeRows(4);
        }

        private static void WriteHealthWorksheet(
            XLWorkbook workbook,
            Guarantee guarantee,
            IReadOnlyList<Guarantee> orderedHistory,
            IReadOnlyList<WorkflowRequest> orderedRequests)
        {
            IXLWorksheet worksheet = workbook.Worksheets.Add("سلامة السجل");
            worksheet.RightToLeft = true;

            Guarantee current = ResolveSummaryGuarantee(guarantee, orderedHistory);
            string supplier = ExcelReportSupport.ValueOrDash(current.Supplier);
            List<GuaranteeHistoryHealthFinding> findings = GuaranteeHistoryHealthAnalyzer.BuildFindings(current, orderedHistory, orderedRequests);

            string titleState = findings.Any(item => item.Level != "سليم")
                ? "توجد نقاط تحتاج متابعة"
                : "السجل مكتمل حسب الفحوص الحالية";

            ExcelReportSupport.WriteTitle(
                worksheet,
                1,
                5,
                "سلامة سجل الضمان",
                $"{current.GuaranteeNo} | {supplier} | {titleState}");
            ExcelReportSupport.WriteHeaderRow(worksheet, 4, new[] { "المستوى", "الفحص", "النتيجة", "الدليل", "الإجراء المقترح" });

            for (int i = 0; i < findings.Count; i++)
            {
                GuaranteeHistoryHealthFinding finding = findings[i];
                int row = i + 5;

                IXLCell levelCell = worksheet.Cell(row, 1);
                levelCell.Value = finding.Level;
                worksheet.Cell(row, 2).Value = finding.Check;
                worksheet.Cell(row, 3).Value = finding.Result;
                worksheet.Cell(row, 4).Value = finding.Evidence;
                worksheet.Cell(row, 5).Value = finding.Action;

                levelCell.Style.Font.Bold = true;
                levelCell.Style.Font.FontColor = finding.Level switch
                {
                    "إجراء مطلوب" => XLColor.FromHtml("#111111"),
                    "نقص دليل" => XLColor.FromHtml("#B91C1C"),
                    "متابعة" => XLColor.FromHtml("#A16207"),
                    _ => XLColor.FromHtml("#006847")
                };
            }

            ExcelReportSupport.ApplyTableStyle(worksheet, 4, Math.Max(4, findings.Count + 4), 5);
            worksheet.Columns().AdjustToContents();
            worksheet.SheetView.FreezeRows(4);
        }

        private static void WriteRequestsWorksheet(
            XLWorkbook workbook,
            Guarantee guarantee,
            IReadOnlyList<Guarantee> orderedHistory,
            IReadOnlyList<WorkflowRequest> orderedRequests)
        {
            IXLWorksheet worksheet = workbook.Worksheets.Add("الطلبات");
            worksheet.RightToLeft = true;

            string supplier = ExcelReportSupport.ValueOrDash(guarantee.Supplier);
            string[] headers =
            {
                "التسلسل",
                "نوع الطلب",
                "الحالة",
                "تاريخ الطلب",
                "تاريخ الرد",
                "الإصدار الأساس",
                "أثر التنفيذ",
                "القيمة المطلوبة",
                "المستندات",
                "منشئ الطلب",
                "الملاحظات"
            };

            ExcelReportSupport.WriteTitle(
                worksheet,
                1,
                headers.Length,
                $"طلبات السلسلة {guarantee.GuaranteeNo}",
                $"{supplier} | {guarantee.Bank}");
            ExcelReportSupport.WriteHeaderRow(worksheet, 4, headers);

            for (int i = 0; i < orderedRequests.Count; i++)
            {
                WorkflowRequest request = orderedRequests[i];
                int row = i + 5;

                worksheet.Cell(row, 1).Value = request.SequenceNumber;
                worksheet.Cell(row, 2).Value = request.TypeLabel;

                IXLCell statusCell = worksheet.Cell(row, 3);
                statusCell.Value = request.StatusLabel;

                IXLCell requestDateCell = worksheet.Cell(row, 4);
                requestDateCell.Value = request.RequestDate;
                requestDateCell.Style.DateFormat.Format = "yyyy-MM-dd";

                worksheet.Cell(row, 5).Value = request.ResponseRecordedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "---";
                worksheet.Cell(row, 6).Value = ResolveVersionLabel(orderedHistory, request.BaseVersionId);
                worksheet.Cell(row, 7).Value = BuildExecutionEffectSummary(request, orderedHistory);
                worksheet.Cell(row, 8).Value = BuildRequestedValueSummary(request);
                worksheet.Cell(row, 9).Value = BuildDocumentState(request);
                worksheet.Cell(row, 10).Value = ExcelReportSupport.ValueOrDash(request.CreatedBy);
                worksheet.Cell(row, 11).Value = BuildRequestNotes(request);

                statusCell.Style.Font.FontColor = ExcelReportSupport.GetRequestStatusColor(request.Status);
            }

            ExcelReportSupport.ApplyTableStyle(worksheet, 4, Math.Max(4, orderedRequests.Count + 4), headers.Length);
            worksheet.Columns().AdjustToContents();
            worksheet.SheetView.FreezeRows(4);
        }

        private static FlowDocument BuildPrintDocument(
            Guarantee guarantee,
            IReadOnlyList<Guarantee> orderedHistory,
            IReadOnlyList<WorkflowRequest> orderedRequests)
        {
            Guarantee current = ResolveSummaryGuarantee(guarantee, orderedHistory);
            string supplier = ExcelReportSupport.ValueOrDash(current.Supplier);
            int totalAttachments = orderedHistory.Sum(item => item.AttachmentCount);
            int pendingRequests = orderedRequests.Count(item => item.Status == RequestStatus.Pending);

            var document = new FlowDocument
            {
                FlowDirection = FlowDirection.RightToLeft,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.5,
                PagePadding = new Thickness(34),
                PageWidth = 793,
                PageHeight = 1122,
                ColumnGap = 0,
                ColumnWidth = 725,
                TextAlignment = TextAlignment.Right
            };

            document.Blocks.Add(BuildTitleParagraph($"سجل الضمان {current.GuaranteeNo}", 18, FontWeights.Bold, "#0F172A", new Thickness(0, 0, 0, 2)));
            document.Blocks.Add(BuildTitleParagraph($"{supplier} | {current.Bank}", 11.5, FontWeights.SemiBold, "#64748B", new Thickness(0, 0, 0, 16)));

            document.Blocks.Add(BuildSummaryTable(new[]
            {
                ("رقم الضمان", current.GuaranteeNo, "الإصدار الحالي", current.VersionLabel),
                ("المورد", supplier, "البنك", current.Bank),
                ("الحالة الزمنية", current.StatusLabel, "الحالة التشغيلية", current.LifecycleStatusLabel),
                ("عدد الإصدارات", orderedHistory.Count.ToString("N0", CultureInfo.InvariantCulture), "عدد الطلبات", orderedRequests.Count.ToString("N0", CultureInfo.InvariantCulture)),
                ("الطلبات المعلقة", pendingRequests.ToString("N0", CultureInfo.InvariantCulture), "إجمالي المرفقات", totalAttachments.ToString("N0", CultureInfo.InvariantCulture))
            }));

            document.Blocks.Add(BuildSectionHeading("الإصدارات"));
            if (orderedHistory.Count == 0)
            {
                document.Blocks.Add(BuildBodyParagraph("لا توجد إصدارات محفوظة لهذا الضمان."));
            }
            else
            {
                document.Blocks.Add(BuildTable(
                    new[] { "الإصدار", "الحفظ", "الإنشاء", "الانتهاء", "القيمة", "المرفقات" },
                    orderedHistory.Select(item => new[]
                    {
                        item.VersionLabel,
                        item.IsCurrent ? "حالي" : "محفوظ",
                        item.CreatedAt.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                        item.ExpiryDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                        $"{item.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال",
                        item.AttachmentCount.ToString("N0", CultureInfo.InvariantCulture)
                    }),
                    new[] { 1.1, 1.1, 1.35, 1.35, 1.5, 0.8 }));
            }

            document.Blocks.Add(BuildSectionHeading("الطلبات"));
            if (orderedRequests.Count == 0)
            {
                document.Blocks.Add(BuildBodyParagraph("لا توجد طلبات مرتبطة بهذه السلسلة."));
            }
            else
            {
                document.Blocks.Add(BuildTable(
                    new[] { "الطلب", "الحالة", "التاريخ", "الرد", "القيمة المطلوبة", "المستندات" },
                    orderedRequests.Select(item => new[]
                    {
                        item.TypeLabel,
                        item.StatusLabel,
                        item.RequestDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                        item.ResponseRecordedAt?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "---",
                        BuildRequestedValueSummary(item),
                        BuildDocumentState(item)
                    }),
                    new[] { 1.35, 1.15, 1.2, 1.2, 1.7, 1.2 }));
            }

            if (!string.IsNullOrWhiteSpace(current.Notes))
            {
                document.Blocks.Add(BuildSectionHeading("ملاحظات الإصدار الحالي"));
                document.Blocks.Add(BuildBodyParagraph(current.Notes));
            }

            return document;
        }

        private static Table BuildSummaryTable(IEnumerable<(string Label1, string Value1, string Label2, string Value2)> rows)
        {
            return BuildTable(
                Array.Empty<string>(),
                rows.Select(row => new[] { row.Label1, row.Value1, row.Label2, row.Value2 }),
                new[] { 1.0, 1.5, 1.0, 1.5 },
                true);
        }

        private static Table BuildTable(
            IReadOnlyList<string> headers,
            IEnumerable<IReadOnlyList<string>> rows,
            IReadOnlyList<double> widths,
            bool summaryMode = false)
        {
            var table = new Table
            {
                CellSpacing = 0,
                Margin = new Thickness(0, 0, 0, 18)
            };

            foreach (double width in widths)
            {
                table.Columns.Add(new TableColumn { Width = new GridLength(width, GridUnitType.Star) });
            }

            var body = new TableRowGroup();
            table.RowGroups.Add(body);

            if (!summaryMode && headers.Count > 0)
            {
                var headerRow = new TableRow();
                foreach (string header in headers)
                {
                    headerRow.Cells.Add(BuildCell(header, true));
                }

                body.Rows.Add(headerRow);
            }

            foreach (IReadOnlyList<string> row in rows)
            {
                var tableRow = new TableRow();
                for (int i = 0; i < row.Count; i++)
                {
                    bool labelCell = summaryMode && i % 2 == 0;
                    tableRow.Cells.Add(BuildCell(row[i], false, labelCell));
                }

                body.Rows.Add(tableRow);
            }

            return table;
        }

        private static TableCell BuildCell(string text, bool header, bool summaryLabel = false)
        {
            var paragraph = new Paragraph(new Run(text))
            {
                Margin = new Thickness(0),
                TextAlignment = TextAlignment.Right,
                FlowDirection = FlowDirection.RightToLeft
            };

            paragraph.Foreground = header
                ? Brushes.White
                : summaryLabel
                    ? BrushFrom("#64748B")
                    : BrushFrom("#0F172A");

            return new TableCell(paragraph)
            {
                Padding = new Thickness(8, 6, 8, 6),
                BorderBrush = BrushFrom("#D8E1EE"),
                BorderThickness = new Thickness(1),
                Background = header
                    ? BrushFrom("#0F172A")
                    : summaryLabel
                        ? BrushFrom("#F8FAFC")
                        : Brushes.White
            };
        }

        private static Paragraph BuildTitleParagraph(string text, double size, FontWeight weight, string color, Thickness margin)
        {
            return new Paragraph(new Run(text))
            {
                Margin = margin,
                FontSize = size,
                FontWeight = weight,
                Foreground = BrushFrom(color),
                TextAlignment = TextAlignment.Right,
                FlowDirection = FlowDirection.RightToLeft
            };
        }

        private static Paragraph BuildSectionHeading(string text)
        {
            return BuildTitleParagraph(text, 14, FontWeights.Bold, "#0F172A", new Thickness(0, 0, 0, 8));
        }

        private static Paragraph BuildBodyParagraph(string text)
        {
            return BuildTitleParagraph(text, 11.5, FontWeights.Normal, "#374151", new Thickness(0, 0, 0, 14));
        }

        private static Brush BrushFrom(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }

        private static string BuildReferenceSummary(Guarantee guarantee)
        {
            return guarantee.HasReference
                ? $"{guarantee.ReferenceTypeLabel}: {guarantee.ReferenceNumber}"
                : "بدون مرجع";
        }

        private static string ResolveVersionLabel(IReadOnlyList<Guarantee> history, int versionId)
        {
            Guarantee? match = history.FirstOrDefault(item => item.Id == versionId);
            return match?.VersionLabel ?? $"#{versionId.ToString("N0", CultureInfo.InvariantCulture)}";
        }

        private static string BuildRequestedValueSummary(WorkflowRequest request)
        {
            return request.Type switch
            {
                RequestType.Reduction => request.RequestedAmount.HasValue
                    ? $"{request.RequestedAmount.Value.ToString("N0", CultureInfo.InvariantCulture)} ريال"
                    : "---",
                RequestType.Extension => request.RequestedExpiryDate?.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) ?? "---",
                RequestType.Replacement => string.IsNullOrWhiteSpace(request.ReplacementGuaranteeNo)
                    ? request.TypeLabel
                    : request.ReplacementGuaranteeNo,
                _ => request.RequestedValueLabel
            };
        }

        private static string BuildExecutionEffectSummary(WorkflowRequest request, IReadOnlyList<Guarantee> history)
        {
            if (request.Status != RequestStatus.Executed)
            {
                return "---";
            }

            return request.Type switch
            {
                RequestType.Extension or RequestType.Reduction when request.ResultVersionId.HasValue =>
                    ResolveVersionLabel(history, request.ResultVersionId.Value),
                RequestType.Verification when request.ResultVersionId.HasValue =>
                    $"مرفق رسمي على {ResolveVersionLabel(history, request.ResultVersionId.Value)}",
                RequestType.Release =>
                    "إنهاء بالإفراج",
                RequestType.Liquidation =>
                    "إنهاء بالتسييل",
                RequestType.Replacement =>
                    string.IsNullOrWhiteSpace(request.ReplacementGuaranteeNo)
                        ? "ضمان بديل"
                        : $"ضمان بديل {request.ReplacementGuaranteeNo}",
                _ => "---"
            };
        }

        private static string BuildDocumentState(WorkflowRequest request)
        {
            if (request.HasLetter && request.HasResponseDocument)
            {
                return "خطاب + رد";
            }

            if (request.HasLetter)
            {
                return "خطاب";
            }

            if (request.HasResponseDocument)
            {
                return "رد";
            }

            return "---";
        }

        private static string BuildRequestNotes(WorkflowRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.ResponseNotes))
            {
                return request.ResponseNotes;
            }

            return ExcelReportSupport.ValueOrDash(request.Notes);
        }

        private void ResetLastOutputPath()
        {
            LastOutputPath = null;
        }

    }
}
