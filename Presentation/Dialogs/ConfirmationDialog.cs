using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GuaranteeManager
{
    public sealed class ConfirmationDialog : Window
    {
        private ConfirmationDialog(string title, string message, string confirmText, string cancelText)
        {
            Title = title;
            Width = 430;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = UiTypography.DefaultFontFamily;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(
                this,
                nameof(ConfirmationDialog),
                () => DialogResult = true,
                "أكمل نافذة التأكيد الحالية أو أغلقها أولاً.",
                persistWindowState: false);
            UiInstrumentation.Identify(this, "Dialog.Confirmation", title);

            var root = new Grid { Margin = new Thickness(18, 16, 18, 16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var confirmButton = UiInstrumentation.Identify(
                new Button
                {
                    Content = confirmText,
                    IsDefault = true
                },
                "Dialog.Confirmation.ConfirmButton",
                confirmText);
            confirmButton.Click += (_, _) => DialogResult = true;

            var cancelButton = UiInstrumentation.Identify(
                new Button
                {
                    Content = cancelText,
                    IsCancel = true
                },
                "Dialog.Confirmation.CancelButton",
                cancelText);

            var actions = DialogFormSupport.BuildActionBar(confirmButton, cancelButton, 112, 96);
            Grid.SetRow(actions, 1);

            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });
            content.Children.Add(new TextBlock
            {
                Text = "لن يتم إرسال أي خطاب أو تسجيل حدث حتى تؤكد هذا الإجراء.",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                TextWrapping = TextWrapping.Wrap
            });

            root.Children.Add(content);
            root.Children.Add(actions);
            Content = root;
        }

        public static bool TryShow(string title, string message, string confirmText = "تأكيد", string cancelText = "إلغاء")
        {
            var dialog = new ConfirmationDialog(title, message, confirmText, cancelText)
            {
                Owner = Application.Current.MainWindow
            };

            return dialog.ShowDialog() == true;
        }
    }
}
