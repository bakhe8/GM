using System;
using System.Collections.Generic;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Xunit;

namespace GuaranteeManager.Tests
{
    [Collection("Database")]
    public sealed class WorkspaceReportCatalogTests
    {
        private readonly TestEnvironmentFixture _fixture;

        public WorkspaceReportCatalogTests(TestEnvironmentFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void Run_CurrentGuaranteesWithoutAttachments_ExportsOnlyMatchingCurrentRows()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            Guarantee withoutAttachmentsSeed = _fixture.CreateGuarantee();
            Guarantee withAttachmentsSeed = _fixture.CreateGuarantee();

            database.SaveGuarantee(withoutAttachmentsSeed, new List<string>());
            database.SaveGuarantee(withAttachmentsSeed, new List<string> { _fixture.CreateSourceFile(contents: "attachment") });

            var excel = new CaptureExcelService();

            bool exported = WorkspaceReportCatalog.Run("portfolio.no-attachments", database, excel);

            Assert.True(exported);
            Assert.NotNull(excel.CurrentWithoutAttachments);
            Assert.Contains(excel.CurrentWithoutAttachments!, guarantee => guarantee.GuaranteeNo == withoutAttachmentsSeed.GuaranteeNo);
            Assert.DoesNotContain(excel.CurrentWithoutAttachments!, guarantee => guarantee.GuaranteeNo == withAttachmentsSeed.GuaranteeNo);
        }

        [Fact]
        public void Run_GuaranteeVersionsWithoutAttachments_ExportsHistoryRowsAcrossVersions()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            Guarantee noAttachmentSeed = _fixture.CreateGuarantee();
            database.SaveGuarantee(noAttachmentSeed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(noAttachmentSeed.GuaranteeNo)!;

            WorkflowRequest extension = workflow.CreateExtensionRequest(
                current.Id,
                current.ExpiryDate.AddDays(15),
                "history-test",
                "tester");
            workflow.RecordBankResponse(extension.Id, RequestStatus.Executed, "executed-without-response");

            Guarantee withAttachmentSeed = _fixture.CreateGuarantee();
            database.SaveGuarantee(withAttachmentSeed, new List<string> { _fixture.CreateSourceFile(contents: "attachment") });

            var excel = new CaptureExcelService();

            bool exported = WorkspaceReportCatalog.Run("portfolio.versions.no-attachments", database, excel);

            Assert.True(exported);
            Assert.NotNull(excel.VersionRowsWithoutAttachments);

            List<Guarantee> matchingRows = excel.VersionRowsWithoutAttachments!
                .Where(guarantee => guarantee.GuaranteeNo == noAttachmentSeed.GuaranteeNo)
                .OrderBy(guarantee => guarantee.VersionNumber)
                .ToList();

            Assert.Equal(new[] { 1, 2 }, matchingRows.Select(guarantee => guarantee.VersionNumber).ToArray());
            Assert.All(matchingRows, row => Assert.Equal(0, row.AttachmentCount));
            Assert.DoesNotContain(excel.VersionRowsWithoutAttachments!, guarantee => guarantee.GuaranteeNo == withAttachmentSeed.GuaranteeNo);
        }

        private sealed class CaptureExcelService : IExcelService
        {
            public string? LastOutputPath => null;

            public IReadOnlyList<Guarantee>? CurrentWithoutAttachments { get; private set; }

            public IReadOnlyList<Guarantee>? VersionRowsWithoutAttachments { get; private set; }

