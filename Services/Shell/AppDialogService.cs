using System.Windows;

namespace GuaranteeManager.Services
{
    public sealed class AppDialogService : IAppDialogService
    {
        private readonly IUiDiagnosticsService _diagnostics;

        public AppDialogService(IUiDiagnosticsService diagnostics)
        {
            _diagnostics = diagnostics;
        }

        public MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
        {
            _diagnostics.RecordEvent(
                "dialog.message",
                "show",
                new
                {
                    Title = title,
                    Buttons = buttons.ToString(),
                    Image = image.ToString(),
                    Message = message
                });
            return global::GuaranteeManager.RtlMessageDialog.ShowDialogMessage(message, title, buttons, image);
        }

        public void ShowInformation(string message, string title)
        {
            Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowWarning(string message, string title)
        {
            Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void ShowError(string message, string title)
        {
            Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public bool Confirm(string message, string title)
        {
            _diagnostics.RecordEvent(
                "dialog.message",
                "confirm.request",
                new
                {
                    Title = title,
                    Message = message
                });

            MessageBoxResult result = global::GuaranteeManager.RtlMessageDialog.ShowDialogMessage(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            _diagnostics.RecordEvent(
                "dialog.message",
                "confirm.result",
                new
                {
                    Title = title,
                    Accepted = result == MessageBoxResult.Yes
                });

            return result == MessageBoxResult.Yes;
        }
    }
}
