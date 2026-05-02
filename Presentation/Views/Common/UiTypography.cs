using System.Windows.Media;

namespace GuaranteeManager
{
    public static class UiTypography
    {
        public const string DefaultFontFamilyName = "Segoe UI Variable Text, Segoe UI, Tahoma";

        public static FontFamily DefaultFontFamily { get; } = new(DefaultFontFamilyName);
    }
}
