using System;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class ExtensionRequestFlowSupportTests
    {
        [Fact]
        public void GetSuggestedRequestedExpiryDate_UsesOneYearAheadOfCurrentExpiry()
        {
            Guarantee guarantee = new()
            {
                ExpiryDate = new DateTime(2026, 5, 15),
                LifecycleStatus = GuaranteeLifecycleStatus.Active
            };

            DateTime suggested = ExtensionRequestFlowSupport.GetSuggestedRequestedExpiryDate(guarantee);

            Assert.Equal(new DateTime(2027, 5, 15), suggested);
        }

        [Fact]
        public void TryValidate_RejectsRequestedDateThatDoesNotAdvanceExpiry()
        {
            Guarantee guarantee = new()
            {
                ExpiryDate = new DateTime(2026, 5, 15),
                LifecycleStatus = GuaranteeLifecycleStatus.Active
            };

            bool isValid = ExtensionRequestFlowSupport.TryValidate(
                guarantee,
                new DateTime(2026, 5, 15),
                "tester",
                out string reason);

            Assert.False(isValid);
            Assert.Equal("اجعل تاريخ التمديد المطلوب بعد تاريخ الانتهاء الحالي.", reason);
        }

        [Fact]
        public void TryValidate_RejectsMissingCreatedBy()
        {
            Guarantee guarantee = new()
            {
                ExpiryDate = new DateTime(2026, 5, 15),
                LifecycleStatus = GuaranteeLifecycleStatus.Active
            };

            bool isValid = ExtensionRequestFlowSupport.TryValidate(
                guarantee,
                new DateTime(2027, 5, 15),
                string.Empty,
                out string reason);

            Assert.False(isValid);
            Assert.Equal("أدخل اسم منشئ الطلب أولًا.", reason);
        }
    }
}
