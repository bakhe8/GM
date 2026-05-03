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
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = UiTypography.DefaultFontFamily;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(
                this,
                nameof(PromptDialog),
                () => DialogResult = true,
                "أغلق نافذة الإدخال الحالية أو أكملها أولاً.",
                persistWindowState: false);

            var root = new Grid { Margin = new Thickness(18, 16, 18, 16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
                Margin = new Thickness(0, 0, 0, 8)
            });

            _input = new TextBox
            {
                Text = defaultValue,
                Height = 34,
                FontSize = 12,
                Padding = new Thickness(8, 0, 8, 0),
                TextAlignment = TextAlignment.Right,
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8E1EE")),
                BorderThickness = new Thickness(1)
            };
            Grid.SetRow(_input, 1);
            root.Children.Add(_input);

            var okButton = new Button
            {
                Content = "موافق",
                IsDefault = true
            };
            okButton.Click += (_, _) => DialogResult = true;

            var cancelButton = new Button
            {
                Content = "إلغاء",
                IsCancel = true
            };

            var actions = DialogFormSupport.BuildActionBar(okButton, cancelButton, 96, 96);
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
