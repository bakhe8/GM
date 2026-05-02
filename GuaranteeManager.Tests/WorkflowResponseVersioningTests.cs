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
    public sealed class WorkflowResponseVersioningTests : DatabaseWorkflowTestBase
    {
        public WorkflowResponseVersioningTests(TestEnvironmentFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void RecordBankResponse_StaleExtensionAfterExpiryChange_KeepsRequestPending()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            WorkflowRequest extensionRequest = workflow.CreateExtensionRequest(
                current.Id,
                current.ExpiryDate.AddDays(60),
                "extend from original date",
                "tester");

            current.ExpiryDate = current.ExpiryDate.AddDays(10);
            int updatedVersionId = database.UpdateGuarantee(current, new List<string>(), new List<AttachmentRecord>());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => workflow.RecordBankResponse(extensionRequest.Id, RequestStatus.Executed, "approved"));
            WorkflowRequest stillPending = database.GetWorkflowRequestById(extensionRequest.Id)!;
            Guarantee latest = database.GetCurrentGuaranteeByRootId(current.RootId ?? current.Id)!;

            Assert.Contains("تاريخ الانتهاء الحالي تغيّر", exception.Message);
            Assert.Equal(RequestStatus.Pending, stillPending.Status);
            Assert.Equal(updatedVersionId, latest.Id);
            Assert.Equal(2, latest.VersionNumber);
        }

        [Fact]
        public void RecordBankResponse_StaleReductionAfterAmountChange_KeepsRequestPending()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            WorkflowRequest reductionRequest = workflow.CreateReductionRequest(
                current.Id,
                current.Amount - 100m,
                "reduce from original amount",
                "tester");

            current.Amount += 250m;
            int updatedVersionId = database.UpdateGuarantee(current, new List<string>(), new List<AttachmentRecord>());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => workflow.RecordBankResponse(reductionRequest.Id, RequestStatus.Executed, "approved"));
            WorkflowRequest stillPending = database.GetWorkflowRequestById(reductionRequest.Id)!;
            Guarantee latest = database.GetCurrentGuaranteeByRootId(current.RootId ?? current.Id)!;

            Assert.Contains("مبلغ الضمان الحالي تغيّر", exception.Message);
            Assert.Equal(RequestStatus.Pending, stillPending.Status);
            Assert.Equal(updatedVersionId, latest.Id);
            Assert.Equal(2, latest.VersionNumber);
        }

        [Fact]
        public void CreateReductionRequest_RejectsAmountWithMoreThanTwoHalalaDigits()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => workflow.CreateReductionRequest(
                    current.Id,
                    current.Amount - 100.123m,
                    "invalid reduction amount",
                    "tester"));

            Assert.Contains("خانتين للهلل", exception.Message);
        }

        [Fact]
        public void CreateExtensionRequest_AfterExecutedExtension_IsAllowedOnCurrentVersion()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee original = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            WorkflowRequest firstExtension = workflow.CreateExtensionRequest(
                original.Id,
                original.ExpiryDate.AddDays(30),
                "first extension",
                "tester");
            workflow.RecordBankResponse(firstExtension.Id, RequestStatus.Executed, "first approved");

            Guarantee extended = database.GetCurrentGuaranteeByRootId(original.RootId ?? original.Id)!;
            WorkflowRequest secondExtension = workflow.CreateExtensionRequest(
                extended.Id,
                extended.ExpiryDate.AddDays(30),
                "second extension",
                "tester");
            workflow.RecordBankResponse(secondExtension.Id, RequestStatus.Executed, "second approved");

            Guarantee twiceExtended = database.GetCurrentGuaranteeByRootId(original.RootId ?? original.Id)!;
            List<Guarantee> history = database.GetGuaranteeHistory(twiceExtended.Id);

            Assert.Equal(3, twiceExtended.VersionNumber);
            Assert.Equal(GuaranteeLifecycleStatus.Active, twiceExtended.LifecycleStatus);
            Assert.Equal(3, history.Count);
        }

        [Fact]
        public void CreateReductionRequest_AfterExecutedReduction_IsAllowedOnCurrentVersion()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee original = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            WorkflowRequest firstReduction = workflow.CreateReductionRequest(
                original.Id,
                original.Amount - 100m,
                "first reduction",
                "tester");
            workflow.RecordBankResponse(firstReduction.Id, RequestStatus.Executed, "first approved");

            Guarantee reduced = database.GetCurrentGuaranteeByRootId(original.RootId ?? original.Id)!;
            WorkflowRequest secondReduction = workflow.CreateReductionRequest(
                reduced.Id,
                reduced.Amount - 100m,
                "second reduction",
                "tester");
            workflow.RecordBankResponse(secondReduction.Id, RequestStatus.Executed, "second approved");

            Guarantee twiceReduced = database.GetCurrentGuaranteeByRootId(original.RootId ?? original.Id)!;
            List<Guarantee> history = database.GetGuaranteeHistory(twiceReduced.Id);

            Assert.Equal(3, twiceReduced.VersionNumber);
            Assert.Equal(GuaranteeLifecycleStatus.Active, twiceReduced.LifecycleStatus);
            Assert.Equal(3, history.Count);
        }
    }
}
