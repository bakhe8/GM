using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GuaranteeManager.Tests
{
    [Collection("Database")]
    public sealed class WorkflowTerminalLifecycleTests : DatabaseWorkflowTestBase
    {
        public WorkflowTerminalLifecycleTests(TestEnvironmentFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void ClosedLegacyLifecycle_BlocksAllWorkflowCreation()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();
            seed.LifecycleStatus = GuaranteeLifecycleStatus.Closed;

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            Action[] blockedActions =
            {
                () => workflow.CreateExtensionRequest(current.Id, current.ExpiryDate.AddDays(30), "extension", "tester"),
                () => workflow.CreateReductionRequest(current.Id, current.Amount - 1m, "reduction", "tester"),
                () => workflow.CreateReleaseRequest(current.Id, "release", "tester"),
                () => workflow.CreateLiquidationRequest(current.Id, "liquidation", "tester"),
                () => workflow.CreateVerificationRequest(current.Id, "verification", "tester"),
                () => workflow.CreateReplacementRequest(
                    current.Id,
                    $"REP-{_fixture.NextToken("NO")}",
                    current.Supplier,
                    current.Bank,
                    current.Amount,
                    current.ExpiryDate.AddDays(30),
                    current.DateCalendar,
                    current.GuaranteeType,
                    current.Beneficiary,
                    current.ReferenceType,
                    current.ReferenceNumber,
                    "replacement",
                    "tester")
            };

            foreach (Action blockedAction in blockedActions)
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(blockedAction);
                Assert.False(string.IsNullOrWhiteSpace(exception.Message));
            }
        }

        [Fact]
        public void RecordBankResponse_ExecutedRelease_EndsLifecycleWithoutCreatingVersion()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();
            seed.DateCalendar = GuaranteeDateCalendar.Hijri;

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(current.Id, "release", "tester");
            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "released");

            Guarantee releasedGuarantee = database.GetCurrentGuaranteeByRootId(current.RootId ?? current.Id)!;
            WorkflowRequest executedRequest = database.GetWorkflowRequestById(releaseRequest.Id)!;
            WorkflowRequestListItem requestListItem = Assert.Single(database.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RootGuaranteeId = current.RootId ?? current.Id
            }));

            Assert.Equal(GuaranteeLifecycleStatus.Released, releasedGuarantee.LifecycleStatus);
            Assert.Equal(1, releasedGuarantee.VersionNumber);
            Assert.Equal(RequestStatus.Executed, executedRequest.Status);
            Assert.NotNull(executedRequest.ResponseRecordedAt);
            Assert.Null(executedRequest.ResultVersionId);
            Assert.Equal(1, requestListItem.BaseVersionNumber);
            Assert.Null(requestListItem.ResultVersionNumber);
            Assert.Equal("الأول", requestListItem.RelatedVersionLabel);
        }

        [Fact]
        public void RecordBankResponse_ExecutedLiquidation_EndsLifecycleWithoutCreatingVersion()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            WorkflowRequest liquidationRequest = workflow.CreateLiquidationRequest(current.Id, "liquidation", "tester");
            workflow.RecordBankResponse(liquidationRequest.Id, RequestStatus.Executed, "liquidated");

            int rootId = current.RootId ?? current.Id;
            Guarantee liquidatedGuarantee = database.GetCurrentGuaranteeByRootId(rootId)!;
            WorkflowRequest executedRequest = database.GetWorkflowRequestById(liquidationRequest.Id)!;
            List<Guarantee> history = database.GetGuaranteeHistory(current.Id);

            Assert.Equal(current.Id, liquidatedGuarantee.Id);
            Assert.Equal(GuaranteeLifecycleStatus.Liquidated, liquidatedGuarantee.LifecycleStatus);
            Assert.Equal(1, liquidatedGuarantee.VersionNumber);
            Assert.Null(executedRequest.ResultVersionId);
            Assert.Single(history);
        }

        [Fact]
        public void CreateReleaseRequest_AfterExecutedExtension_IsAllowedAndEndsExtendedGuarantee()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            WorkflowRequest extensionRequest = workflow.CreateExtensionRequest(
                current.Id,
                current.ExpiryDate.AddDays(60),
                "extend before release",
                "tester");
            workflow.RecordBankResponse(extensionRequest.Id, RequestStatus.Executed, "extended");

            Guarantee extendedGuarantee = database.GetCurrentGuaranteeByRootId(current.RootId ?? current.Id)!;
            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(extendedGuarantee.Id, "release extended guarantee", "tester");
            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "released");

            Guarantee releasedGuarantee = database.GetCurrentGuaranteeByRootId(current.RootId ?? current.Id)!;
            WorkflowRequest executedRelease = database.GetWorkflowRequestById(releaseRequest.Id)!;
            List<Guarantee> history = database.GetGuaranteeHistory(releasedGuarantee.Id);

            Assert.Equal(2, extendedGuarantee.VersionNumber);
            Assert.Equal(GuaranteeLifecycleStatus.Released, releasedGuarantee.LifecycleStatus);
            Assert.Equal(2, releasedGuarantee.VersionNumber);
            Assert.Null(executedRelease.ResultVersionId);
            Assert.Equal(2, history.Count);
        }

        [Fact]
        public void CreateLiquidationRequest_AfterExecutedReduction_IsAllowedAndEndsReducedGuarantee()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            WorkflowRequest reductionRequest = workflow.CreateReductionRequest(
                current.Id,
                current.Amount - 100m,
                "reduce before liquidation",
                "tester");
            workflow.RecordBankResponse(reductionRequest.Id, RequestStatus.Executed, "reduced");

            Guarantee reducedGuarantee = database.GetCurrentGuaranteeByRootId(current.RootId ?? current.Id)!;
            WorkflowRequest liquidationRequest = workflow.CreateLiquidationRequest(reducedGuarantee.Id, "liquidate reduced guarantee", "tester");
            workflow.RecordBankResponse(liquidationRequest.Id, RequestStatus.Executed, "liquidated");

            Guarantee liquidatedGuarantee = database.GetCurrentGuaranteeByRootId(current.RootId ?? current.Id)!;
            WorkflowRequest executedLiquidation = database.GetWorkflowRequestById(liquidationRequest.Id)!;
            List<Guarantee> history = database.GetGuaranteeHistory(liquidatedGuarantee.Id);

            Assert.Equal(2, reducedGuarantee.VersionNumber);
            Assert.Equal(GuaranteeLifecycleStatus.Liquidated, liquidatedGuarantee.LifecycleStatus);
            Assert.Equal(2, liquidatedGuarantee.VersionNumber);
            Assert.Null(executedLiquidation.ResultVersionId);
            Assert.Equal(2, history.Count);
        }
        [Fact]
        public void CreateTerminalRequests_AfterTerminalExecution_AreBlocked()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee releaseSeed = _fixture.CreateGuarantee();
            Guarantee liquidationSeed = _fixture.CreateGuarantee();

            database.SaveGuarantee(releaseSeed, new List<string>());
            database.SaveGuarantee(liquidationSeed, new List<string>());
            Guarantee releaseCurrent = database.GetCurrentGuaranteeByNo(releaseSeed.GuaranteeNo)!;
            Guarantee liquidationCurrent = database.GetCurrentGuaranteeByNo(liquidationSeed.GuaranteeNo)!;

            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(releaseCurrent.Id, "release", "tester");
            WorkflowRequest liquidationRequest = workflow.CreateLiquidationRequest(liquidationCurrent.Id, "liquidate", "tester");
            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "released");
            workflow.RecordBankResponse(liquidationRequest.Id, RequestStatus.Executed, "liquidated");

            Guarantee releasedGuarantee = database.GetCurrentGuaranteeByRootId(releaseCurrent.RootId ?? releaseCurrent.Id)!;
            Guarantee liquidatedGuarantee = database.GetCurrentGuaranteeByRootId(liquidationCurrent.RootId ?? liquidationCurrent.Id)!;

            InvalidOperationException repeatedRelease = Assert.Throws<InvalidOperationException>(
                () => workflow.CreateReleaseRequest(releasedGuarantee.Id, "repeat release", "tester"));
            InvalidOperationException repeatedLiquidation = Assert.Throws<InvalidOperationException>(
                () => workflow.CreateLiquidationRequest(liquidatedGuarantee.Id, "repeat liquidation", "tester"));

            Assert.Contains("لا يمكن إنشاء طلب إفراج", repeatedRelease.Message);
            Assert.Contains("لا يمكن إنشاء طلب تسييل", repeatedLiquidation.Message);
        }

        [Fact]
        public void CreateReleaseRequest_ForExpiredLifecycle_IsAllowedAndEndsLifecycle()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();
            seed.ExpiryDate = DateTime.Today.AddDays(-5);
            seed.LifecycleStatus = GuaranteeLifecycleStatus.Expired;

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(current.Id, "return expired guarantee", "tester");
            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "released after expiry");

            Guarantee releasedGuarantee = database.GetCurrentGuaranteeByRootId(current.RootId ?? current.Id)!;
            WorkflowRequest executedRequest = database.GetWorkflowRequestById(releaseRequest.Id)!;

            Assert.Equal(GuaranteeLifecycleStatus.Released, releasedGuarantee.LifecycleStatus);
            Assert.Equal(RequestStatus.Executed, executedRequest.Status);
        }

        [Fact]
        public void CreateNonReleaseRequests_ForDateExpiredGuarantee_AreBlocked()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();
            seed.ExpiryDate = DateTime.Today.AddDays(-1);
            seed.LifecycleStatus = GuaranteeLifecycleStatus.Active;

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            var attempts = new Action[]
            {
                () => workflow.CreateExtensionRequest(current.Id, DateTime.Today.AddDays(30), "extension", "tester"),
                () => workflow.CreateReductionRequest(current.Id, current.Amount - 1m, "reduction", "tester"),
                () => workflow.CreateLiquidationRequest(current.Id, "liquidation", "tester"),
                () => workflow.CreateVerificationRequest(current.Id, "verification", "tester"),
                () => workflow.CreateReplacementRequest(
                    current.Id,
                    $"BG-R-{_fixture.NextToken("NO")}",
                    current.Supplier,
                    current.Bank,
                    current.Amount,
                    DateTime.Today.AddDays(90),
                    GuaranteeDateCalendar.Gregorian,
                    current.GuaranteeType,
                    current.Beneficiary,
                    current.ReferenceType,
                    current.ReferenceNumber,
                    "replacement",
                    "tester")
            };

            foreach (Action attempt in attempts)
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(attempt);
                Assert.Contains("منتهي الصلاحية", exception.Message);
            }

            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(current.Id, "release", "tester");
            Assert.Equal(RequestType.Release, releaseRequest.Type);
        }

        [Fact]
        public void RecordBankResponse_ExecutedTerminalRequest_SupersedesOtherPendingRequests()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            int rootId = current.RootId ?? current.Id;

            WorkflowRequest verificationRequest = workflow.CreateVerificationRequest(current.Id, "verification", "tester");
            WorkflowRequest reductionRequest = workflow.CreateReductionRequest(current.Id, current.Amount - 1m, "reduction", "tester");
            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(current.Id, "release", "tester");

            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "released");

            List<WorkflowRequest> requests = database.GetWorkflowRequestsByRootId(rootId);
            Assert.Equal(RequestStatus.Executed, requests.Single(request => request.Id == releaseRequest.Id).Status);
            Assert.Equal(RequestStatus.Superseded, requests.Single(request => request.Id == verificationRequest.Id).Status);
            Assert.Equal(RequestStatus.Superseded, requests.Single(request => request.Id == reductionRequest.Id).Status);
        }

        [Fact]
        public void RecordBankResponse_ExecutedReplacement_CreatesReplacementGuaranteeAndMarksOriginalWithoutNewVersion()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            int originalRootId = current.RootId ?? current.Id;
            string replacementNo = $"BG-R-{_fixture.NextToken("NO")}";
            string replacementImagePath = _fixture.CreateSourceFile(".pdf", "replacement-guarantee-image");

            WorkflowRequest replacementRequest = workflow.CreateReplacementRequest(
                current.Id,
                replacementNo,
                current.Supplier,
                "Replacement Bank",
                current.Amount,
                current.ExpiryDate.AddDays(90),
                GuaranteeDateCalendar.Hijri,
                current.GuaranteeType,
                current.Beneficiary,
                current.ReferenceType,
                current.ReferenceNumber,
                "replacement",
                "tester",
                new List<AttachmentInput>
                {
                    new(replacementImagePath, AttachmentDocumentType.GuaranteeImage)
                });
            workflow.RecordBankResponse(replacementRequest.Id, RequestStatus.Executed, "replaced");

            Guarantee originalAfterReplacement = database.GetCurrentGuaranteeByRootId(originalRootId)!;
            Guarantee replacementGuarantee = database.GetCurrentGuaranteeByNo(replacementNo)!;
            WorkflowRequest executedRequest = database.GetWorkflowRequestById(replacementRequest.Id)!;
            List<Guarantee> originalHistory = database.GetGuaranteeHistory(current.Id);
            AttachmentRecord replacementAttachment = Assert.Single(replacementGuarantee.Attachments);

            Assert.Equal(current.Id, originalAfterReplacement.Id);
            Assert.Equal(GuaranteeLifecycleStatus.Replaced, originalAfterReplacement.LifecycleStatus);
            Assert.Equal(GuaranteeDateCalendar.Hijri, replacementGuarantee.DateCalendar);
            Assert.Contains("هـ", replacementGuarantee.WorkflowDisplayLabel);
            Assert.Equal(1, originalAfterReplacement.VersionNumber);
            Assert.Equal(replacementGuarantee.RootId, originalAfterReplacement.ReplacedByRootId);
            Assert.Equal(originalRootId, replacementGuarantee.ReplacesRootId);
            Assert.Equal(1, replacementGuarantee.VersionNumber);
            Assert.Equal(GuaranteeLifecycleStatus.Active, replacementGuarantee.LifecycleStatus);
            Assert.Equal(replacementGuarantee.Id, executedRequest.ResultVersionId);
            Assert.Single(executedRequest.ReplacementAttachments);
            Assert.Equal(AttachmentDocumentType.GuaranteeImage, replacementAttachment.DocumentType);
            Assert.True(File.Exists(replacementAttachment.FilePath));
            Assert.Single(originalHistory);
        }
    }
}
