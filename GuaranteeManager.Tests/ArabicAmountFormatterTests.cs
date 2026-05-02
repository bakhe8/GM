using GuaranteeManager.Utils;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class ArabicAmountFormatterTests
    {
        [Theory]
        [InlineData(0, "صفر ريال سعودي")]
        [InlineData(1_100_000, "مليون ومئة ألف ريال سعودي")]
        [InlineData(2_500_030, "مليونان وخمسمئة ألف وثلاثون ريال سعودي")]
        public void FormatSaudiRiyalsInWords_FormatsWholeRiyals(decimal amount, string expected)
        {
            Assert.Equal(expected, ArabicAmountFormatter.FormatSaudiRiyalsInWords(amount));
        }

        [Fact]
        public void FormatSaudiRiyalsInWords_IncludesHalalasWhenPresent()
        {
            Assert.Equal(
                "مئة ريال سعودي وخمسون هللة",
                ArabicAmountFormatter.FormatSaudiRiyalsInWords(100.50m));
        }

        [Fact]
        public void FormatSaudiRiyalsForLetter_CombinesNumericAndTextLines()
        {
            string text = ArabicAmountFormatter.FormatSaudiRiyalsForLetter(1200m);

            Assert.Contains("\u20C1 1,200.00", text);
            Assert.Contains("ألف ومئتان ريال سعودي", text);
            Assert.Contains(System.Environment.NewLine, text);
        }
    }
}
