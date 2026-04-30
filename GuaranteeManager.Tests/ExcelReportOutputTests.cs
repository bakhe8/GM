using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ClosedXML.Excel;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Xunit;

namespace GuaranteeManager.Tests
{
    [Collection("Database")]
    public sealed class ExcelReportOutputTests
    {
        private readonly TestEnvironmentFixture _fixture;

        public ExcelReportOutputTests(TestEnvironmentFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void GuaranteeListReport_UsesSupplierAsDisplayedParty()
        {
            Guarantee guarantee = _fixture.CreateGuarantee("BG-EXCEL-001");
            guarantee.Supplier = "شركة التشغيل الطبي";
            guarantee.Bank = "بنك الرياض";
            guarantee.Beneficiary = "قيمة قديمة لا يفترض عرضها هنا";

            string outputPath = _fixture.CreateArtifactPath(".xlsx");
            var excel = new ExcelService();

            bool exported = excel.ExportGuaranteesReportToPath(
                new[] { guarantee },
                "تقرير الضمانات",
                "اختبار شكل الأعمدة",
                outputPath);

            Assert.True(exported);

            using var workbook = new XLWorkbook(outputPath);
            IXLWorksheet worksheet = workbook.Worksheet("الضمانات");
            List<string> headers = ReadHeaderRow(worksheet, 4, 14);

            Assert.True(worksheet.RightToLeft);
            Assert.Contains("المورد", headers);
            Assert.DoesNotContain("المستفيد", headers);
            Assert.Equal("شركة التشغيل الطبي", worksheet.Cell(5, 4).GetString());
        }

        [Fact]
        public void WorkflowRequestReport_ExplainsExecutionEffectInsteadOfInternalIds()
        {
            var request = new WorkflowRequest
            {
                SequenceNumber = 2,
                Type = RequestType.Extension,
                Status = RequestStatus.Executed,
                RequestDate = new DateTime(2026, 1, 10),
                ResponseRecordedAt = new DateTime(2026, 1, 12),
                ResultVersionId = 987,
                RequestedDataJson = JsonSerializer.Serialize(new WorkflowRequestedData
                {
                    RequestedExpiryDate = new DateTime(2026, 12, 31)
                }),
                LetterSavedFileName = "letter.html",
                ResponseSavedFileName = "response.pdf"
            };
            var item = new WorkflowRequestListItem
            {
                Request = request,
                GuaranteeNo = "BG-EXCEL-002",
                Supplier = "شركة الصيانة",
                Bank = "بنك ساب",
                ReferenceType = GuaranteeReferenceType.Contract,
                ReferenceNumber = "C-7788",
                CurrentAmount = 500_000m,
                CurrentExpiryDate = new DateTime(2026, 6, 30),
                CurrentVersionNumber = 2,
                BaseVersionNumber = 1,
                ResultVersionNumber = 2,
                LifecycleStatus = GuaranteeLifecycleStatus.Active
            };

            string outputPath = _fixture.CreateArtifactPath(".xlsx");
            var excel = new ExcelService();

            bool exported = excel.ExportWorkflowRequestsReportToPath(
                new[] { item },
                "تقرير الطلبات",
                "اختبار أثر التنفيذ",
                outputPath);

            Assert.True(exported);

            using var workbook = new XLWorkbook(outputPath);
            IXLWorksheet worksheet = workbook.Worksheet("الطلبات");
            List<string> headers = ReadHeaderRow(worksheet, 4, 21);

            Assert.Contains("إصدار الطلب", headers);
            Assert.Contains("أثر التنفيذ", headers);
            Assert.DoesNotContain("معرّف السجل الناتج", headers);
            Assert.Equal("الثاني", worksheet.Cell(5, 16).GetString());
            Assert.Equal("نتج الإصدار الثاني", worksheet.Cell(5, 17).GetString());
        }

        [Fact]
        public void SingleGuaranteeReport_DoesNotShowBeneficiary()
        {
            Guarantee guarantee = _fixture.CreateGuarantee("BG-EXCEL-SINGLE");
            guarantee.Supplier = "شركة التشغيل الطبي";
            guarantee.Bank = "بنك ساب";
            guarantee.Beneficiary = "مستفيد ثابت لا يعرض في Excel";

            string outputPath = _fixture.CreateArtifactPath(".xlsx");
            var excel = new ExcelService();

            bool exported = excel.ExportSingleGuaranteeReportToPath(guarantee, outputPath);

            Assert.True(exported);

            using var workbook = new XLWorkbook(outputPath);
            IXLWorksheet worksheet = workbook.Worksheet("بيانات الضمان");
            string usedText = string.Join(
                "|",
                worksheet.CellsUsed().Select(cell => cell.GetString()));

            Assert.Contains("المورد", usedText);
            Assert.DoesNotContain("المستفيد", usedText);
            Assert.DoesNotContain("مستفيد ثابت لا يعرض في Excel", usedText);
        }

        [Fact]
        public void StatisticsWorksheet_ReplacesLegacyClosedColumnWithExpiryFollowUp()
        {
            using var workbook = new XLWorkbook();
            IXLWorksheet worksheet = workbook.Worksheets.Add("الإحصاء");
            worksheet.RightToLeft = true;

            ExcelReportSupport.WriteGuaranteeStatisticsWorksheet(
                worksheet,
                new[]
                {
                    new GuaranteeStatisticRow
                    {
                        Label = "بنك الاختبار",
                        Count = 3,
                        TotalAmount = 1_250_000m,
                        ExpiringSoonCount = 1,
                        ExpiredCount = 2,
                        ActiveLifecycleCount = 2,
                        ReleasedCount = 1,
                        LiquidatedCount = 0,
                        ReplacedCount = 0,
                        ExpiryFollowUpCount = 2
                    }
                },
                "إحصاء الضمانات",
                "اختبار الأعمدة",
                "البنك");

            List<string> headers = ReadHeaderRow(worksheet, 4, 10);

            Assert.Contains("تحتاج إفراج/إعادة", headers);
            Assert.DoesNotContain("مغلق (قديم)", headers);
            Assert.Equal(2, worksheet.Cell(5, 10).GetValue<int>());
        }

        [Fact]
        public void HistoryWorkbook_UsesSupplierAsPrimaryParty()
        {
            Guarantee guarantee = _fixture.CreateGuarantee("BG-HISTORY-001");
            guarantee.Id = 1001;
            guarantee.RootId = 1001;
            guarantee.Supplier = "شركة الخدمات الطبية";
            guarantee.Bank = "البنك الأهلي السعودي";
            guarantee.Beneficiary = BusinessPartyDefaults.DefaultBeneficiaryName;
            guarantee.IsCurrent = true;
            guarantee.VersionNumber = 1;
            guarantee.CreatedAt = new DateTime(2026, 1, 1, 9, 0, 0);

            var request = new WorkflowRequest
            {
                SequenceNumber = 1,
                BaseVersionId = guarantee.Id,
                Type = RequestType.Release,
                Status = RequestStatus.Pending,
                RequestDate = new DateTime(2026, 1, 5)
            };

            string outputPath = _fixture.CreateArtifactPath(".xlsx");
            var service = new GuaranteeHistoryDocumentService();

            bool exported = service.ExportHistoryToExcelToPath(
                guarantee,
                new[] { guarantee },
                new[] { request },
                outputPath);

            Assert.True(exported);

            using var workbook = new XLWorkbook(outputPath);
            IXLWorksheet overview = workbook.Worksheet("ملخص السجل");
            IXLWorksheet versions = workbook.Worksheet("الإصدارات");
            List<string> versionHeaders = ReadHeaderRow(versions, 4, 12);

            Assert.Contains("شركة الخدمات الطبية", overview.Cell(2, 1).GetString());
            Assert.Equal("المورد", overview.Cell(10, 1).GetString());
            Assert.Equal("شركة الخدمات الطبية", overview.Cell(10, 2).GetString());
            Assert.DoesNotContain("المستفيد", ReadColumn(overview, 1, 9, 15));
            Assert.Contains("المورد", versionHeaders);
            Assert.DoesNotContain("المستفيد", versionHeaders);
            Assert.Equal("شركة الخدمات الطبية", versions.Cell(5, 5).GetString());
        }

        private static List<string> ReadHeaderRow(IXLWorksheet worksheet, int row, int columns)
        {
            return Enumerable
                .Range(1, columns)
                .Select(column => worksheet.Cell(row, column).GetString())
                .ToList();
        }

        private static List<string> ReadColumn(IXLWorksheet worksheet, int column, int firstRow, int lastRow)
        {
            return Enumerable
                .Range(firstRow, lastRow - firstRow + 1)
                .Select(row => worksheet.Cell(row, column).GetString())
                .ToList();
        }
    }
}
