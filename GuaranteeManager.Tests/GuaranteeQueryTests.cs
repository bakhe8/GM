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
        public void QueryGuarantees_LimitAndOffset_ReturnsExpectedSqlWindow()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string bankToken = $"Paging Bank {_fixture.NextToken("PAGE")}";

            for (int i = 0; i < 8; i++)
            {
                Guarantee guarantee = _fixture.CreateGuarantee($"BG-PAGE-{bankToken}-{i:D2}");
                guarantee.Bank = bankToken;
                guarantee.ExpiryDate = DateTime.Today.AddDays(i);
                database.SaveGuarantee(guarantee, new List<string>());
            }

            List<Guarantee> results = database.QueryGuarantees(new GuaranteeQueryOptions
            {
                Bank = bankToken,
                Limit = 3,
                Offset = 4,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            });

            Assert.Equal(
                new[]
                {
                    $"BG-PAGE-{bankToken}-04",
                    $"BG-PAGE-{bankToken}-05",
                    $"BG-PAGE-{bankToken}-06"
                },
                results.Select(guarantee => guarantee.GuaranteeNo));
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
        public void ActiveButDateExpiredGuarantees_AreExcludedFromNonReleaseEligibilityLists()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            Guarantee expiredActive = _fixture.CreateGuarantee();
            expiredActive.ExpiryDate = DateTime.Today.AddDays(-1);
            expiredActive.LifecycleStatus = GuaranteeLifecycleStatus.Active;

            database.SaveGuarantee(expiredActive, new List<string>());

            Assert.DoesNotContain(workflow.GetGuaranteesEligibleForExtension(), item => item.GuaranteeNo == expiredActive.GuaranteeNo);
            Assert.DoesNotContain(workflow.GetGuaranteesEligibleForReduction(), item => item.GuaranteeNo == expiredActive.GuaranteeNo);
            Assert.DoesNotContain(workflow.GetGuaranteesEligibleForLiquidation(), item => item.GuaranteeNo == expiredActive.GuaranteeNo);
            Assert.DoesNotContain(workflow.GetGuaranteesEligibleForVerification(), item => item.GuaranteeNo == expiredActive.GuaranteeNo);
            Assert.DoesNotContain(workflow.GetGuaranteesEligibleForReplacement(), item => item.GuaranteeNo == expiredActive.GuaranteeNo);
        }

        [Fact]
        public void GetGuaranteesEligibleForRelease_ReturnsActiveAndExpiredLifecycleGuarantees()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            Guarantee active = _fixture.CreateGuarantee();
            Guarantee expiredLifecycle = _fixture.CreateGuarantee();
            expiredLifecycle.LifecycleStatus = GuaranteeLifecycleStatus.Expired;
            expiredLifecycle.ExpiryDate = DateTime.Today.AddDays(-5);
            Guarantee released = _fixture.CreateGuarantee();
            released.LifecycleStatus = GuaranteeLifecycleStatus.Released;

            database.SaveGuarantee(active, new List<string>());
            database.SaveGuarantee(expiredLifecycle, new List<string>());
            database.SaveGuarantee(released, new List<string>());

            List<Guarantee> eligible = workflow.GetGuaranteesEligibleForRelease();

            Assert.Contains(eligible, item => item.GuaranteeNo == active.GuaranteeNo);
            Assert.Contains(eligible, item => item.GuaranteeNo == expiredLifecycle.GuaranteeNo);
            Assert.DoesNotContain(eligible, item => item.GuaranteeNo == released.GuaranteeNo);
        }

        [Fact]
        public void GetGuaranteesEligibleForReduction_ReturnsPositiveAmountGuarantees()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            Guarantee positiveAmount = _fixture.CreateGuarantee();

            database.SaveGuarantee(positiveAmount, new List<string>());

            List<Guarantee> eligible = workflow.GetGuaranteesEligibleForReduction();

            Assert.Contains(eligible, item => item.GuaranteeNo == positiveAmount.GuaranteeNo);
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
