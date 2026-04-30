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
    public sealed class GuaranteeTimelineBackfillTests : DatabaseWorkflowTestBase
    {
        public GuaranteeTimelineBackfillTests(TestEnvironmentFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void GetGuaranteeTimelineEvents_BackfillsWorkflowLifecycleWithoutDuplicatingEvents()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee original = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            WorkflowRequest extensionRequest = workflow.CreateExtensionRequest(
                original.Id,
                original.ExpiryDate.AddDays(60),
                "extend before release",
                "tester");
            workflow.RecordBankResponse(extensionRequest.Id, RequestStatus.Executed, "extended");

            Guarantee extended = database.GetCurrentGuaranteeByRootId(original.RootId ?? original.Id)!;
            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(
                extended.Id,
                "release extended guarantee",
                "tester");
            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "released");

            List<GuaranteeTimelineEvent> events = database.GetGuaranteeTimelineEvents(extended.Id);
            List<GuaranteeTimelineEvent> secondRead = database.GetGuaranteeTimelineEvents(extended.Id);

            Assert.Equal(events.Count, secondRead.Count);
            Assert.Contains(events, item => item.EventType == "GuaranteeCreated" && item.GuaranteeId == original.Id);
            Assert.Contains(events, item => item.EventType == "WorkflowRequestCreated" && item.WorkflowRequestId == extensionRequest.Id);
            Assert.Contains(events, item => item.EventType == "WorkflowResponseRecorded" && item.WorkflowRequestId == extensionRequest.Id);

            GuaranteeTimelineEvent versionEvent = Assert.Single(
                events,
                item => item.EventType == "GuaranteeVersionCreated" && item.GuaranteeId == extended.Id);
            Assert.Contains("شروط", versionEvent.Details);
            Assert.DoesNotContain("مفرج", versionEvent.Details);

            GuaranteeTimelineEvent releaseResponse = Assert.Single(
                events,
                item => item.EventType == "WorkflowResponseRecorded" && item.WorkflowRequestId == releaseRequest.Id);
            Assert.Equal("تسجيل رد طلب إفراج", releaseResponse.Title);
            Assert.Contains("تم إنهاء دورة حياة الضمان بالإفراج", releaseResponse.Details);

            using SqliteConnection connection = SqliteConnectionFactory.OpenForPath(AppPaths.DatabasePath);
            using SqliteCommand updateCommand = connection.CreateCommand();
            updateCommand.CommandText = "UPDATE GuaranteeEvents SET Title = Title WHERE Id = $id";
            updateCommand.Parameters.AddWithValue("$id", releaseResponse.Id);
            Assert.Throws<SqliteException>(() => updateCommand.ExecuteNonQuery());
        }

        [Fact]
        public void GetGuaranteeTimelineEvents_BackfillsLateResponseDocumentAsSeparateEvent()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(
                current.Id,
                "release without response document",
                "tester");
            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "released without document");

            List<GuaranteeTimelineEvent> beforeAttach = database.GetGuaranteeTimelineEvents(current.Id);
            Assert.DoesNotContain(beforeAttach, item => item.EventType == "WorkflowResponseDocumentAttached");

            string responsePath = _fixture.CreateSourceFile(".pdf", "late-response-document");
            workflow.AttachResponseDocumentToClosedRequest(releaseRequest.Id, responsePath, "attached later");

            List<GuaranteeTimelineEvent> afterAttach = database.GetGuaranteeTimelineEvents(current.Id);
            List<GuaranteeTimelineEvent> secondRead = database.GetGuaranteeTimelineEvents(current.Id);

            Assert.Equal(afterAttach.Count, secondRead.Count);
            GuaranteeTimelineEvent documentEvent = Assert.Single(
                afterAttach,
                item => item.EventType == "WorkflowResponseDocumentAttached" && item.WorkflowRequestId == releaseRequest.Id);
            Assert.Equal("إلحاق مستند رد البنك", documentEvent.Title);
            Assert.Equal("مرفق", documentEvent.Status);
            Assert.Contains(".pdf", documentEvent.Details);
        }
    }
}
