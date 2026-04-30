using System;
using System.Windows.Media;
using GuaranteeManager.Models;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class DashboardWorkspaceDataServiceTests
    {
        [Fact]
        public void BuildDetailState_ForExpiryFollowUp_KeepsDurationAndExpiryDate()
        {
            var service = new DashboardWorkspaceDataService();
            DateTime expiryDate = DateTime.Today.AddDays(5);
            var item = CreateFollowUpItem(
                DashboardScope.ExpiringSoon,
                "قريبة الانتهاء",
                "مرتفع",
                1,
                "BG-TEST-EXP",
                1000m,
                "1,000 ريال",
                expiryDate,
                expiryDate.ToString("yyyy/MM/dd"),
                "خلال 5 أيام");

            DashboardWorkspaceDetailState state = service.BuildDetailState(
                item,
                DashboardScopeFilters.ExpiryFollowUps,
                false,
                string.Empty,
                string.Empty);

            Assert.Equal(DashboardDetailProfile.FollowUp, state.DetailProfile);
            Assert.Equal("خلال 5 أيام", state.Due);
            Assert.Equal(expiryDate.ToString("yyyy/MM/dd"), state.Expiry);
        }

        [Fact]
        public void BuildFilteredItems_ForExpiredFollowUpMode_OnlyReturnsExpiredItems()
        {
            var service = new DashboardWorkspaceDataService();
            DashboardWorkItem expired = CreateFollowUpItem(
                DashboardScope.ExpiredFollowUp,
                "منتهية تحتاج متابعة",
                "حرج",
                0,
                "BG-TEST-OLD",
                2000m,
                "2,000 ريال",
                DateTime.Today.AddDays(-3),
                DateTime.Today.AddDays(-3).ToString("yyyy/MM/dd"),
                "متأخر 3 أيام");
            DashboardWorkItem expiring = CreateFollowUpItem(
                DashboardScope.ExpiringSoon,
                "قريبة الانتهاء",
                "متابعة",
                3,
                "BG-TEST-SOON",
                1000m,
                "1,000 ريال",
                DateTime.Today.AddDays(5),
                DateTime.Today.AddDays(5).ToString("yyyy/MM/dd"),
                "خلال 5 أيام");

            DashboardWorkspaceFilterResult result = service.BuildFilteredItems(
                new[] { expired, expiring },
                string.Empty,
                DashboardScopeFilters.ExpiryFollowUps,
                false,
                string.Empty,
                Array.Empty<Guarantee>(),
                Array.Empty<WorkflowRequestListItem>(),
                DashboardExpiryFollowUpFilters.Expired);

            DashboardWorkItem item = Assert.Single(result.Items);
            Assert.Equal(DashboardScope.ExpiredFollowUp, item.Scope);
            Assert.Equal("0", result.Metrics.First.Value);
            Assert.Equal("1", result.Metrics.Second.Value);
        }

        [Fact]
        public void BuildGuidanceState_UsesHighestPriorityItemAsSmartGuideTarget()
        {
            var service = new DashboardWorkspaceDataService();
            DashboardWorkItem normal = CreateFollowUpItem(
                DashboardScope.ExpiringSoon,
                "قريبة الانتهاء",
                "متابعة",
                3,
                "BG-TEST-SOON",
                1000m,
                "1,000 ريال",
                DateTime.Today.AddDays(5),
                DateTime.Today.AddDays(5).ToString("yyyy/MM/dd"),
                "خلال 5 أيام");
            DashboardWorkItem critical = CreateFollowUpItem(
                DashboardScope.ExpiredFollowUp,
                "منتهية تحتاج متابعة",
                "حرج",
                0,
                "BG-TEST-OLD",
                2000m,
                "2,000 ريال",
                DateTime.Today.AddDays(-3),
                DateTime.Today.AddDays(-3).ToString("yyyy/MM/dd"),
                "متأخر 3 أيام");

            DashboardGuidanceState state = service.BuildGuidanceState(
                new[] { normal, critical },
                Array.Empty<Guarantee>(),
                Array.Empty<WorkflowRequestListItem>());

            Assert.Equal(DashboardGuidanceActionKind.OpenTopPriority, state.Guide.ActionKind);
            Assert.Same(critical, state.Guide.TargetItem);
            Assert.Contains("BG-TEST-OLD", state.Guide.PrimaryText, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildGuidanceState_RecommendsExpiredFollowUpWhenNoPendingRequests()
        {
            var service = new DashboardWorkspaceDataService();
            DashboardWorkItem expired = CreateFollowUpItem(
                DashboardScope.ExpiredFollowUp,
                "منتهية تحتاج متابعة",
                "حرج",
                0,
                "BG-TEST-OLD",
                2000m,
                "2,000 ريال",
                DateTime.Today.AddDays(-3),
                DateTime.Today.AddDays(-3).ToString("yyyy/MM/dd"),
                "متأخر 3 أيام");

            DashboardGuidanceState state = service.BuildGuidanceState(
                new[] { expired },
                Array.Empty<Guarantee>(),
                Array.Empty<WorkflowRequestListItem>());

            Assert.Equal(DashboardGuidanceActionKind.FilterExpiredFollowUps, state.Recommendation.ActionKind);
            Assert.Contains("1 ضمان منتهي", state.Recommendation.PrimaryText, StringComparison.Ordinal);
        }

        private static DashboardWorkItem CreateFollowUpItem(
            DashboardScope scope,
            string category,
            string priority,
            int rank,
            string reference,
            decimal amount,
            string amountDisplay,
            DateTime expiryDate,
            string dueLabel,
            string dueDetail)
        {
            return new DashboardWorkItem(
                scope,
                42,
                null,
                GuaranteeFileFocusArea.Actions,
                category,
                priority,
                rank,
                "مستفيد اختبار",
                "بنك اختبار",
                new DrawingImage(),
                reference,
                amount,
                amountDisplay,
                "قيمة الضمان الحالية",
                expiryDate,
                dueLabel,
                dueDetail,
                "ابتدائي",
                "راجع التمديد",
                "افتح الضمان في المحفظة وراجع قرار التمديد",
                "ظهر اليوم لأنه داخل نافذة الانتهاء القريبة.",
                Brushes.DarkOrange,
                Brushes.White,
                Brushes.Orange,
                Brushes.DarkOrange);
        }
    }
}
