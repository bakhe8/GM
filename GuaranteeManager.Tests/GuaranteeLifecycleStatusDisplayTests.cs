using GuaranteeManager.Models;
using GuaranteeManager.Services;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class GuaranteeLifecycleStatusDisplayTests
    {
        [Fact]
        public void ClosedStatus_IsMarkedAsLegacyInDisplayLabel()
        {
            Assert.Equal("مغلق (قديم)", GuaranteeLifecycleStatusDisplay.GetLabel(GuaranteeLifecycleStatus.Closed));
            Assert.True(GuaranteeLifecycleStatusDisplay.IsLegacyOnly(GuaranteeLifecycleStatus.Closed));
        }

        [Fact]
        public void ParseLifecycleStatus_Closed_PreservesLegacyStatus()
        {
            GuaranteeLifecycleStatus status = GuaranteeDataAccess.ParseLifecycleStatus("Closed");

            Assert.Equal(GuaranteeLifecycleStatus.Closed, status);
            Assert.Equal("مغلق (قديم)", GuaranteeLifecycleStatusDisplay.GetLabel(status));
        }
    }
}
