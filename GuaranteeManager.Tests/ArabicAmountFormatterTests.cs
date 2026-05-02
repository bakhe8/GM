using GuaranteeManager.Utils;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class ArabicAmountFormatterTests
    {
        [Theory]
        [InlineData(0, "صفر ريال سعودي \u20C1")]
        [InlineData(1_100_000, "مليون ومئة ألف ريال سعودي \u20C1")]
        [InlineData(2_500_030, "مليونان وخمسمئة ألف وثلاثون ريال سعودي \u20C1")]
        public void FormatSaudiRiyalsInWords_FormatsWholeRiyals(decimal amount, string expected)
        {
            Assert.Equal(expected, ArabicAmountFormatter.FormatSaudiRiyalsInWords(amount));
        }

        [Fact]
        public void FormatSaudiRiyals_PutsSymbolLeftOfNumberAndNegativeSign()
        {
            Assert.Equal("\u20C1 -1,250", ArabicAmountFormatter.FormatSaudiRiyals(-1250m));
        }

        [Fact]
        public void FormatSaudiRiyalsInWords_IncludesHalalasWhenPresent()
        {
            Assert.Equal(
                "مئة ريال سعودي وخمسون هللة \u20C1",
                ArabicAmountFormatter.FormatSaudiRiyalsInWords(100.50m));
        }

        [Fact]
        public void FormatSaudiRiyalsForLetter_CombinesNumericAndTextLines()
        {
            string text = ArabicAmountFormatter.FormatSaudiRiyalsForLetter(1200m);

            Assert.Contains("\u20C1 1,200.00", text);
            Assert.Contains("ألف ومئتان ريال سعودي \u20C1", text);
            Assert.Contains(System.Environment.NewLine, text);
        }
    }
}
