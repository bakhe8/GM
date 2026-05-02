using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;
using Microsoft.Win32;

namespace GuaranteeManager.Services
{
    internal static class ExcelReportSupport
    {
        internal static SaveFileDialog BuildSaveDialog(string fileName, string title)
        {
            return new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = fileName,
                CheckFileExists = false,
                CheckPathExists = true,
                CreatePrompt = false,
                OverwritePrompt = true,
                ValidateNames = true,
                Title = title
            };
        }

        internal static ExcelExportResult SaveWorkbook(XLWorkbook workbook, string outputPath)
        {
            workbook.SaveAs(outputPath);
            return ExcelExportResult.Saved(outputPath);
        }

        internal static void WriteGuaranteesWorksheet(
            IXLWorksheet worksheet,
            IReadOnlyList<Guarantee> guarantees,
            string reportTitle,
            string reportSubtitle)
        {
            string[] headers =
            {
                "رقم الضمان",
                "نوع المرجع",
                "رقم المرجع",
                "المورد",
                "البنك",
                "المبلغ",
                "تاريخ الانتهاء",
                "الحالة الزمنية",
                "الحالة التشغيلية",
                "الإصدار",
                "النوع",
                "عدد المرفقات",
                "تاريخ الإدخال",
                "الملاحظات"
            };

            WriteTitle(worksheet, 1, headers.Length, reportTitle, reportSubtitle);
            WriteHeaderRow(worksheet, 4, headers);

            for (int i = 0; i < guarantees.Count; i++)
            {
                var guarantee = guarantees[i];
                int row = i + 5;

                worksheet.Cell(row, 1).Value = guarantee.GuaranteeNo;
                worksheet.Cell(row, 2).Value = guarantee.ReferenceTypeLabel;
                worksheet.Cell(row, 3).Value = ValueOrDash(guarantee.ReferenceNumber);
                worksheet.Cell(row, 4).Value = guarantee.Supplier;
                worksheet.Cell(row, 5).Value = guarantee.Bank;

                var amountCell = worksheet.Cell(row, 6);
                amountCell.Value = guarantee.Amount;
                amountCell.Style.NumberFormat.Format = "#,##0.00";

                var expiryCell = worksheet.Cell(row, 7);
                expiryCell.Value = guarantee.ExpiryDate;
                expiryCell.Style.DateFormat.Format = "yyyy-MM-dd";

                var temporalStatusCell = worksheet.Cell(row, 8);
                temporalStatusCell.Value = guarantee.StatusLabel;

                var lifecycleStatusCell = worksheet.Cell(row, 9);
                lifecycleStatusCell.Value = guarantee.LifecycleStatusLabel;

                worksheet.Cell(row, 10).Value = guarantee.VersionLabel;
                worksheet.Cell(row, 11).Value = ValueOrDash(guarantee.GuaranteeType);
                worksheet.Cell(row, 12).Value = guarantee.AttachmentCount;

                var createdCell = worksheet.Cell(row, 13);
                createdCell.Value = guarantee.CreatedAt;
                createdCell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm";

                worksheet.Cell(row, 14).Value = ValueOrDash(guarantee.Notes);

                temporalStatusCell.Style.Font.FontColor = guarantee.StatusLabel switch
                {
                    "منتهي" => XLColor.FromHtml("#111111"),
                    "قريب الانتهاء" => XLColor.FromHtml("#666666"),
                    _ => XLColor.FromHtml("#006847")
                };

                lifecycleStatusCell.Style.Font.FontColor = guarantee.LifecycleStatus switch
                {
                    GuaranteeLifecycleStatus.Released => XLColor.FromHtml("#666666"),
                    GuaranteeLifecycleStatus.Liquidated => XLColor.FromHtml("#111111"),
                    GuaranteeLifecycleStatus.Replaced => XLColor.FromHtml("#666666"),
                    GuaranteeLifecycleStatus.Closed => XLColor.FromHtml("#111111"),
                    _ => XLColor.FromHtml("#006847")
                };

                if (guarantee.IsExpired)
                {
                    expiryCell.Style.Font.FontColor = XLColor.FromHtml("#111111");
                }
            }

            ApplyTableStyle(worksheet, 4, guarantees.Count + 4, headers.Length);
            worksheet.Columns().AdjustToContents();
            worksheet.SheetView.FreezeRows(4);
        }

