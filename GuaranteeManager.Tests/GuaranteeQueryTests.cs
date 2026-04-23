using System;
using System.Collections.Generic;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using Xunit;

namespace GuaranteeManager.Tests
{
    [Collection("Database")]
    public sealed class GuaranteeQueryTests
    {
        private readonly TestEnvironmentFixture _fixture;

        public GuaranteeQueryTests(TestEnvironmentFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void QueryGuarantees_SearchText_MatchesSupplierAndGuaranteeNo()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string token = _fixture.NextToken("SRQ");
            Guarantee match = _fixture.CreateGuarantee();
            match.Supplier = $"شركة-{token}";
            Guarantee other = _fixture.CreateGuarantee();

            database.SaveGuarantee(match, new List<string>());
            database.SaveGuarantee(other, new List<string>());

            List<Guarantee> results = database.QueryGuarantees(new GuaranteeQueryOptions
            {
                SearchText = token,
                SortMode = GuaranteeQuerySortMode.CreatedAtDescending
            });

            Assert.Contains(results, g => g.Supplier == match.Supplier);
            Assert.DoesNotContain(results, g => g.GuaranteeNo == other.GuaranteeNo);
        }

        [Fact]
        public void QueryGuarantees_LifecycleStatus_ReturnsOnlyMatchingStatus()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            Guarantee activeSeed = _fixture.CreateGuarantee();
            Guarantee releasedSeed = _fixture.CreateGuarantee();

            database.SaveGuarantee(activeSeed, new List<string>());
            database.SaveGuarantee(releasedSeed, new List<string>());

            Guarantee releasedCurrent = database.GetCurrentGuaranteeByNo(releasedSeed.GuaranteeNo)!;
            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(releasedCurrent.Id, "lifecycle-test", "tester");
            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "released");

            List<Guarantee> activeOnly = database.QueryGuarantees(new GuaranteeQueryOptions
            {
                LifecycleStatus = GuaranteeLifecycleStatus.Active,
                SortMode = GuaranteeQuerySortMode.CreatedAtDescending
            });

            List<Guarantee> releasedOnly = database.QueryGuarantees(new GuaranteeQueryOptions
            {
                LifecycleStatus = GuaranteeLifecycleStatus.Released,
                SortMode = GuaranteeQuerySortMode.CreatedAtDescending
            });

