using System;
using System.Collections.Generic;
using System.Globalization;

namespace GuaranteeManager.Utils
{
    public static class ArabicAmountFormatter
    {
        public const string SaudiRiyalSymbol = "\u20C1";

        public static string FormatNumber(decimal amount, int decimals = 2)
        {
            EnsureValidSaudiRiyalAmount(amount);
            string format = decimals <= 0 ? "N0" : $"N{decimals.ToString(CultureInfo.InvariantCulture)}";
            return amount.ToString(format, CultureInfo.InvariantCulture);
        }

        public static string FormatSaudiRiyals(decimal amount, int decimals = 2)
        {
            return $"{SaudiRiyalSymbol} {FormatNumber(amount, decimals)}";
        }

        public static bool TryParsePositiveSaudiRiyalAmount(string? value, out decimal amount)
        {
            amount = 0m;
            string normalized = NormalizeAmountInput(value);
            if (!HasValidHalalaInputPrecision(normalized)
                || !decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
                || parsed <= 0
                || !HasValidHalalaPrecision(parsed))
            {
                return false;
            }

            amount = NormalizeSaudiRiyalAmount(parsed);
            return true;
        }

        public static decimal NormalizeSaudiRiyalAmount(decimal amount)
        {
            return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        }

        public static bool HasValidHalalaPrecision(decimal amount)
        {
            return CountSignificantDecimalPlaces(amount) <= 2;
        }

        public static void EnsureValidSaudiRiyalAmount(decimal amount, string fieldName = "المبلغ")
        {
            if (amount < 0)
            {
                throw new InvalidOperationException($"{fieldName} لا يمكن أن يكون بالسالب.");
            }

            if (!HasValidHalalaPrecision(amount))
            {
                throw new InvalidOperationException($"{fieldName} لا يمكن أن يحتوي على أكثر من خانتين للهلل.");
            }
        }

        public static void EnsurePositiveSaudiRiyalAmount(decimal amount, string fieldName = "المبلغ")
        {
            EnsureValidSaudiRiyalAmount(amount, fieldName);
            if (amount <= 0)
            {
                throw new InvalidOperationException($"{fieldName} يجب أن يكون أكبر من صفر.");
            }
        }

        public static string FormatSaudiRiyalsInWords(decimal amount)
        {
            EnsureValidSaudiRiyalAmount(amount);
            decimal rounded = NormalizeSaudiRiyalAmount(amount);
            EnsureWholeRiyalAmountWithinWordsRange(rounded);
            long riyals = (long)Math.Floor(rounded);
            int halalas = (int)((rounded - riyals) * 100m);

            string riyalText = $"{NumberToArabicWords(riyals)} ريال سعودي";
            if (halalas == 0)
            {
                return riyalText;
            }

            return $"{riyalText} و{NumberToArabicWords(halalas)} هللة";
        }

        public static string FormatSaudiRiyalsForLetter(decimal amount)
        {
            return $"{FormatNumber(amount, 2)}{Environment.NewLine}{FormatSaudiRiyalsInWords(amount)}";
        }

        public static string NumberToArabicWords(decimal amount)
        {
            EnsureValidSaudiRiyalAmount(amount, "الرقم");
            decimal rounded = Math.Round(amount, MidpointRounding.AwayFromZero);
            EnsureWholeRiyalAmountWithinWordsRange(rounded);
            long number = (long)rounded;
            return NumberToArabicWords(number);
        }

        private static void EnsureWholeRiyalAmountWithinWordsRange(decimal amount)
        {
            if (Math.Floor(amount) > long.MaxValue)
            {
                throw new InvalidOperationException("المبلغ أكبر من الحد المدعوم للتحويل إلى كلمات عربية.");
            }
        }

