using System;
using GuaranteeManager.Models;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class GuaranteeExpiryFollowUpTests
    {
        [Fact]
        public void NeedsExpiryFollowUp_IsTrue_ForExpiredGuaranteeWithActiveLifecycle()
        {
            Guarantee guarantee = new()
            {
                ExpiryDate = DateTime.Today.AddDays(-1),
                LifecycleStatus = GuaranteeLifecycleStatus.Active
            };

            Assert.True(guarantee.NeedsExpiryFollowUp);
        }

        [Fact]
        public void NeedsExpiryFollowUp_IsTrue_ForExpiredGuaranteeWithExpiredLifecycle()
        {
            Guarantee guarantee = new()
            {
                ExpiryDate = DateTime.Today.AddDays(-1),
                LifecycleStatus = GuaranteeLifecycleStatus.Expired
            };

            Assert.True(guarantee.NeedsExpiryFollowUp);
        }

        [Fact]
        public void NeedsExpiryFollowUp_IsFalse_ForExpiredGuaranteeWithReleasedLifecycle()
        {
            Guarantee guarantee = new()
            {
                ExpiryDate = DateTime.Today.AddDays(-1),
                LifecycleStatus = GuaranteeLifecycleStatus.Released
            };

            Assert.False(guarantee.NeedsExpiryFollowUp);
        }
    }
}
