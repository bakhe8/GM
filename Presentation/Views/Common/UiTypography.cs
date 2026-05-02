using System.Windows.Media;

namespace GuaranteeManager
{
    public static class UiTypography
    {
        public const string DefaultFontFamilyName = "Segoe UI Variable Text, Segoe UI, Tahoma";
        public const double Tiny = 9d;
        public const double Small = 10d;
        public const double Secondary = 11d;
        public const double Body = 12d;
        public const double CardTitle = 13d;
        public const double SectionTitle = 16d;
        public const double Title = 18d;
        public const double Kpi = 32d;
        public const double HeroNumber = 34d;

        public static FontFamily DefaultFontFamily { get; } = new(DefaultFontFamilyName);
    }
}
