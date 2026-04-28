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
            var item = new DashboardWorkItem(
                DashboardScope.ExpiringSoon,
                DashboardTarget.Today,
                42,
                null,
                GuaranteeFileFocusArea.Actions,
                "قريبة الانتهاء",
                "مرتفع",
                1,
                "مستفيد اختبار",
                "بنك اختبار",
                new DrawingImage(),
                "BG-TEST-EXP",
                1000m,
                "1,000 ريال",
                "قيمة الضمان الحالية",
                expiryDate,
                expiryDate.ToString("yyyy/MM/dd"),
                "خلال 5 أيام",
                "ابتدائي",
                "راجع التمديد",
                "متابعات الانتهاء",
                "ركّز المتابعة",
                "افتح الملف وراجع قرار التمديد",
                "ظهر اليوم لأنه داخل نافذة الانتهاء القريبة.",
                Brushes.DarkOrange,
                Brushes.White,
                Brushes.Orange,
                Brushes.DarkOrange);

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
    }
}
