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
    public sealed class WorkflowResponseDocumentTests : DatabaseWorkflowTestBase
    {
        public WorkflowResponseDocumentTests(TestEnvironmentFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void AttachResponseDocumentToClosedRequest_PreservesExecutionAndAddsLateDocument()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();
            string responseDocumentPath = _fixture.CreateSourceFile(".pdf", "late-response");

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(current.Id, "release", "tester");
            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "approved-without-file");
            WorkflowRequest executedBeforeAttach = database.GetWorkflowRequestById(releaseRequest.Id)!;

            workflow.AttachResponseDocumentToClosedRequest(releaseRequest.Id, responseDocumentPath, "late doc");

            WorkflowRequest executedAfterAttach = database.GetWorkflowRequestById(releaseRequest.Id)!;

            Assert.Equal(RequestStatus.Executed, executedAfterAttach.Status);
            Assert.True(executedAfterAttach.HasResponseDocument);
            Assert.True(File.Exists(executedAfterAttach.ResponseFilePath));
            Assert.Equal(executedBeforeAttach.ResponseRecordedAt, executedAfterAttach.ResponseRecordedAt);
            Assert.Contains("approved-without-file", executedAfterAttach.ResponseNotes);
            Assert.Contains("late doc", executedAfterAttach.ResponseNotes);
        }

        [Fact]
        public void AttachResponseDocumentToClosedRequest_FillsMissingResponseRecordedAt()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();
            string responseDocumentPath = _fixture.CreateSourceFile(".pdf", "late-response-with-missing-date");

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(current.Id, "release", "tester");
            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "approved-without-file");

            using (SqliteConnection connection = SqliteConnectionFactory.OpenForPath(AppPaths.DatabasePath))
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE WorkflowRequests SET ResponseRecordedAt = NULL WHERE Id = $id";
                command.Parameters.AddWithValue("$id", releaseRequest.Id);
                command.ExecuteNonQuery();
            }

            workflow.AttachResponseDocumentToClosedRequest(releaseRequest.Id, responseDocumentPath, "late doc");

            WorkflowRequest executedAfterAttach = database.GetWorkflowRequestById(releaseRequest.Id)!;
            Assert.NotNull(executedAfterAttach.ResponseRecordedAt);
            Assert.True(executedAfterAttach.HasResponseDocument);
        }
    }
}