        internal static void WriteWorkflowRequestsWorksheet(
            IXLWorksheet worksheet,
            IReadOnlyList<WorkflowRequestListItem> requests,
            string reportTitle,
            string reportSubtitle)
        {
            string[] headers =
            {
                "تسلسل الطلب",
                "رقم الضمان",
                "المورد",
                "البنك",
                "نوع المرجع",
                "رقم المرجع",
                "نوع الطلب",
                "حالة الطلب",
                "الحالة التشغيلية",
                "الحقل الحالي",
                "القيمة الحالية",
                "الحقل المطلوب",
                "القيمة المطلوبة",
                "تاريخ الطلب",
                "تاريخ الرد",
                "إصدار الطلب",
                "أثر التنفيذ",
                "خطاب الطلب",
                "مستند رد البنك",
                "ملاحظات الطلب",
                "ملاحظات الرد"
            };

            WriteTitle(worksheet, 1, headers.Length, reportTitle, reportSubtitle);
            WriteHeaderRow(worksheet, 4, headers);

            for (int i = 0; i < requests.Count; i++)
            {
                WorkflowRequestListItem item = requests[i];
                int row = i + 5;

                worksheet.Cell(row, 1).Value = item.Request.SequenceNumber;
                worksheet.Cell(row, 2).Value = item.GuaranteeNo;
                worksheet.Cell(row, 3).Value = item.Supplier;
                worksheet.Cell(row, 4).Value = item.Bank;
                worksheet.Cell(row, 5).Value = item.ReferenceTypeLabel;
                worksheet.Cell(row, 6).Value = ValueOrDash(item.ReferenceNumber);

                IXLCell typeCell = worksheet.Cell(row, 7);
                typeCell.Value = item.Request.TypeLabel;

                IXLCell statusCell = worksheet.Cell(row, 8);
                statusCell.Value = item.Request.StatusLabel;

                IXLCell lifecycleCell = worksheet.Cell(row, 9);
                lifecycleCell.Value = item.LifecycleStatusLabel;

                worksheet.Cell(row, 10).Value = item.CurrentValueFieldLabel;
                WriteWorkflowCurrentValueCell(worksheet.Cell(row, 11), item);
                worksheet.Cell(row, 12).Value = item.RequestedValueFieldLabel;
                WriteWorkflowRequestedValueCell(worksheet.Cell(row, 13), item);

                IXLCell requestDateCell = worksheet.Cell(row, 14);
                requestDateCell.Value = item.Request.RequestDate;
                requestDateCell.Style.DateFormat.Format = "yyyy-MM-dd";

                IXLCell responseDateCell = worksheet.Cell(row, 15);
                if (item.Request.ResponseRecordedAt.HasValue)
                {
                    responseDateCell.Value = item.Request.ResponseRecordedAt.Value;
                    responseDateCell.Style.DateFormat.Format = "yyyy-MM-dd";
                }
                else
                {
                    responseDateCell.Value = "---";
                }

                worksheet.Cell(row, 16).Value = item.RelatedVersionLabel;
                worksheet.Cell(row, 17).Value = BuildWorkflowExecutionEffectSummary(item);

                IXLCell letterCell = worksheet.Cell(row, 18);
                letterCell.Value = item.Request.HasLetter ? "موجود" : "---";

                IXLCell responseDocCell = worksheet.Cell(row, 19);
                responseDocCell.Value = item.Request.HasResponseDocument ? "موجود" : "---";

                worksheet.Cell(row, 20).Value = ValueOrDash(item.Request.Notes);
                worksheet.Cell(row, 21).Value = ValueOrDash(item.Request.ResponseNotes);

                typeCell.Style.Font.FontColor = GetRequestTypeColor(item.Request.Type);
                statusCell.Style.Font.FontColor = GetRequestStatusColor(item.Request.Status);
                lifecycleCell.Style.Font.FontColor = GetLifecycleStatusColor(item.LifecycleStatus);
                letterCell.Style.Font.FontColor = item.Request.HasLetter ? XLColor.FromHtml("#006847") : XLColor.FromHtml("#666666");
                responseDocCell.Style.Font.FontColor = item.Request.HasResponseDocument ? XLColor.FromHtml("#006847") : XLColor.FromHtml("#666666");
            }

            ApplyTableStyle(worksheet, 4, requests.Count + 4, headers.Length);
            worksheet.Columns().AdjustToContents();
            worksheet.SheetView.FreezeRows(4);
        }

