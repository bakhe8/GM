using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace GuaranteeManager.Services
{
    public static class AppMessageBox
    {
        public static MessageBoxResult Show(string message)
        {
            return Show(message, string.Empty, MessageBoxButton.OK, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string message, string title)
        {
            return Show(message, title, MessageBoxButton.OK, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons)
        {
            return Show(message, title, buttons, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
        {
            IAppDialogService? dialogs = Resolve();
            return dialogs != null
                ? dialogs.Show(message, title, buttons, image)
                : System.Windows.MessageBox.Show(message, title, buttons, image);
        }

        public static void ShowInformation(string message, string title)
        {
            IAppDialogService? dialogs = Resolve();
            if (dialogs != null)
            {
                dialogs.ShowInformation(message, title);
                return;
            }

            System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void ShowWarning(string message, string title)
        {
            IAppDialogService? dialogs = Resolve();
            if (dialogs != null)
            {
                dialogs.ShowWarning(message, title);
                return;
            }

            System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static void ShowError(string message, string title)
        {
            IAppDialogService? dialogs = Resolve();
            if (dialogs != null)
            {
                dialogs.ShowError(message, title);
                return;
            }

            System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static bool Confirm(string message, string title)
        {
            IAppDialogService? dialogs = Resolve();
            if (dialogs != null)
            {
                return dialogs.Confirm(message, title);
            }

            return System.Windows.MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        private static IAppDialogService? Resolve()
        {
            if (Application.Current is not App app || app.Services == null)
            {
                return null;
            }

            return app.Services.GetService<IAppDialogService>();
        }
    }
}
