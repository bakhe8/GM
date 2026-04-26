using GuaranteeManager.Services;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class NavigationGuardServiceTests
    {
        [Fact]
        public void CanNavigateAway_IsFalse_WhileScopeIsActive()
        {
            INavigationGuard guard = new NavigationGuardService();

            using IDisposable _ = guard.Block("توجد تغييرات غير محفوظة.");

            bool canNavigate = guard.CanNavigateAway(out string reason);

            Assert.False(canNavigate);
            Assert.Equal("توجد تغييرات غير محفوظة.", reason);
        }

        [Fact]
        public void CanNavigateAway_ReturnsTrue_AfterScopeIsDisposed()
        {
            INavigationGuard guard = new NavigationGuardService();
            IDisposable scope = guard.Block("توجد تغييرات غير محفوظة.");

            scope.Dispose();

            bool canNavigate = guard.CanNavigateAway(out string reason);

            Assert.True(canNavigate);
            Assert.Equal(string.Empty, reason);
        }
    }
}