        internal static void WriteGuaranteeStatisticsWorksheet(
            IXLWorksheet worksheet,
            IReadOnlyList<GuaranteeStatisticRow> rows,
            string reportTitle,
            string reportSubtitle,
            string firstColumnHeader)
        {
            string[] headers =
            {
                firstColumnHeader,
                "عدد الضمانات",
                "إجمالي المبالغ",
                "قريب الانتهاء",
                "منتهي",
                "نشط تشغيليًا",
                "مفرج",
                "مسيّل",
                "مستبدل",
                "تحتاج إفراج/إعادة"
            };

            WriteTitle(worksheet, 1, headers.Length, reportTitle, reportSubtitle);
            WriteHeaderRow(worksheet, 4, headers);

            for (int i = 0; i < rows.Count; i++)
            {
                GuaranteeStatisticRow row = rows[i];
                int excelRow = i + 5;

                worksheet.Cell(excelRow, 1).Value = row.Label;
                worksheet.Cell(excelRow, 2).Value = row.Count;
                worksheet.Cell(excelRow, 3).Value = row.TotalAmount;
                worksheet.Cell(excelRow, 3).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(excelRow, 4).Value = row.ExpiringSoonCount;
                worksheet.Cell(excelRow, 5).Value = row.ExpiredCount;
                worksheet.Cell(excelRow, 6).Value = row.ActiveLifecycleCount;
                worksheet.Cell(excelRow, 7).Value = row.ReleasedCount;
                worksheet.Cell(excelRow, 8).Value = row.LiquidatedCount;
                worksheet.Cell(excelRow, 9).Value = row.ReplacedCount;
                worksheet.Cell(excelRow, 10).Value = row.ExpiryFollowUpCount;
            }

            ApplyTableStyle(worksheet, 4, rows.Count + 4, headers.Length);
            worksheet.Columns().AdjustToContents();
            worksheet.SheetView.FreezeRows(4);
        }

        internal static void WriteOverviewRow(IXLWorksheet worksheet, int row, string metric, string value, string description, string notes)
        {
            worksheet.Cell(row, 1).Value = metric;
            worksheet.Cell(row, 2).Value = value;
            worksheet.Cell(row, 3).Value = description;
            worksheet.Cell(row, 4).Value = notes;
        }

