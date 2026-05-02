using System.Text.RegularExpressions;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class TypographyPolicyTests
    {
        [Fact]
        public void PresentationDoesNotUseUnsupportedSemiBoldWeight()
        {
            string presentation = ReadPresentationText();

            Assert.DoesNotContain("FontWeights.SemiBold", presentation, StringComparison.Ordinal);
            Assert.DoesNotContain("FontWeight=\"SemiBold\"", presentation, StringComparison.Ordinal);
            Assert.DoesNotContain("FontWeight\" Value=\"SemiBold\"", presentation, StringComparison.Ordinal);
        }

        [Fact]
        public void FontFamiliesAreCentralized()
        {
            string root = FindRepositoryRoot();
            string[] files = Directory
                .EnumerateFiles(Path.Combine(root, "Presentation"), "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                            || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (string file in files)
            {
                string relativePath = Path.GetRelativePath(root, file);
                string content = File.ReadAllText(file);

                foreach (Match match in Regex.Matches(content, "FontFamily\\s*=\\s*\"([^\"]+)\""))
                {
                    string value = match.Groups[1].Value;
                    Assert.True(
                        value == "{x:Static local:UiTypography.DefaultFontFamily}",
                        $"{relativePath} contains a direct FontFamily value: {value}");
                }

                foreach (Match match in Regex.Matches(content, "Property=\"FontFamily\"\\s+Value=\"([^\"]+)\""))
                {
                    string value = match.Groups[1].Value;
                    Assert.True(
                        value == "{x:Static local:UiTypography.DefaultFontFamily}"
                        || value == "Segoe MDL2 Assets",
                        $"{relativePath} contains a direct FontFamily setter: {value}");
                }

                if (!relativePath.EndsWith(@"Presentation\Views\Common\UiTypography.cs", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.DoesNotContain("new FontFamily(", content, StringComparison.Ordinal);
                    Assert.DoesNotContain("Segoe UI Variable Text", content, StringComparison.Ordinal);
                    Assert.DoesNotContain("Tahoma", content, StringComparison.Ordinal);
                }
            }
        }

        [Fact]
        public void TypographyThemeDefinesTheDefaultTextRole()
        {
            string root = FindRepositoryRoot();
            string typography = File.ReadAllText(Path.Combine(root, "Presentation", "Themes", "Typography.xaml"));

            Assert.Contains("<Style TargetType=\"{x:Type TextBlock}\">", typography, StringComparison.Ordinal);
            Assert.Contains("Property=\"FontFamily\" Value=\"{x:Static local:UiTypography.DefaultFontFamily}\"", typography, StringComparison.Ordinal);
            Assert.Contains("Property=\"FontSize\" Value=\"12\"", typography, StringComparison.Ordinal);
            Assert.Contains("Property=\"FontWeight\" Value=\"Normal\"", typography, StringComparison.Ordinal);
            Assert.Contains("Property=\"Foreground\" Value=\"{StaticResource Brush.Text.Primary}\"", typography, StringComparison.Ordinal);
        }

        [Fact]
        public void AmountTableCellsUseTabularFigures()
        {
            string root = FindRepositoryRoot();
            string tableTheme = File.ReadAllText(Path.Combine(root, "Presentation", "Themes", "Tables.xaml"));
            string guaranteeSurface = File.ReadAllText(Path.Combine(root, "Presentation", "Views", "Guarantees", "GuaranteesDashboardView.xaml"));
            string dashboardSurface = File.ReadAllText(Path.Combine(root, "Presentation", "Views", "Dashboard", "DashboardWorkspaceSurface.cs"));
            string banksSurface = File.ReadAllText(Path.Combine(root, "Presentation", "Views", "Banks", "BanksWorkspaceSurface.cs"));

            Assert.Contains("x:Key=\"TableAmountCell\"", tableTheme, StringComparison.Ordinal);
            Assert.Contains("Property=\"Typography.NumeralAlignment\" Value=\"Tabular\"", tableTheme, StringComparison.Ordinal);
            Assert.Contains("Style=\"{StaticResource TableAmountCell}\"", guaranteeSurface, StringComparison.Ordinal);
            Assert.Contains("WorkspaceSurfaceChrome.Style(\"TableAmountCell\")", dashboardSurface, StringComparison.Ordinal);
            Assert.Contains("\"TableAmountCell\"", banksSurface, StringComparison.Ordinal);
        }

        private static string ReadPresentationText()
        {
            string root = FindRepositoryRoot();
            IEnumerable<string> files = Directory
                .EnumerateFiles(Path.Combine(root, "Presentation"), "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
                            || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));

            return string.Join(Environment.NewLine, files.Select(File.ReadAllText));
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "GuaranteeManager.csproj")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate GuaranteeManager.csproj.");
        }
    }
}
