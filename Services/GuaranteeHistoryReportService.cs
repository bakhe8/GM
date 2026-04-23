using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ClosedXML.Excel;
using GuaranteeManager.Models;
using Microsoft.Win32;

namespace GuaranteeManager.Services
{
    public class GuaranteeHistoryReportService
    {
        public string? LastOutputPath { get; private set; }

        public bool ExportHistoryToExcel(Guarantee selectedGuarantee, IReadOnlyList<Guarantee> history)
        {
            LastOutputPath = null;

            if (history.Count == 0)
            {
                return false;
            }

            var current = history.FirstOrDefault(g => g.IsCurrent) ?? history.First();
            var firstCreated = history.Min(g => g.CreatedAt);
            var lastUpdated = history.Max(g => g.CreatedAt);

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"Guarantee_History_{ExcelReportSupport.MakeSafeFileName(selectedGuarantee.GuaranteeNo)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                CheckFileExists = false,
                CheckPathExists = true,
                CreatePrompt = false,
                OverwritePrompt = true,
                ValidateNames = true,
                Title = "حفظ تقرير تاريخ الضمان"
            };

            if (saveFileDialog.ShowDialog() != true)
            {
                return false;
            }

            return ExportHistoryToExcelAtPath(selectedGuarantee, history, saveFileDialog.FileName);
        }

        internal bool ExportHistoryToExcelAtPath(Guarantee selectedGuarantee, IReadOnlyList<Guarantee> history, string outputPath)
        {
            if (history.Count == 0)
            {
                return false;
            }

            var current = history.FirstOrDefault(g => g.IsCurrent) ?? history.First();
            var firstCreated = history.Min(g => g.CreatedAt);
            var lastUpdated = history.Max(g => g.CreatedAt);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("سجل الضمان");
            worksheet.SetRightToLeft(true);

            WriteSummarySection(worksheet, selectedGuarantee, current, history.Count, firstCreated, lastUpdated);

            const int headersRow = 6;
            string[] headers =
            {
                "الحالة",
                "الإصدار",
                "المورد",
                "البنك",
                "المبلغ",
                "تاريخ الانتهاء",
                "تاريخ الإنشاء",
                "النوع",
                "نوع المرجع",
                "رقم المرجع",
                "المستفيد",
                "عدد المرفقات",
                "الملاحظات"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var headerCell = worksheet.Cell(headersRow, i + 1);
                headerCell.Value = headers[i];
                ExcelReportSupport.ApplyHeaderStyle(headerCell);
            }

            for (int i = 0; i < history.Count; i++)
            {
                var guarantee = history[i];
                int row = headersRow + i + 1;

                worksheet.Cell(row, 1).Value = guarantee.IsCurrent ? "حالي" : "أرشيف";
                worksheet.Cell(row, 2).Value = $"v{guarantee.VersionNumber}";
                worksheet.Cell(row, 3).Value = guarantee.Supplier;
                worksheet.Cell(row, 4).Value = guarantee.Bank;

                var amountCell = worksheet.Cell(row, 5);
                amountCell.Value = guarantee.Amount;
                amountCell.Style.NumberFormat.Format = "#,##0.00";

                var expiryCell = worksheet.Cell(row, 6);
                expiryCell.Value = guarantee.ExpiryDate;
                expiryCell.Style.DateFormat.Format = "yyyy-MM-dd";

                var createdCell = worksheet.Cell(row, 7);
                createdCell.Value = guarantee.CreatedAt;
                createdCell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm";

                worksheet.Cell(row, 8).Value = ExcelReportSupport.ValueOrDash(guarantee.GuaranteeType);
                worksheet.Cell(row, 9).Value = guarantee.ReferenceTypeLabel;
                worksheet.Cell(row, 10).Value = ExcelReportSupport.ValueOrDash(guarantee.ReferenceNumber);
                worksheet.Cell(row, 11).Value = ExcelReportSupport.ValueOrDash(guarantee.Beneficiary);
                worksheet.Cell(row, 12).Value = guarantee.AttachmentCount;
                worksheet.Cell(row, 13).Value = ExcelReportSupport.ValueOrDash(guarantee.Notes);

                if (guarantee.IsCurrent)
                {
                    worksheet.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
                }

                if (guarantee.IsExpired)
                {
                    expiryCell.Style.Font.FontColor = XLColor.FromHtml("#111111");
                }
            }

            var dataRange = worksheet.Range(headersRow, 1, headersRow + history.Count, headers.Length);
            dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            dataRange.Style.Alignment.WrapText = true;

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(outputPath);
            LastOutputPath = outputPath;
            return true;
        }

