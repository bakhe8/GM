using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GuaranteeManager
{
    public sealed class PromptDialog : Window
    {
        private readonly TextBox _input;

        private PromptDialog(string title, string label, string defaultValue)
        {
            Title = title;
            Width = 360;
            Height = 174;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(this, nameof(PromptDialog), () => DialogResult = true, "أغلق نافذة الإدخال الحالية أو أكملها أولاً.");

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            root.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827")),
                Margin = new Thickness(0, 0, 0, 8)
            });

            _input = new TextBox
            {
                Text = defaultValue,
                Height = 34,
                FontSize = 12,
                Padding = new Thickness(8, 0, 8, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8E1EE")),
                BorderThickness = new Thickness(1)
            };
            Grid.SetRow(_input, 1);
            root.Children.Add(_input);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 16, 0, 0)
            };

            var okButton = new Button
            {
                Content = "موافق",
                Width = 88,
                Height = 32,
                IsDefault = true,
                Margin = new Thickness(8, 0, 0, 0)
            };
            okButton.Click += (_, _) => DialogResult = true;

            var cancelButton = new Button
            {
                Content = "إلغاء",
                Width = 88,
                Height = 32,
                IsCancel = true
            };

            actions.Children.Add(okButton);
            actions.Children.Add(cancelButton);
            Grid.SetRow(actions, 2);
            root.Children.Add(actions);

            Content = root;
        }

        public static bool TryShow(string title, string label, string defaultValue, out string value)
        {
            var dialog = new PromptDialog(title, label, defaultValue)
            {
                Owner = Application.Current.MainWindow
            };

            bool accepted = dialog.ShowDialog() == true;
            value = accepted ? dialog._input.Text : string.Empty;
            return accepted;
        }
    }
}
