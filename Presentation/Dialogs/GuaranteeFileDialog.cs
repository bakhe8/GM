using System.Windows;
using System.Windows.Media;
using GuaranteeManager.Services;

namespace GuaranteeManager
{
    public sealed class GuaranteeFileDialog : Window
    {
        private GuaranteeFileDialog(ShellViewModel viewModel, GuaranteeRow row)
        {
            Title = $"ملف الضمان - {row.GuaranteeNo}";
            Width = 480;
            Height = 860;
            MinWidth = 420;
            MinHeight = 680;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = WorkspaceSurfaceChrome.BrushFrom("#F7F9FC");
            DataContext = viewModel;
            DialogWindowSupport.Attach(this, $"{nameof(GuaranteeFileDialog)}:{row.RootId}", navigationReason: "ملف ضمان مفتوح");

            Content = new GuaranteeDetailPanel
            {
                DataContext = viewModel
            };
        }

        public static void ShowFor(ShellViewModel viewModel, GuaranteeRow row)
        {
            App.CurrentApp.GetRequiredService<SecondaryWindowManager>().ShowDialog(
                $"guarantee-file:{row.RootId}",
                () => new GuaranteeFileDialog(viewModel, row),
                "ملف الضمان",
                "ملف هذا الضمان مفتوح بالفعل.");
        }
    }
}
