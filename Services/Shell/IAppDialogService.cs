using System.Windows;

namespace GuaranteeManager.Services
{
    public interface IAppDialogService
    {
        MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image);

        void ShowInformation(string message, string title);

        void ShowWarning(string message, string title);

        void ShowError(string message, string title);

        bool Confirm(string message, string title);
    }
}
