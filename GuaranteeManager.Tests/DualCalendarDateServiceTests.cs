using System;
using System.Globalization;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class DualCalendarDateServiceTests
    {
        [Fact]
        public void TryParseDate_AcceptsGregorianYearFirstDate()
        {
            bool parsed = DualCalendarDateService.TryParseDate("2026/03/04", out DateTime actual);

            Assert.True(parsed);
            Assert.Equal(new DateTime(2026, 3, 4), actual);
        }

        [Fact]
        public void TryParseDate_AcceptsHijriYearFirstDate()
        {
            var calendar = new UmAlQuraCalendar();
            DateTime expected = calendar.ToDateTime(1447, 9, 15, 0, 0, 0, 0).Date;

            bool parsed = DualCalendarDateService.TryParseDate("1447/09/15", out DateTime actual, out GuaranteeDateCalendar dateCalendar);

            Assert.True(parsed);
            Assert.Equal(expected, actual);
            Assert.Equal(GuaranteeDateCalendar.Hijri, dateCalendar);
        }

        [Fact]
        public void TryParseDate_ReturnsGregorianCalendarForGregorianInput()
        {
            bool parsed = DualCalendarDateService.TryParseDate("2026/03/04", out DateTime actual, out GuaranteeDateCalendar dateCalendar);

            Assert.True(parsed);
            Assert.Equal(new DateTime(2026, 3, 4), actual);
            Assert.Equal(GuaranteeDateCalendar.Gregorian, dateCalendar);
        }

        [Fact]
        public void FormatDate_UsesStoredCalendarPreference()
        {
            DateTime date = new DateTime(2026, 3, 4);

            string gregorian = DualCalendarDateService.FormatDate(date, GuaranteeDateCalendar.Gregorian);
            string hijri = DualCalendarDateService.FormatDate(date, GuaranteeDateCalendar.Hijri);

            Assert.Equal("2026/03/04", gregorian);
            Assert.Contains("هـ", hijri);
            Assert.DoesNotContain("2026/03/04", hijri);
        }

        [Fact]
        public void TryParseDate_AcceptsHijriDayFirstDateWithSuffix()
        {
            var calendar = new UmAlQuraCalendar();
            DateTime expected = calendar.ToDateTime(1447, 9, 15, 0, 0, 0, 0).Date;

            bool parsed = DualCalendarDateService.TryParseDate("15/09/1447 هـ", out DateTime actual);

            Assert.True(parsed);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TryParseDate_AcceptsArabicIndicHijriDigits()
        {
            var calendar = new UmAlQuraCalendar();
            DateTime expected = calendar.ToDateTime(1447, 9, 15, 0, 0, 0, 0).Date;

            bool parsed = DualCalendarDateService.TryParseDate("١٤٤٧/٠٩/١٥", out DateTime actual);

            Assert.True(parsed);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TryParseDate_RejectsInvalidHijriDate()
        {
            bool parsed = DualCalendarDateService.TryParseDate("1447/13/01", out _);

            Assert.False(parsed);
        }

        [Fact]
        public void FormatDualDate_IncludesGregorianAndHijriLabels()
        {
            string formatted = DualCalendarDateService.FormatDualDate(new DateTime(2026, 3, 4));

            Assert.Contains("2026/03/04 م", formatted);
            Assert.Contains("هـ", formatted);
        }

        [Fact]
        public void PersistedDateTime_ParseTreatsHijriDateOnlyAsUmAlQuraDate()
        {
            var calendar = new UmAlQuraCalendar();
            DateTime expected = calendar.ToDateTime(1447, 9, 15, 0, 0, 0, 0).Date;

            DateTime actual = PersistedDateTime.Parse("1447/09/15");

            Assert.Equal(expected, actual);
        }
    }
}