        public bool PrintHistory(Guarantee selectedGuarantee, IReadOnlyList<Guarantee> history)
        {
            if (history.Count == 0)
            {
                return false;
            }

            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true)
            {
                return false;
            }

            if (printDialog.PrintTicket != null)
            {
                printDialog.PrintTicket.PageOrientation = PageOrientation.Portrait;
            }

            FlowDocument document = BuildHistoryDocument(selectedGuarantee, history);
            document.PagePadding = new Thickness(32);
            document.ColumnGap = 0;
            document.ColumnWidth = double.PositiveInfinity;

            if (printDialog.PrintableAreaWidth > 0)
            {
                document.PageWidth = printDialog.PrintableAreaWidth + document.PagePadding.Left + document.PagePadding.Right;
            }

            if (printDialog.PrintableAreaHeight > 0)
            {
                document.PageHeight = printDialog.PrintableAreaHeight + document.PagePadding.Top + document.PagePadding.Bottom;
            }

            printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, $"سجل الضمان {selectedGuarantee.GuaranteeNo}");
            return true;
        }

        private static void WriteSummarySection(IXLWorksheet worksheet, Guarantee selectedGuarantee, Guarantee current, int versionCount, DateTime firstCreated, DateTime lastUpdated)
        {
            WriteSummaryPair(worksheet, 1, 1, "رقم الضمان", selectedGuarantee.GuaranteeNo, "المورد الحالي", current.Supplier);
            WriteSummaryPair(worksheet, 2, 1, "البنك الحالي", current.Bank, "الإصدار الحالي", $"v{current.VersionNumber}");
            WriteSummaryPair(worksheet, 3, 1, "عدد الإصدارات", versionCount.ToString(), "أول تسجيل", firstCreated.ToString("yyyy-MM-dd"));
            WriteSummaryPair(worksheet, 4, 1, "آخر تحديث", lastUpdated.ToString("yyyy-MM-dd HH:mm"), "تاريخ التقرير", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        }

        private static void WriteSummaryPair(IXLWorksheet worksheet, int row, int startColumn, string label1, string value1, string label2, string value2)
        {
            var labelCell1 = worksheet.Cell(row, startColumn);
            labelCell1.Value = label1;
            ExcelReportSupport.ApplySummaryLabelStyle(labelCell1);

            var valueCell1 = worksheet.Cell(row, startColumn + 1);
            valueCell1.Value = ExcelReportSupport.ValueOrDash(value1);
            ExcelReportSupport.ApplySummaryValueStyle(valueCell1);

            var labelCell2 = worksheet.Cell(row, startColumn + 2);
            labelCell2.Value = label2;
            ExcelReportSupport.ApplySummaryLabelStyle(labelCell2);

            var valueCell2 = worksheet.Cell(row, startColumn + 3);
            valueCell2.Value = ExcelReportSupport.ValueOrDash(value2);
            ExcelReportSupport.ApplySummaryValueStyle(valueCell2);
        }

        private static FlowDocument BuildHistoryDocument(Guarantee selectedGuarantee, IReadOnlyList<Guarantee> history)
        {
            var current = history.FirstOrDefault(g => g.IsCurrent) ?? history.First();
            var firstCreated = history.Min(g => g.CreatedAt);
            var lastUpdated = history.Max(g => g.CreatedAt);
            Brush primaryText = new SolidColorBrush(Color.FromRgb(17, 17, 17));
            Brush mutedText = new SolidColorBrush(Color.FromRgb(102, 102, 102));
            Brush accentText = new SolidColorBrush(Color.FromRgb(0, 104, 71));
            Brush panelBackground = new SolidColorBrush(Color.FromRgb(247, 247, 247));
            Brush panelBorder = new SolidColorBrush(Color.FromRgb(207, 207, 207));

            var document = new FlowDocument
            {
                FlowDirection = FlowDirection.RightToLeft,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Foreground = primaryText
            };

            document.Blocks.Add(new Paragraph(new Run($"سجل الضمان رقم {selectedGuarantee.GuaranteeNo}"))
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            });

            document.Blocks.Add(new Paragraph(new Run($"{current.Supplier} - {current.Bank}"))
            {
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 2)
            });

            document.Blocks.Add(new Paragraph(new Run($"عدد الإصدارات: {history.Count} | الإصدار الحالي: v{current.VersionNumber} | أول تسجيل: {firstCreated:yyyy-MM-dd} | آخر تحديث: {lastUpdated:yyyy-MM-dd HH:mm}"))
            {
                FontSize = 10,
                Foreground = mutedText,
                Margin = new Thickness(0, 0, 0, 16)
            });

            foreach (var guarantee in history)
            {
                document.Blocks.Add(new Paragraph(new Run($"الإصدار v{guarantee.VersionNumber} - {(guarantee.IsCurrent ? "حالي" : "أرشيف")}"))
                {
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 6, 0, 4),
                    Foreground = guarantee.IsCurrent ? accentText : primaryText
                });

                document.Blocks.Add(BuildVersionTable(guarantee, panelBackground, panelBorder));
            }

            return document;
        }

        private static Table BuildVersionTable(Guarantee guarantee, Brush panelBackground, Brush panelBorder)
        {
            var table = new Table
            {
                CellSpacing = 0,
                Margin = new Thickness(0, 0, 0, 10)
            };

            table.Columns.Add(new TableColumn { Width = new GridLength(150) });
            table.Columns.Add(new TableColumn { Width = new GridLength(360) });

            var group = new TableRowGroup();
            table.RowGroups.Add(group);

            AddVersionRow(group, "المورد", guarantee.Supplier, panelBackground, panelBorder);
            AddVersionRow(group, "البنك", guarantee.Bank, panelBackground, panelBorder);
            AddVersionRow(group, "المبلغ", guarantee.Amount.ToString("#,##0.00"), panelBackground, panelBorder);
            AddVersionRow(group, "تاريخ الانتهاء", guarantee.ExpiryDate.ToString("yyyy-MM-dd"), panelBackground, panelBorder);
            AddVersionRow(group, "تاريخ الإنشاء", guarantee.CreatedAt.ToString("yyyy-MM-dd HH:mm"), panelBackground, panelBorder);
            AddVersionRow(group, "النوع", ExcelReportSupport.ValueOrDash(guarantee.GuaranteeType), panelBackground, panelBorder);
            AddVersionRow(group, "نوع المرجع", guarantee.ReferenceTypeLabel, panelBackground, panelBorder);
            AddVersionRow(group, "رقم المرجع", ExcelReportSupport.ValueOrDash(guarantee.ReferenceNumber), panelBackground, panelBorder);
            AddVersionRow(group, "المستفيد", ExcelReportSupport.ValueOrDash(guarantee.Beneficiary), panelBackground, panelBorder);
            AddVersionRow(group, "عدد المرفقات", guarantee.AttachmentCount.ToString(), panelBackground, panelBorder);
            AddVersionRow(group, "الملاحظات", ExcelReportSupport.ValueOrDash(guarantee.Notes), panelBackground, panelBorder);

            return table;
        }

        private static void AddVersionRow(TableRowGroup group, string label, string value, Brush labelBackground, Brush borderBrush)
        {
            var row = new TableRow();
            group.Rows.Add(row);

            row.Cells.Add(new TableCell(new Paragraph(new Run(label)))
            {
                Background = labelBackground,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0.6),
                Padding = new Thickness(8, 5, 8, 5),
                TextAlignment = TextAlignment.Right
            });

            row.Cells.Add(new TableCell(new Paragraph(new Run(ExcelReportSupport.ValueOrDash(value))))
            {
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0.6),
                Padding = new Thickness(8, 5, 8, 5),
                TextAlignment = TextAlignment.Right
            });
        }

    }
}
