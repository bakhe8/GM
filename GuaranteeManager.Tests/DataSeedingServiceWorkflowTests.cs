#if DEBUG
using System.Collections.Generic;
using GuaranteeManager.Development;
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
    }
}
#endif
