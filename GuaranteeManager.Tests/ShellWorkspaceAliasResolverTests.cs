using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class ShellWorkspaceAliasResolverTests
    {
        [Fact]
        public void Resolve_ExactArabicAlias_NavigatesWithoutSearchText()
        {
            ShellWorkspaceSearchPlan plan = ShellWorkspaceAliasResolver.Resolve("التنبيهات", ShellWorkspaceKeys.Dashboard);

            Assert.Equal(ShellWorkspaceKeys.Dashboard, plan.TargetWorkspaceKey);
            Assert.False(plan.HasSearchText);
            Assert.Equal(DashboardScopeFilters.ExpiryFollowUps, plan.InitialScopeFilter);
            Assert.True(plan.MatchedAlias);
        }

        [Fact]
        public void Resolve_FollowUpAliasWithExpiredFilter_TargetsTodayExpiredFollowUps()
        {
            ShellWorkspaceSearchPlan plan = ShellWorkspaceAliasResolver.Resolve("التنبيهات: منتهي", ShellWorkspaceKeys.Guarantees);

            Assert.Equal(ShellWorkspaceKeys.Dashboard, plan.TargetWorkspaceKey);
            Assert.False(plan.HasSearchText);
            Assert.Equal(DashboardScopeFilters.LegacyExpiredFollowUp, plan.InitialScopeFilter);
            Assert.True(plan.MatchedAlias);
        }

        [Fact]
        public void Resolve_AliasWithColon_ExtractsWorkspaceAndQuery()
        {
            ShellWorkspaceSearchPlan plan = ShellWorkspaceAliasResolver.Resolve("الطلبات: تمديد", ShellWorkspaceKeys.Dashboard);

            Assert.Equal(ShellWorkspaceKeys.Requests, plan.TargetWorkspaceKey);
            Assert.Equal("تمديد", plan.SearchText);
            Assert.True(plan.MatchedAlias);
        }

        [Fact]
        public void Resolve_UnaliasedSearch_UsesCurrentWorkspaceWhenNotDashboard()
        {
            ShellWorkspaceSearchPlan plan = ShellWorkspaceAliasResolver.Resolve("مورد", ShellWorkspaceKeys.Requests);

            Assert.Equal(ShellWorkspaceKeys.Requests, plan.TargetWorkspaceKey);
            Assert.Equal("مورد", plan.SearchText);
            Assert.False(plan.MatchedAlias);
        }

        [Fact]
        public void Resolve_UnaliasedSearch_FromDashboardFallsBackToGuarantees()
        {
            ShellWorkspaceSearchPlan plan = ShellWorkspaceAliasResolver.Resolve("BG-2026-0044", ShellWorkspaceKeys.Dashboard);

            Assert.Equal(ShellWorkspaceKeys.Guarantees, plan.TargetWorkspaceKey);
            Assert.Equal("BG-2026-0044", plan.SearchText);
            Assert.False(plan.MatchedAlias);
        }
    }
}
