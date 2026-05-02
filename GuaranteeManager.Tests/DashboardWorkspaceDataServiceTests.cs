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
                "\u20C1 1,000.00",
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
            Assert.Equal("\u20C1 1,000.00", state.AmountHeadline);
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
                "\u20C1 2,000.00",
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
                "\u20C1 1,000.00",
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
            Assert.Contains(
                result.Metrics.Cards,
                card => card.Label == "قريبة الانتهاء"
                    && card.ScopeFilter == DashboardScopeFilters.ExpiryFollowUps
                    && card.ExpiryFilter == DashboardExpiryFollowUpFilters.ExpiringSoon);
            Assert.Contains(
                result.Metrics.Cards,
                card => card.Label == "منتهيه تحتاج اغلاق"
                    && card.ScopeFilter == DashboardScopeFilters.ExpiryFollowUps
                    && card.ExpiryFilter == DashboardExpiryFollowUpFilters.Expired);
        }

        [Fact]
        public void BuildFilteredItems_LabelsExpiredFollowUpMetricAsNeedsClosure()
        {
            var service = new DashboardWorkspaceDataService();
            var expiredOpen = CreateGuarantee(
                "BG-OPEN",
                DateTime.Today.AddDays(-3),
                GuaranteeLifecycleStatus.Active,
                2_000m);
            var expiredReleased = CreateGuarantee(
                "BG-RELEASED",
                DateTime.Today.AddDays(-5),
                GuaranteeLifecycleStatus.Released,
                1_000m);
            var expiringSoon = CreateGuarantee(
                "BG-SOON",
                DateTime.Today.AddDays(5),
                GuaranteeLifecycleStatus.Active,
                3_000m);
            var guarantees = new[] { expiredOpen, expiredReleased, expiringSoon };
            List<DashboardWorkItem> items = service.BuildItems(
                guarantees,
                Array.Empty<WorkflowRequestListItem>());

            DashboardWorkspaceFilterResult result = service.BuildFilteredItems(
                items,
                string.Empty,
                DashboardScopeFilters.AllWork,
                false,
                string.Empty,
                guarantees,
                Array.Empty<WorkflowRequestListItem>());

            Assert.DoesNotContain(items, item => item.Reference == expiredReleased.GuaranteeNo);
            DashboardMetricCard followUpCard = Assert.Single(result.Metrics.Cards, card => card.Label == "منتهيه تحتاج اغلاق");
            Assert.Equal("1", followUpCard.Value);
            Assert.DoesNotContain(result.Metrics.Cards, card => card.Label == "ضمانات منتهية");
        }

        [Fact]
        public void BuildItems_ExpiringSoonScope_OnlyIncludesGuaranteesWithPendingRequests()
        {
            var service = new DashboardWorkspaceDataService();
            var expiringWithRequest = CreateGuarantee(
                "BG-SOON-WITH-REQUEST",
                DateTime.Today.AddDays(5),
                GuaranteeLifecycleStatus.Active,
                3_000m);
            var expiringWithoutRequest = CreateGuarantee(
                "BG-SOON-WITHOUT-REQUEST",
                DateTime.Today.AddDays(8),
                GuaranteeLifecycleStatus.Active,
                4_000m);
            var pendingRequest = CreatePendingRequest(expiringWithRequest, 501);
            var guarantees = new[] { expiringWithRequest, expiringWithoutRequest };

            List<DashboardWorkItem> items = service.BuildItems(
                guarantees,
                new[] { pendingRequest });

            List<DashboardWorkItem> expiringItems = items
                .Where(item => item.Scope == DashboardScope.ExpiringSoon)
                .ToList();
            DashboardWorkItem item = Assert.Single(expiringItems);
            Assert.Equal(expiringWithRequest.GuaranteeNo, item.Reference);
            Assert.Equal("طلب تمديد", item.RequiredLabel);
            Assert.DoesNotContain(items, item => item.Reference == expiringWithoutRequest.GuaranteeNo);

            DashboardWorkspaceFilterResult result = service.BuildFilteredItems(
                items,
                string.Empty,
                DashboardScopeFilters.AllWork,
                false,
                string.Empty,
                guarantees,
                new[] { pendingRequest });

            DashboardMetricCard expiringCard = Assert.Single(result.Metrics.Cards, card => card.Label == "قريبة الانتهاء");
            Assert.Equal("1", expiringCard.Value);
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
                "\u20C1 1,000.00",
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
                "\u20C1 2,000.00",
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
                "\u20C1 2,000.00",
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
                GuaranteeFocusArea.Actions,
                category,
                priority,
                rank,
                "مورد اختبار",
                "مراجعة تمديد",
                "مورد اختبار",
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

        private static Guarantee CreateGuarantee(
            string guaranteeNo,
            DateTime expiryDate,
            GuaranteeLifecycleStatus lifecycleStatus,
            decimal amount)
        {
            int id = guaranteeNo switch
            {
                "BG-OPEN" => 101,
                "BG-RELEASED" => 102,
                "BG-SOON" => 103,
                "BG-SOON-WITH-REQUEST" => 104,
                "BG-SOON-WITHOUT-REQUEST" => 105,
                _ => 199
            };

            return new Guarantee
            {
                Id = id,
                RootId = id,
                IsCurrent = true,
                VersionNumber = 1,
                GuaranteeNo = guaranteeNo,
                Supplier = "مورد اختبار",
                Bank = "بنك اختبار",
                Amount = amount,
                ExpiryDate = expiryDate.Date,
                GuaranteeType = "ابتدائي",
                Beneficiary = "مستفيد اختبار",
                LifecycleStatus = lifecycleStatus
            };
        }

        private static WorkflowRequestListItem CreatePendingRequest(Guarantee guarantee, int requestId)
        {
            return new WorkflowRequestListItem
            {
                Request = new WorkflowRequest
                {
                    Id = requestId,
                    RootGuaranteeId = guarantee.RootId ?? guarantee.Id,
                    BaseVersionId = guarantee.Id,
                    SequenceNumber = 1,
                    Type = RequestType.Extension,
                    Status = RequestStatus.Pending,
                    RequestDate = DateTime.Today.AddDays(-2)
                },
                CurrentGuaranteeId = guarantee.Id,
                RootGuaranteeId = guarantee.RootId ?? guarantee.Id,
                GuaranteeNo = guarantee.GuaranteeNo,
                Supplier = guarantee.Supplier,
                Bank = guarantee.Bank,
                CurrentAmount = guarantee.Amount,
                CurrentExpiryDate = guarantee.ExpiryDate,
                CurrentVersionNumber = guarantee.VersionNumber,
                BaseVersionNumber = guarantee.VersionNumber,
                LifecycleStatus = guarantee.LifecycleStatus
            };
        }
    }
}
