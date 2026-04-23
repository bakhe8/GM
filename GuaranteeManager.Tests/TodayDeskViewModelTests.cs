using System;
using System.Collections.Generic;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.ViewModels;
using Xunit;

namespace GuaranteeManager.Tests
{
    [Collection("Database")]
    public sealed class TodayDeskViewModelTests
    {
        private readonly TestEnvironmentFixture _fixture;

        public TodayDeskViewModelTests(TestEnvironmentFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void Refresh_CountsExpiredFollowUpGuaranteesUsingUnifiedRule()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);

            Guarantee expiredActive = _fixture.CreateGuarantee();
            expiredActive.ExpiryDate = DateTime.Today.AddDays(-4);
            expiredActive.LifecycleStatus = GuaranteeLifecycleStatus.Active;

            Guarantee expiredLifecycle = _fixture.CreateGuarantee();
            expiredLifecycle.ExpiryDate = DateTime.Today.AddDays(-2);
            expiredLifecycle.LifecycleStatus = GuaranteeLifecycleStatus.Expired;

            Guarantee releasedExpired = _fixture.CreateGuarantee();
            releasedExpired.ExpiryDate = DateTime.Today.AddDays(-1);
            releasedExpired.LifecycleStatus = GuaranteeLifecycleStatus.Released;

            database.SaveGuarantee(expiredActive, new List<string>());
            database.SaveGuarantee(expiredLifecycle, new List<string>());
            database.SaveGuarantee(releasedExpired, new List<string>());

            TodayDeskViewModel viewModel = new(database, workflow);

            viewModel.Refresh();

            Assert.Equal("2", viewModel.ExpiredFollowUp);
        }
    }
}
