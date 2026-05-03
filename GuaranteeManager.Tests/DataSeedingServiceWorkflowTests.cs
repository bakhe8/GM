using System.Collections.Generic;
using GuaranteeManager.Services.Seeding;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Xunit;

namespace GuaranteeManager.Tests
{
    [Collection("Database")]
    public sealed class DataSeedingServiceWorkflowTests : DatabaseWorkflowTestBase
    {
        public DataSeedingServiceWorkflowTests(TestEnvironmentFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void DataSeedingService_AppendMode_PreservesExistingRows()
        {
            string originalStorageRoot = AppPaths.StorageRootDirectory;
            string appendStorageRoot = _fixture.CreateStorageRoot($"seed-append-{_fixture.NextToken("ROOT")}");

            try
            {
                _fixture.SwitchStorageRoot(appendStorageRoot);
                DatabaseService.InitializeRuntime();

                DatabaseService database = _fixture.CreateDatabaseService();
                WorkflowService workflow = _fixture.CreateWorkflowService(database);
                var seeding = new DataSeedingService(database, workflow);

                seeding.Seed(clearExistingData: true);
                long initialGuarantees = CountRows("Guarantees");
                long initialRequests = CountRows("WorkflowRequests");
                long initialAttachments = CountRows("Attachments");
                string preservedGuaranteeNo = QueryFirstGuaranteeNo();

                seeding.Seed(clearExistingData: false);

                Assert.True(CountRows("Guarantees") > initialGuarantees);
                Assert.True(CountRows("WorkflowRequests") > initialRequests);
                Assert.True(CountRows("Attachments") > initialAttachments);
                Assert.NotNull(database.GetCurrentGuaranteeByNo(preservedGuaranteeNo));
            }
            finally
            {
                _fixture.SwitchStorageRoot(originalStorageRoot);
                DatabaseService.InitializeRuntime();
            }
        }

        [Fact]
        public void DataSeedingService_ClearMode_ReplacesTimelineEvents()
        {
            string originalStorageRoot = AppPaths.StorageRootDirectory;
            string seedStorageRoot = _fixture.CreateStorageRoot($"seed-clear-{_fixture.NextToken("ROOT")}");

            try
            {
                _fixture.SwitchStorageRoot(seedStorageRoot);
                DatabaseService.InitializeRuntime();

                DatabaseService database = _fixture.CreateDatabaseService();
                WorkflowService workflow = _fixture.CreateWorkflowService(database);
                var seeding = new DataSeedingService(database, workflow);

                seeding.Seed(clearExistingData: true);
                Assert.NotEmpty(database.GetGuaranteeTimelineEvents(QueryFirstGuaranteeId()));
                long initialEvents = CountRows("GuaranteeEvents");

                seeding.Seed(clearExistingData: true);
                Assert.NotEmpty(database.GetGuaranteeTimelineEvents(QueryFirstGuaranteeId()));

                Assert.True(initialEvents > 0);
                Assert.Equal(initialEvents, CountRows("GuaranteeEvents"));
            }
            finally
            {
                _fixture.SwitchStorageRoot(originalStorageRoot);
                DatabaseService.InitializeRuntime();
            }
        }
    }
}