            public bool ExportGuarantees(IReadOnlyList<Guarantee> guarantees) => throw new NotSupportedException();
            public bool ExportGuaranteesByBank(string bank, IReadOnlyList<Guarantee> guarantees) => throw new NotSupportedException();
            public bool ExportGuaranteesBySupplier(string supplier, IReadOnlyList<Guarantee> guarantees) => throw new NotSupportedException();
            public bool ExportGuaranteesByTemporalStatus(string temporalStatus, IReadOnlyList<Guarantee> guarantees) => throw new NotSupportedException();
            public bool ExportGuaranteesByLifecycleStatus(string lifecycleStatus, IReadOnlyList<Guarantee> guarantees) => throw new NotSupportedException();
            public bool ExportGuaranteesByType(string guaranteeType, IReadOnlyList<Guarantee> guarantees) => throw new NotSupportedException();
            public bool ExportSingleGuaranteeReport(Guarantee guarantee) => throw new NotSupportedException();
            public bool ExportDailyFollowUpReport(IReadOnlyList<Guarantee> guarantees, IReadOnlyList<WorkflowRequestListItem> requests) => throw new NotSupportedException();
            public bool ExportExpiringSoonGuarantees(IReadOnlyList<Guarantee> guarantees) => throw new NotSupportedException();
            public bool ExportExpiredActiveGuarantees(IReadOnlyList<Guarantee> guarantees) => throw new NotSupportedException();
            public bool ExportGuaranteeStatisticsByBank(IReadOnlyList<Guarantee> guarantees) => throw new NotSupportedException();
            public bool ExportGuaranteeStatisticsBySupplier(IReadOnlyList<Guarantee> guarantees) => throw new NotSupportedException();
            public bool ExportGuaranteePortfolioSummary(IReadOnlyList<Guarantee> guarantees, IReadOnlyList<WorkflowRequestListItem> requests) => throw new NotSupportedException();
            public bool ExportGuaranteeVersionCounts(IReadOnlyList<Guarantee> guarantees) => throw new NotSupportedException();

            public bool ExportGuaranteesWithoutAttachments(IReadOnlyList<Guarantee> guarantees)
            {
                CurrentWithoutAttachments = guarantees.ToList();
                return true;
            }

            public bool ExportGuaranteeVersionsWithoutAttachments(IReadOnlyList<Guarantee> guarantees)
            {
                VersionRowsWithoutAttachments = guarantees.ToList();
                return true;
            }

            public bool ExportWorkflowRequestsByStatus(RequestStatus status, IReadOnlyList<WorkflowRequestListItem> requests) => throw new NotSupportedException();
            public bool ExportWorkflowRequestsByType(RequestType type, IReadOnlyList<WorkflowRequestListItem> requests) => throw new NotSupportedException();
            public bool ExportWorkflowRequestsByTypeAndStatus(RequestType type, RequestStatus status, IReadOnlyList<WorkflowRequestListItem> requests) => throw new NotSupportedException();
            public bool ExportExecutedRequestsWithEffect(IReadOnlyList<WorkflowRequestListItem> requests) => throw new NotSupportedException();
            public bool ExportClosedWithoutExecutionRequests(IReadOnlyList<WorkflowRequestListItem> requests) => throw new NotSupportedException();
            public bool ExportSupersededRequests(IReadOnlyList<WorkflowRequestListItem> requests) => throw new NotSupportedException();
            public bool ExportRequestsWithoutResponseDocument(IReadOnlyList<WorkflowRequestListItem> requests) => throw new NotSupportedException();
            public bool ExportPendingWorkflowRequestsByType(RequestType type, IReadOnlyList<WorkflowRequestListItem> requests) => throw new NotSupportedException();
            public bool ExportPendingRequestsByBank(string bank, IReadOnlyList<WorkflowRequestListItem> requests) => throw new NotSupportedException();
            public bool ExportWorkflowRequestsByBank(string bank, IReadOnlyList<WorkflowRequestListItem> requests) => throw new NotSupportedException();
            public bool ExportOldestPendingRequests(IReadOnlyList<WorkflowRequestListItem> requests, int topCount = 10) => throw new NotSupportedException();
            public bool ExportExecutedExtensionsThisMonth(IReadOnlyList<WorkflowRequestListItem> requests, DateTime periodStart, DateTime periodEnd) => throw new NotSupportedException();
            public bool ExportActivePurchaseOrderOnlyGuarantees(IReadOnlyList<Guarantee> guarantees) => throw new NotSupportedException();
            public bool ExportContractRelatedReleasedInPeriod(IReadOnlyList<WorkflowRequestListItem> requests, DateTime periodStart, DateTime periodEnd) => throw new NotSupportedException();
            public bool ExportEmployeeContractRequestsInPeriod(string employeeName, IReadOnlyList<WorkflowRequestListItem> requests, DateTime periodStart, DateTime periodEnd) => throw new NotSupportedException();
            public bool ExportExpiredPurchaseOrderOnlyWithoutExecutedExtension(IReadOnlyList<Guarantee> guarantees) => throw new NotSupportedException();
        }
    }
}
