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
        public void FormatSaudiRiyals_RejectsNegativeAmounts()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ArabicAmountFormatter.FormatSaudiRiyals(-1250m));

            Assert.Contains("بالسالب", exception.Message);
        }

        [Fact]
        public void FormatSaudiRiyals_AlwaysShowsHalalas()
        {
            Assert.Equal("\u20C1 1,250.00", ArabicAmountFormatter.FormatSaudiRiyals(1250m));
            Assert.Equal("\u20C1 1,250.50", ArabicAmountFormatter.FormatSaudiRiyals(1250.5m));
        }

        [Theory]
        [InlineData("1,250.50", true, 1250.50)]
        [InlineData("1,250.500", false, 0)]
        [InlineData("1,250.555", false, 0)]
        [InlineData("-1", false, 0)]
        [InlineData("0", false, 0)]
        public void TryParsePositiveSaudiRiyalAmount_RequiresPositiveAmountWithTwoHalalaDigits(
            string value,
            bool expectedResult,
            decimal expectedAmount)
        {
            bool result = ArabicAmountFormatter.TryParsePositiveSaudiRiyalAmount(value, out decimal amount);

            Assert.Equal(expectedResult, result);
            if (expectedResult)
            {
                Assert.Equal(expectedAmount, amount);
            }
        }

        [Fact]
        public void FormatSaudiRiyalsInWords_RejectsNegativeAmounts()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ArabicAmountFormatter.FormatSaudiRiyalsInWords(-100m));

            Assert.Contains("بالسالب", exception.Message);
        }

        [Fact]
        public void FormatSaudiRiyalsInWords_RejectsAmountsBeyondWholeRiyalWordRange()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => ArabicAmountFormatter.FormatSaudiRiyalsInWords(decimal.MaxValue));

            Assert.Contains("الحد المدعوم", exception.Message);
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

            Assert.Contains("1,200.00", text);
            Assert.DoesNotContain("\u20C1", text);
            Assert.Contains("ألف ومئتان ريال سعودي", text);
            Assert.Contains(System.Environment.NewLine, text);
        }
    }
}