        internal static void WriteTitle(IXLWorksheet worksheet, int row, int lastColumn, string title, string subtitle)
        {
            var titleRange = worksheet.Range(row, 1, row, lastColumn);
            titleRange.Merge();
            titleRange.Value = title;
            titleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#006847");
            titleRange.Style.Font.FontColor = XLColor.White;
            titleRange.Style.Font.Bold = true;
            titleRange.Style.Font.FontSize = 15;
            titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            var subtitleRange = worksheet.Range(row + 1, 1, row + 1, lastColumn);
            subtitleRange.Merge();
            subtitleRange.Value = subtitle;
            subtitleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F7F7F7");
            subtitleRange.Style.Font.FontColor = XLColor.FromHtml("#666666");
            subtitleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            subtitleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        internal static void WriteHeaderRow(IXLWorksheet worksheet, int headerRow, IReadOnlyList<string> headers)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                var cell = worksheet.Cell(headerRow, i + 1);
                cell.Value = headers[i];
                ApplyHeaderStyle(cell);
            }
        }

        internal static void WriteSummaryPair(
            IXLWorksheet worksheet,
            int row,
            string label1,
            string value1,
            string label2,
            string value2)
        {
            worksheet.Cell(row, 1).Value = label1;
            ApplySummaryLabelStyle(worksheet.Cell(row, 1));
            worksheet.Range(row, 2, row, 4).Merge();
            worksheet.Cell(row, 2).Value = ValueOrDash(value1);
            ApplySummaryValueStyle(worksheet.Cell(row, 2));

            worksheet.Cell(row, 5).Value = label2;
            ApplySummaryLabelStyle(worksheet.Cell(row, 5));
            worksheet.Range(row, 6, row, 8).Merge();
            worksheet.Cell(row, 6).Value = ValueOrDash(value2);
            ApplySummaryValueStyle(worksheet.Cell(row, 6));
        }

        internal static void ApplyTableStyle(IXLWorksheet worksheet, int firstRow, int lastRow, int lastColumn)
        {
            var range = worksheet.Range(firstRow, 1, lastRow, lastColumn);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            range.Style.Alignment.WrapText = true;
            range.SetAutoFilter();
        }

        internal static void ApplyHeaderStyle(IXLCell cell)
        {
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E6E6E6");
            cell.Style.Font.FontColor = XLColor.FromHtml("#111111");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        internal static void ApplySummaryLabelStyle(IXLCell cell)
        {
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F7F7F7");
            cell.Style.Font.FontColor = XLColor.FromHtml("#666666");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        internal static void ApplySummaryValueStyle(IXLCell cell)
        {
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        internal static string ValueOrDash(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "---" : value;
        }

        internal static string GetRequestStatusLabel(RequestStatus status)
        {
            return status switch
            {
                RequestStatus.Pending => "قيد الانتظار",
                RequestStatus.Executed => "منفذ",
                RequestStatus.Rejected => "مرفوض",
                RequestStatus.Cancelled => "مُلغى",
                RequestStatus.Superseded => "مُسقط آليًا",
                _ => "غير معروف"
            };
        }

        internal static string GetRequestTypeLabel(RequestType type)
        {
            return type switch
            {
                RequestType.Extension => "طلب تمديد",
                RequestType.Release => "طلب إفراج",
                RequestType.Liquidation => "طلب تسييل",
                RequestType.Reduction => "طلب تخفيض",
                RequestType.Verification => "طلب تحقق",
                RequestType.Replacement => "طلب استبدال",
                _ => "طلب غير معروف"
            };
        }

        private static void WriteWorkflowCurrentValueCell(IXLCell cell, WorkflowRequestListItem item)
        {
            if (item.Request.Type == RequestType.Reduction)
            {
                WritePlainAmountCell(cell, item.CurrentAmount);
                return;
            }

            cell.Value = item.CurrentValueDisplay;
        }

        private static void WriteWorkflowRequestedValueCell(IXLCell cell, WorkflowRequestListItem item)
        {
            if (item.Request.Type == RequestType.Reduction)
            {
                if (item.Request.RequestedAmount.HasValue)
                {
                    WritePlainAmountCell(cell, item.Request.RequestedAmount.Value);
                    return;
                }

                cell.Value = "---";
                return;
            }

            cell.Value = item.RequestedValueDisplay;
        }

        internal static void WritePlainAmountCell(IXLCell cell, decimal amount)
        {
            cell.Value = amount;
            cell.Style.NumberFormat.Format = "#,##0.00";
        }

        internal static string FormatPlainAmount(decimal amount, int decimals = 2)
        {
            return ArabicAmountFormatter.FormatNumber(amount, decimals);
        }

        private static string BuildWorkflowExecutionEffectSummary(WorkflowRequestListItem item)
        {
            if (item.Request.Status != RequestStatus.Executed)
            {
                return "---";
            }

            return item.Request.Type switch
            {
                RequestType.Extension or RequestType.Reduction when item.ResultVersionNumber.HasValue =>
                    $"نتج الإصدار {GuaranteeVersionDisplay.GetLabel(item.ResultVersionNumber.Value)}",
                RequestType.Verification when item.ResultVersionNumber.HasValue =>
                    $"اعتماد مستند رسمي على الإصدار {GuaranteeVersionDisplay.GetLabel(item.ResultVersionNumber.Value)}",
                RequestType.Release => "إنهاء دورة الحياة بالإفراج",
                RequestType.Liquidation => "إنهاء دورة الحياة بالتسييل",
                RequestType.Replacement => string.IsNullOrWhiteSpace(item.Request.ReplacementGuaranteeNo)
                    ? "إنهاء دورة الحياة بالاستبدال"
                    : $"استبدال بضمان {item.Request.ReplacementGuaranteeNo}",
                _ => "منفذ بلا إصدار جديد"
            };
        }

        internal static XLColor GetLifecycleStatusColor(GuaranteeLifecycleStatus lifecycleStatus)
        {
            return lifecycleStatus switch
            {
                GuaranteeLifecycleStatus.Released => XLColor.FromHtml("#666666"),
                GuaranteeLifecycleStatus.Liquidated => XLColor.FromHtml("#111111"),
                GuaranteeLifecycleStatus.Replaced => XLColor.FromHtml("#666666"),
                GuaranteeLifecycleStatus.Closed => XLColor.FromHtml("#111111"),
                _ => XLColor.FromHtml("#006847")
            };
        }

        internal static XLColor GetRequestStatusColor(RequestStatus status)
        {
            return status switch
            {
                RequestStatus.Pending => XLColor.FromHtml("#666666"),
                RequestStatus.Executed => XLColor.FromHtml("#006847"),
                RequestStatus.Rejected => XLColor.FromHtml("#111111"),
                RequestStatus.Cancelled => XLColor.FromHtml("#666666"),
                RequestStatus.Superseded => XLColor.FromHtml("#666666"),
                _ => XLColor.FromHtml("#111111")
            };
        }

        internal static XLColor GetRequestTypeColor(RequestType type)
        {
            return type switch
            {
                RequestType.Extension => XLColor.FromHtml("#111111"),
                RequestType.Release => XLColor.FromHtml("#006847"),
                RequestType.Liquidation => XLColor.FromHtml("#111111"),
                RequestType.Reduction => XLColor.FromHtml("#666666"),
                RequestType.Verification => XLColor.FromHtml("#666666"),
                RequestType.Replacement => XLColor.FromHtml("#111111"),
                _ => XLColor.FromHtml("#111111")
            };
        }

        internal static string MakeSafeFileName(string input)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var safeChars = input.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
            return new string(safeChars);
        }

        internal static List<GuaranteeStatisticRow> BuildGuaranteeStatisticsRows(
            IReadOnlyList<Guarantee> guarantees,
            Func<Guarantee, string> groupSelector)
        {
            return guarantees
                .Where(guarantee => !string.IsNullOrWhiteSpace(groupSelector(guarantee)))
                .GroupBy(guarantee => groupSelector(guarantee).Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => new GuaranteeStatisticRow
                {
                    Label = group.Key,
                    Count = group.Count(),
                    TotalAmount = group.Sum(item => item.Amount),
                    ExpiringSoonCount = group.Count(item => item.IsExpiringSoon),
                    ExpiredCount = group.Count(item => item.IsExpired),
                    ActiveLifecycleCount = group.Count(item => item.LifecycleStatus == GuaranteeLifecycleStatus.Active),
                    ReleasedCount = group.Count(item => item.LifecycleStatus == GuaranteeLifecycleStatus.Released),
                    LiquidatedCount = group.Count(item => item.LifecycleStatus == GuaranteeLifecycleStatus.Liquidated),
                    ReplacedCount = group.Count(item => item.LifecycleStatus == GuaranteeLifecycleStatus.Replaced),
                    ExpiryFollowUpCount = group.Count(item => item.NeedsExpiryFollowUp)
                })
                .OrderByDescending(row => row.TotalAmount)
                .ThenByDescending(row => row.Count)
                .ThenBy(row => row.Label)
                .ToList();
        }
    }

    internal sealed class GuaranteeStatisticRow
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public int ExpiringSoonCount { get; set; }
        public int ExpiredCount { get; set; }
        public int ActiveLifecycleCount { get; set; }
        public int ReleasedCount { get; set; }
        public int LiquidatedCount { get; set; }
        public int ReplacedCount { get; set; }
        public int ExpiryFollowUpCount { get; set; }
    }
}