            Assert.Contains(activeOnly, g => g.GuaranteeNo == activeSeed.GuaranteeNo);
            Assert.DoesNotContain(activeOnly, g => g.GuaranteeNo == releasedSeed.GuaranteeNo);
            Assert.Contains(releasedOnly, g => g.GuaranteeNo == releasedSeed.GuaranteeNo);
            Assert.DoesNotContain(releasedOnly, g => g.GuaranteeNo == activeSeed.GuaranteeNo);
        }

        [Fact]
        public void CountGuarantees_WithAndWithoutFilter_ReturnsCorrectCounts()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string token = _fixture.NextToken("CNT");
            Guarantee guarantee = _fixture.CreateGuarantee();
            guarantee.Bank = $"بنك-{token}";

            int baselineTotal = database.CountGuarantees();
            int baselineFiltered = database.CountGuarantees(new GuaranteeQueryOptions { SearchText = token });

            database.SaveGuarantee(guarantee, new List<string>());

            Assert.Equal(baselineTotal + 1, database.CountGuarantees());
            Assert.Equal(baselineFiltered + 1, database.CountGuarantees(new GuaranteeQueryOptions { SearchText = token }));
        }

        [Fact]
        public void IsGuaranteeNoUnique_ReturnsFalseForExistingNumber()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            Guarantee seed = _fixture.CreateGuarantee();
            database.SaveGuarantee(seed, new List<string>());

            Assert.False(database.IsGuaranteeNoUnique(seed.GuaranteeNo));
            Assert.True(database.IsGuaranteeNoUnique($"NONEXISTENT-{_fixture.NextToken("UNQ")}"));
        }

        [Fact]
        public void QueryGuarantees_Limit_CappsResultCount()
        {
            DatabaseService database = _fixture.CreateDatabaseService();

            for (int i = 0; i < 5; i++)
            {
                database.SaveGuarantee(_fixture.CreateGuarantee(), new List<string>());
            }

            List<Guarantee> results = database.QueryGuarantees(new GuaranteeQueryOptions { Limit = 2 });

            Assert.Equal(2, results.Count);
        }

        [Fact]
        public void HasPendingWorkflowRequest_ReturnsTrueWhenPendingExists()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            Guarantee seed = _fixture.CreateGuarantee();
            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            int rootId = current.RootId ?? current.Id;

            Assert.False(database.HasPendingWorkflowRequest(rootId, RequestType.Verification));

            workflow.CreateVerificationRequest(current.Id, "pending-check", "tester");

            Assert.True(database.HasPendingWorkflowRequest(rootId, RequestType.Verification));
        }

        [Fact]
        public void GetPendingWorkflowRequestCount_IncreasesWhenRequestCreated()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            int baseline = database.GetPendingWorkflowRequestCount();

            Guarantee seed = _fixture.CreateGuarantee();
            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            workflow.CreateVerificationRequest(current.Id, "count-check", "tester");

            Assert.Equal(baseline + 1, database.GetPendingWorkflowRequestCount());
        }

        [Fact]
        public void GetGuaranteesEligibleForExtension_ReturnsOnlyActiveGuarantees()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            Guarantee active = _fixture.CreateGuarantee();
            Guarantee expiredLifecycle = _fixture.CreateGuarantee();
            expiredLifecycle.LifecycleStatus = GuaranteeLifecycleStatus.Expired;

            database.SaveGuarantee(active, new List<string>());
            database.SaveGuarantee(expiredLifecycle, new List<string>());

            List<Guarantee> eligible = workflow.GetGuaranteesEligibleForExtension();

            Assert.Contains(eligible, item => item.GuaranteeNo == active.GuaranteeNo);
            Assert.DoesNotContain(eligible, item => item.GuaranteeNo == expiredLifecycle.GuaranteeNo);
        }

        [Fact]
        public void GetGuaranteesEligibleForReduction_ExcludesZeroAmountGuarantees()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            Guarantee positiveAmount = _fixture.CreateGuarantee();
            Guarantee zeroAmount = _fixture.CreateGuarantee();
            zeroAmount.Amount = 0;

            database.SaveGuarantee(positiveAmount, new List<string>());
            database.SaveGuarantee(zeroAmount, new List<string>());

            List<Guarantee> eligible = workflow.GetGuaranteesEligibleForReduction();

            Assert.Contains(eligible, item => item.GuaranteeNo == positiveAmount.GuaranteeNo);
            Assert.DoesNotContain(eligible, item => item.GuaranteeNo == zeroAmount.GuaranteeNo);
        }

        [Fact]
        public void GetGuaranteesEligibleForAnnulment_ReturnsReleasedAndLiquidatedGuarantees()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            Guarantee activeSeed = _fixture.CreateGuarantee();
            Guarantee releasedSeed = _fixture.CreateGuarantee();
            Guarantee liquidatedSeed = _fixture.CreateGuarantee();

            database.SaveGuarantee(activeSeed, new List<string>());
            database.SaveGuarantee(releasedSeed, new List<string>());
            database.SaveGuarantee(liquidatedSeed, new List<string>());

            Guarantee releasedCurrent = database.GetCurrentGuaranteeByNo(releasedSeed.GuaranteeNo)!;
            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(releasedCurrent.Id, "annulment-release", "tester");
            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "released");

            Guarantee liquidatedCurrent = database.GetCurrentGuaranteeByNo(liquidatedSeed.GuaranteeNo)!;
            WorkflowRequest liquidationRequest = workflow.CreateLiquidationRequest(liquidatedCurrent.Id, "annulment-liquidation", "tester");
            workflow.RecordBankResponse(liquidationRequest.Id, RequestStatus.Executed, "liquidated");

            List<Guarantee> eligible = workflow.GetGuaranteesEligibleForAnnulment();

            Assert.Contains(eligible, item => item.GuaranteeNo == releasedSeed.GuaranteeNo);
            Assert.Contains(eligible, item => item.GuaranteeNo == liquidatedSeed.GuaranteeNo);
            Assert.DoesNotContain(eligible, item => item.GuaranteeNo == activeSeed.GuaranteeNo);
        }

        [Fact]
        public void ExecuteExtensionRequest_UpdatesExpiryAndCreatesNewVersion()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            Guarantee seed = _fixture.CreateGuarantee();
            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            DateTime newExpiry = current.ExpiryDate.AddDays(30);

            WorkflowRequest extensionRequest = workflow.CreateExtensionRequest(
                current.Id,
                newExpiry,
                "extension-execute",
                "tester");

            workflow.RecordBankResponse(extensionRequest.Id, RequestStatus.Executed, "approved");

            Guarantee extended = database.GetCurrentGuaranteeByRootId(current.RootId ?? current.Id)!;

            Assert.Equal(newExpiry.Date, extended.ExpiryDate.Date);
            Assert.Equal(2, extended.VersionNumber);
            Assert.True(extended.IsCurrent);
        }

        [Fact]
        public void CountWorkflowRequests_ByRequestType_CountsCorrectly()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            Guarantee seed = _fixture.CreateGuarantee();
            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            int baselineVerification = database.CountWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RequestType = RequestType.Verification
            });

            workflow.CreateVerificationRequest(current.Id, "count-type-check", "tester");

            Assert.Equal(
                baselineVerification + 1,
                database.CountWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RequestType = RequestType.Verification
                }));
        }
    }
}
