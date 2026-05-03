using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GuaranteeManager
{
    internal sealed class RtlMessageDialog : Window
    {
        private RtlMessageDialog(
            string title,
            string message,
            string confirmText,
            string? cancelText,
            MessageBoxResult confirmResult,
            MessageBoxResult cancelResult)
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
                nameof(RtlMessageDialog),
                () => CloseWithResult(confirmResult),
                "أكمل نافذة الرسالة الحالية أو أغلقها أولاً.",
                persistWindowState: false);
            UiInstrumentation.Identify(this, "Dialog.Message", title);

            var root = new Grid { Margin = new Thickness(18, 16, 18, 16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var text = new TextBlock
            {
                Text = message,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            root.Children.Add(text);

            var confirmButton = UiInstrumentation.Identify(
                new Button
                {
                    Content = confirmText,
                    IsDefault = true
                },
                "Dialog.Message.ConfirmButton",
                confirmText);
            confirmButton.Click += (_, _) => CloseWithResult(confirmResult);

            FrameworkElement actions;
            if (string.IsNullOrWhiteSpace(cancelText))
            {
                var panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                confirmButton.MinWidth = 96;
                panel.Children.Add(confirmButton);
                actions = panel;
            }
            else
            {
                var cancelButton = UiInstrumentation.Identify(
                    new Button
                    {
                        Content = cancelText,
                        IsCancel = true
                    },
                    "Dialog.Message.CancelButton",
                    cancelText);
                cancelButton.Click += (_, _) => CloseWithResult(cancelResult);
                actions = DialogFormSupport.BuildActionBar(confirmButton, cancelButton, 96, 96);
            }

            Grid.SetRow(actions, 1);
            root.Children.Add(actions);
            Content = root;
        }

        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public static MessageBoxResult ShowDialogMessage(
            string message,
            string title,
            MessageBoxButton buttons,
            MessageBoxImage image)
        {
            if (Application.Current == null)
            {
                return ShowSystemMessage(message, title, buttons, image);
            }

            return buttons switch
            {
                MessageBoxButton.YesNo => ShowCore(title, message, "نعم", "لا", MessageBoxResult.Yes, MessageBoxResult.No),
                MessageBoxButton.OK => ShowCore(title, message, "حسنًا", null, MessageBoxResult.OK, MessageBoxResult.OK),
                _ => ShowSystemMessage(message, title, buttons, image)
            };
        }

        private static MessageBoxResult ShowCore(
            string title,
            string message,
            string confirmText,
            string? cancelText,
            MessageBoxResult confirmResult,
            MessageBoxResult cancelResult)
        {
            var dialog = new RtlMessageDialog(title, message, confirmText, cancelText, confirmResult, cancelResult)
            {
                Owner = Application.Current?.MainWindow
            };

            _ = dialog.ShowDialog();
            return dialog.Result == MessageBoxResult.None ? cancelResult : dialog.Result;
        }

        private static MessageBoxResult ShowSystemMessage(
            string message,
            string title,
            MessageBoxButton buttons,
            MessageBoxImage image)
        {
            return System.Windows.MessageBox.Show(
                message,
                title,
                buttons,
                image,
                MessageBoxResult.None,
                MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }

        private void CloseWithResult(MessageBoxResult result)
        {
            Result = result;
            DialogResult = result is MessageBoxResult.OK or MessageBoxResult.Yes;
        }
    }

    public static class MessageBox
    {
        public static MessageBoxResult Show(string message)
            => RtlMessageDialog.ShowDialogMessage(message, string.Empty, MessageBoxButton.OK, MessageBoxImage.None);

        public static MessageBoxResult Show(string message, string title)
            => RtlMessageDialog.ShowDialogMessage(message, title, MessageBoxButton.OK, MessageBoxImage.None);

        public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons)
            => RtlMessageDialog.ShowDialogMessage(message, title, buttons, MessageBoxImage.None);

        public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
            => RtlMessageDialog.ShowDialogMessage(message, title, buttons, image);
    }
}