        private static string NumberToArabicWords(long number)
        {
            if (number == 0)
            {
                return "صفر";
            }

            var parts = new List<string>();
            if (number >= 1_000_000_000)
            {
                long group = number / 1_000_000_000;
                number %= 1_000_000_000;
                parts.Add(group == 1 ? "مليار" : group == 2 ? "ملياران" : ArabicGroupWord(group, "مليارات", "مليار"));
            }

            if (number >= 1_000_000)
            {
                long group = number / 1_000_000;
                number %= 1_000_000;
                parts.Add(group == 1 ? "مليون" : group == 2 ? "مليونان" : ArabicGroupWord(group, "ملايين", "مليون"));
            }

            if (number >= 1_000)
            {
                long group = number / 1_000;
                number %= 1_000;
                parts.Add(group == 1 ? "ألف" : group == 2 ? "ألفان" : ArabicGroupWord(group, "آلاف", "ألف"));
            }

            if (number >= 100)
            {
                parts.Add(ArabicHundreds((int)(number / 100)));
                number %= 100;
            }

            if (number > 0)
            {
                parts.Add(ArabicUnderHundred((int)number));
            }

            return string.Join(" و", parts);
        }

        private static string ArabicGroupWord(long count, string fewForm, string manyForm)
        {
            string countText = ArabicSmallCount(count);
            return count >= 3 && count <= 10 ? $"{countText} {fewForm}" : $"{countText} {manyForm}";
        }

        private static string ArabicSmallCount(long number)
        {
            if (number <= 19)
            {
                return ArabicUnderHundred((int)number);
            }

            if (number < 100)
            {
                return ArabicUnderHundred((int)(number % 10)) + " و" + ArabicTens((int)(number / 10));
            }

            return ArabicHundreds((int)(number / 100)) + (number % 100 > 0 ? " و" + ArabicSmallCount(number % 100) : string.Empty);
        }

        private static string ArabicUnderHundred(int number) => number switch
        {
            1 => "واحد",
            2 => "اثنان",
            3 => "ثلاثة",
            4 => "أربعة",
            5 => "خمسة",
            6 => "ستة",
            7 => "سبعة",
            8 => "ثمانية",
            9 => "تسعة",
            10 => "عشرة",
            11 => "أحد عشر",
            12 => "اثنا عشر",
            13 => "ثلاثة عشر",
            14 => "أربعة عشر",
            15 => "خمسة عشر",
            16 => "ستة عشر",
            17 => "سبعة عشر",
            18 => "ثمانية عشر",
            19 => "تسعة عشر",
            _ when number % 10 == 0 => ArabicTens(number / 10),
            _ => ArabicUnderHundred(number % 10) + " و" + ArabicTens(number / 10)
        };

        private static string ArabicTens(int tens) => tens switch
        {
            2 => "عشرون",
            3 => "ثلاثون",
            4 => "أربعون",
            5 => "خمسون",
            6 => "ستون",
            7 => "سبعون",
            8 => "ثمانون",
            9 => "تسعون",
            _ => string.Empty
        };

        private static string ArabicHundreds(int hundreds) => hundreds switch
        {
            1 => "مئة",
            2 => "مئتان",
            3 => "ثلاثمئة",
            4 => "أربعمئة",
            5 => "خمسمئة",
            6 => "ستمئة",
            7 => "سبعمئة",
            8 => "ثمانمئة",
            9 => "تسعمئة",
            _ => string.Empty
        };

        private static string NormalizeAmountInput(string? value)
        {
            return (value ?? string.Empty)
                .Replace(SaudiRiyalSymbol, string.Empty, StringComparison.Ordinal)
                .Replace("ريال", string.Empty, StringComparison.Ordinal)
                .Replace(",", string.Empty, StringComparison.Ordinal)
                .Trim();
        }

        private static bool HasValidHalalaInputPrecision(string value)
        {
            int separatorIndex = value.IndexOf('.', StringComparison.Ordinal);
            return separatorIndex < 0 || value.Length - separatorIndex - 1 <= 2;
        }

        private static int CountSignificantDecimalPlaces(decimal amount)
        {
            string text = Math.Abs(amount).ToString("0.#############################", CultureInfo.InvariantCulture);
            int separatorIndex = text.IndexOf('.', StringComparison.Ordinal);
            return separatorIndex < 0 ? 0 : text.Length - separatorIndex - 1;
        }
    }
}
