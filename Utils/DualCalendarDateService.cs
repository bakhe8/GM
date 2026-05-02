using System;
using System.Globalization;
using System.Linq;
using System.Text;
using GuaranteeManager.Models;

namespace GuaranteeManager.Utils
{
    internal static class DualCalendarDateService
    {
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        private static readonly char[] DateSeparators =
        {
            '/', '-', '.', '\\'
        };

        private static readonly string[] GregorianFormats =
        {
            "yyyy/M/d",
            "yyyy/MM/dd",
            "yyyy-M-d",
            "yyyy-MM-dd",
            "yyyy.M.d",
            "yyyy.MM.dd",
            "d/M/yyyy",
            "dd/MM/yyyy",
            "d-M-yyyy",
            "dd-MM-yyyy",
            "d.M.yyyy",
            "dd.MM.yyyy"
        };

        public const string InputExamples = "2026/12/31 أو 1448/07/20";

        public static bool TryParseDate(string? input, out DateTime gregorianDate)
        {
            return TryParseDate(input, out gregorianDate, out _);
        }

        public static bool TryParseDate(string? input, out DateTime gregorianDate, out GuaranteeDateCalendar dateCalendar)
        {
            gregorianDate = default;
            dateCalendar = GuaranteeDateCalendar.Gregorian;
            string normalized = NormalizeInput(input);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (TryParseNumericDate(normalized, out gregorianDate, out dateCalendar))
            {
                return true;
            }

            if (DateTime.TryParseExact(
                    normalized,
                    GregorianFormats,
                    InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime exactGregorian))
            {
                gregorianDate = exactGregorian.Date;
                dateCalendar = GuaranteeDateCalendar.Gregorian;
                return IsSupportedGregorianBusinessDate(gregorianDate);
            }

            if (DateTime.TryParse(normalized, InvariantCulture, DateTimeStyles.None, out DateTime invariantParsed)
                && IsSupportedGregorianBusinessDate(invariantParsed))
            {
                gregorianDate = invariantParsed.Date;
                dateCalendar = GuaranteeDateCalendar.Gregorian;
                return true;
            }

            if (DateTime.TryParse(normalized, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime cultureParsed)
                && IsSupportedGregorianBusinessDate(cultureParsed))
            {
                gregorianDate = cultureParsed.Date;
                dateCalendar = GuaranteeDateCalendar.Gregorian;
                return true;
            }

            return false;
        }

        public static string FormatGregorianDate(DateTime date)
        {
            return date.Date.ToString("yyyy/MM/dd", InvariantCulture);
        }

        public static string FormatIsoDate(DateTime date)
        {
            return date.Date.ToString("yyyy-MM-dd", InvariantCulture);
        }

        public static string FormatHijriDate(DateTime gregorianDate)
        {
            try
            {
                var calendar = new UmAlQuraCalendar();
                DateTime date = gregorianDate.Date;
                return string.Format(
                    InvariantCulture,
                    "{0:0000}/{1:00}/{2:00} هـ",
                    calendar.GetYear(date),
                    calendar.GetMonth(date),
                    calendar.GetDayOfMonth(date));
            }
            catch (ArgumentOutOfRangeException)
            {
                return "---";
            }
        }

        public static string FormatDualDate(DateTime gregorianDate)
        {
            string hijriDate = FormatHijriDate(gregorianDate);
            return hijriDate == "---"
                ? $"{FormatGregorianDate(gregorianDate)} م"
                : $"{FormatGregorianDate(gregorianDate)} م | {hijriDate}";
        }

        public static string FormatDateTime(DateTime value)
        {
            return $"{FormatGregorianDate(value)} {value.ToString("HH:mm", InvariantCulture)}";
        }

        public static string FormatDate(DateTime gregorianDate, GuaranteeDateCalendar dateCalendar)
        {
            return dateCalendar == GuaranteeDateCalendar.Hijri
                ? FormatHijriDate(gregorianDate)
                : FormatGregorianDate(gregorianDate);
        }

        public static string FormatDateTime(DateTime value, GuaranteeDateCalendar dateCalendar)
        {
            return dateCalendar == GuaranteeDateCalendar.Hijri
                ? $"{FormatHijriDate(value)} {value.ToString("HH:mm", InvariantCulture)}"
                : FormatDateTime(value);
        }

        public static GuaranteeDateCalendar ParseDateCalendar(string? value)
        {
            return Enum.TryParse(value, ignoreCase: true, out GuaranteeDateCalendar parsed)
                ? parsed
                : GuaranteeDateCalendar.Gregorian;
        }

        private static bool TryParseNumericDate(string normalized, out DateTime gregorianDate, out GuaranteeDateCalendar dateCalendar)
        {
            gregorianDate = default;
            dateCalendar = GuaranteeDateCalendar.Gregorian;
            string[] parts = normalized
                .Split(DateSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 3 || !parts.All(part => int.TryParse(part, NumberStyles.None, InvariantCulture, out _)))
            {
                return false;
            }

            int[] values = parts
                .Select(part => int.Parse(part, NumberStyles.None, InvariantCulture))
                .ToArray();

            int year;
            int month;
            int day;
            if (values[0] >= 1000)
            {
                year = values[0];
                month = values[1];
                day = values[2];
            }
            else if (values[2] >= 1000)
            {
                day = values[0];
                month = values[1];
                year = values[2];
            }
            else
            {
                return false;
            }

            if (IsLikelyHijriYear(year))
            {
                dateCalendar = GuaranteeDateCalendar.Hijri;
                return TryCreateHijriDate(year, month, day, out gregorianDate);
            }

            if (IsLikelyGregorianYear(year))
            {
                dateCalendar = GuaranteeDateCalendar.Gregorian;
                return TryCreateGregorianDate(year, month, day, out gregorianDate);
            }

            return false;
        }

        private static bool TryCreateGregorianDate(int year, int month, int day, out DateTime date)
        {
            date = default;
            try
            {
                date = new DateTime(year, month, day).Date;
                return IsSupportedGregorianBusinessDate(date);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static bool TryCreateHijriDate(int year, int month, int day, out DateTime gregorianDate)
        {
            gregorianDate = default;
            try
            {
                var calendar = new UmAlQuraCalendar();
                gregorianDate = calendar.ToDateTime(year, month, day, 0, 0, 0, 0).Date;
                return IsSupportedGregorianBusinessDate(gregorianDate);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static bool IsLikelyHijriYear(int year)
        {
            return year is >= 1200 and <= 1700;
        }

        private static bool IsLikelyGregorianYear(int year)
        {
            return year is >= 1900 and <= 2200;
        }

        private static bool IsSupportedGregorianBusinessDate(DateTime date)
        {
            return date.Year is >= 1900 and <= 2200;
        }

        private static string NormalizeInput(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(input.Trim().Length);
            foreach (char character in input.Trim())
            {
                builder.Append(character switch
                {
                    >= '\u0660' and <= '\u0669' => (char)('0' + character - '\u0660'),
                    >= '\u06F0' and <= '\u06F9' => (char)('0' + character - '\u06F0'),
                    '\u2013' or '\u2014' or '\u2212' => '-',
                    _ => character
                });
            }

            return builder
                .Replace("هـ", string.Empty)
                .Replace("ه", string.Empty)
                .Replace("م", string.Empty)
                .ToString()
                .Trim();
        }
    }
}
