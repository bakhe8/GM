using System;
using System.Windows;

namespace GuaranteeManager.Services
{
    public static class AppDialogService
    {
        public static void ShowInfo(string message, string title = "معلومة")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void ShowSuccess(string message, string title = "تمت العملية")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void ShowWarning(string message, string title = "تنبيه")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static void ShowError(string message, string title = "خطأ")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static void ShowError(Exception ex, string userMessage, string title = "خطأ")
        {
            if (ex is ApplicationOperationException operationException)
            {
                ShowError(operationException.UserMessageWithReference, title);
                return;
            }

            ShowError($"{userMessage}{Environment.NewLine}{ex.Message}", title);
        }

        public static bool ConfirmDelete(string subjectLabel, string identifier, string? details = null)
        {
            string message = $"هل أنت متأكد من حذف {subjectLabel} {identifier}؟";
            if (!string.IsNullOrWhiteSpace(details))
            {
                message += $"{Environment.NewLine}{details}";
            }

            return MessageBox.Show(message, "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        public static bool ConfirmDiscardChanges(string? details = null)
        {
            string message = "لديك تغييرات غير محفوظة. هل تريد الخروج والتراجع عنها؟";
            if (!string.IsNullOrWhiteSpace(details))
            {
                message += $"{Environment.NewLine}{details}";
            }

            return MessageBox.Show(message, "بيانات غير محفوظة", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        public static MessageBoxResult Ask(string message, string title, MessageBoxButton buttons, MessageBoxImage image = MessageBoxImage.Question)
        {
            return MessageBox.Show(message, title, buttons, image);
        }
    }
}
