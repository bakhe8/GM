using GuaranteeManager.Models;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class GuaranteeVersionDisplayTests
    {
        [Theory]
        [InlineData(1, "الأول")]
        [InlineData(2, "الثاني")]
        [InlineData(3, "الثالث")]
        [InlineData(10, "العاشر")]
        [InlineData(11, "الحادي عشر")]
        [InlineData(21, "الحادي والعشرون")]
        public void GetLabel_ReturnsArabicOrdinalVersionLabel(int versionNumber, string expected)
        {
            Assert.Equal(expected, GuaranteeVersionDisplay.GetLabel(versionNumber));
        }
    }
}
