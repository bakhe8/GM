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
    public sealed class WorkflowRequestQueryDatabaseTests : DatabaseWorkflowTestBase
    {
        public WorkflowRequestQueryDatabaseTests(TestEnvironmentFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void CreateWorkflowRequests_AssignsSequentialNumbersAndCreatesLetters()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            WorkflowRequest extensionRequest = workflow.CreateExtensionRequest(
                current.Id,
                current.ExpiryDate.AddDays(15),
                "extend",
                "tester");
            WorkflowRequest verificationRequest = workflow.CreateVerificationRequest(
                current.Id,
                "verify",
                "tester");

            List<WorkflowRequest> requests = database.GetWorkflowRequestsByRootId(current.RootId ?? current.Id);

            Assert.Equal(1, extensionRequest.SequenceNumber);
            Assert.Equal(2, verificationRequest.SequenceNumber);
            Assert.True(File.Exists(extensionRequest.LetterFilePath));
            Assert.True(File.Exists(verificationRequest.LetterFilePath));
            Assert.Equal(2, requests.Count);
            Assert.Contains(requests, item => item.Id == extensionRequest.Id && item.SequenceNumber == 1);
            Assert.Contains(requests, item => item.Id == verificationRequest.Id && item.SequenceNumber == 2);
        }

        [Fact]
        public void QueryWorkflowRequests_PendingOrMissingResponseOnly_ReturnsPendingAndExecutedWithoutResponse()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            Guarantee pendingGuarantee = _fixture.CreateGuarantee();
            Guarantee executedGuarantee = _fixture.CreateGuarantee();

            database.SaveGuarantee(pendingGuarantee, new List<string>());
            database.SaveGuarantee(executedGuarantee, new List<string>());

            Guarantee currentPending = database.GetCurrentGuaranteeByNo(pendingGuarantee.GuaranteeNo)!;
            Guarantee currentExecuted = database.GetCurrentGuaranteeByNo(executedGuarantee.GuaranteeNo)!;

            WorkflowRequest pendingVerification = workflow.CreateVerificationRequest(currentPending.Id, "pending", "tester");
            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(currentExecuted.Id, "release", "tester");
            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "approved-without-file");

            List<WorkflowRequestListItem> followUpRequests = database.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                PendingOrMissingResponseOnly = true,
                SortMode = WorkflowRequestQuerySortMode.RequestDateAscending
            });

            List<WorkflowRequestListItem> executedWithoutResponse = database.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RootGuaranteeId = currentExecuted.RootId ?? currentExecuted.Id,
                RequestStatus = RequestStatus.Executed,
                PendingOrMissingResponseOnly = true,
                SortMode = WorkflowRequestQuerySortMode.ActivityDateDescending
            });

            Assert.Contains(followUpRequests, item => item.Request.Id == pendingVerification.Id && item.Request.Status == RequestStatus.Pending);
            Assert.Contains(followUpRequests, item => item.Request.Id == releaseRequest.Id && item.Request.Status == RequestStatus.Executed && !item.Request.HasResponseDocument);
            Assert.Single(executedWithoutResponse);
            Assert.Equal(releaseRequest.Id, executedWithoutResponse[0].Request.Id);
            Assert.False(executedWithoutResponse[0].Request.HasResponseDocument);
        }

        [Fact]
        public void QueryWorkflowRequests_RootGuaranteeId_ReturnsOnlyMatchingSeries()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            Guarantee firstGuaranteeSeed = _fixture.CreateGuarantee();
            Guarantee secondGuaranteeSeed = _fixture.CreateGuarantee();

            database.SaveGuarantee(firstGuaranteeSeed, new List<string>());
            database.SaveGuarantee(secondGuaranteeSeed, new List<string>());

            Guarantee firstGuarantee = database.GetCurrentGuaranteeByNo(firstGuaranteeSeed.GuaranteeNo)!;
            Guarantee secondGuarantee = database.GetCurrentGuaranteeByNo(secondGuaranteeSeed.GuaranteeNo)!;

            WorkflowRequest firstRequest = workflow.CreateVerificationRequest(firstGuarantee.Id, "first", "tester");
            WorkflowRequest secondRequest = workflow.CreateVerificationRequest(secondGuarantee.Id, "second", "tester");

            List<WorkflowRequestListItem> filteredRequests = database.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RootGuaranteeId = firstGuarantee.RootId ?? firstGuarantee.Id,
                SortMode = WorkflowRequestQuerySortMode.RequestDateDescending
            });

            Assert.Single(filteredRequests);
            Assert.Equal(firstRequest.Id, filteredRequests[0].Request.Id);
            Assert.DoesNotContain(filteredRequests, item => item.Request.Id == secondRequest.Id);
        }
    }
}
