using System.Windows;
using System.Windows.Media;

namespace GuaranteeManager
{
    public static class UiTypography
    {
        public const string DefaultFontFamilyName = "IBM Plex Sans Arabic";
        public const string EmbeddedFontFamilyPath = "./Assets/Fonts/IBMPlexSansArabic/#IBM Plex Sans Arabic";
        public const string FallbackFontFamilyName = "Segoe UI Variable Text, Segoe UI, Tahoma";
        public const double Tiny = 9d;
        public const double Small = 10d;
        public const double Secondary = 11d;
        public const double Body = 12d;
        public const double CardTitle = 13d;
        public const double SectionTitle = 16d;
        public const double Title = 18d;
        public const double Kpi = 32d;
        public const double HeroNumber = 34d;

        public static FontFamily DefaultFontFamily { get; } = new(
            new Uri("pack://application:,,,/"),
            EmbeddedFontFamilyPath);

        public static FontWeight RegularWeight => FontWeights.Normal;
        public static FontWeight EmphasisWeight => FontWeights.SemiBold;
        public static FontWeight StrongWeight => FontWeights.Bold;
    }
}
