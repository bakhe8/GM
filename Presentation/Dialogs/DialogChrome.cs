using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Shell;

namespace GuaranteeManager
{
    internal static class DialogChrome
    {
        private static readonly DependencyProperty IsChromeWrappedProperty =
            DependencyProperty.RegisterAttached(
                "IsChromeWrapped",
                typeof(bool),
                typeof(DialogChrome),
                new PropertyMetadata(false));

        public static void ApplyWindowDefaults(Window window)
        {
            bool canResize = window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
            window.WindowStyle = WindowStyle.None;
            window.FontFamily = UiTypography.DefaultFontFamily;
            window.Background = Brush("Brush.Canvas");
            WindowChrome.SetWindowChrome(
                window,
                new WindowChrome
                {
                    CaptionHeight = 0,
                    CornerRadius = new CornerRadius(8),
                    GlassFrameThickness = new Thickness(0),
                    ResizeBorderThickness = canResize ? new Thickness(6) : new Thickness(0),
                    UseAeroCaptionButtons = false
                });
            TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(window, TextRenderingMode.ClearType);

            if (window.WindowStartupLocation == WindowStartupLocation.Manual)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
        }

        public static void ApplyContentDefaults(Window window)
        {
            WrapContent(window);

            if (window.Content is not DependencyObject root)
            {
                return;
            }

            ApplyDefaults(root, new HashSet<DependencyObject>());
        }

        private static void WrapContent(Window window)
        {
            if (window.Content is not UIElement content
                || Equals(window.GetValue(IsChromeWrappedProperty), true))
            {
                return;
            }

            window.SetValue(IsChromeWrappedProperty, true);
            window.Content = null;

            var shell = new Border
            {
                Background = Brush("Brush.Canvas"),
                BorderBrush = Brush("Brush.Border"),
                BorderThickness = new Thickness(1),
                ClipToBounds = true
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(BuildTitleBar(window));
            Grid.SetRow(content, 1);
            grid.Children.Add(content);

            shell.Child = grid;
            window.Content = shell;
        }

        private static UIElement BuildTitleBar(Window window)
        {
            var titleBar = new Border
            {
                Background = Brush("Brush.TopBar"),
                Height = 38
            };
            titleBar.MouseLeftButtonDown += (_, e) => DragWindow(window, e);

            var grid = new Grid { FlowDirection = FlowDirection.LeftToRight };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var closeButton = new Button
            {
                Style = Style("ChromeCloseButton"),
                Width = 38,
                Height = 38,
                ToolTip = "إغلاق",
                Content = BuildTitleBarIcon("Icon.Close", 10, 2)
            };
            closeButton.Click += (_, _) => window.Close();
            AutomationProperties.SetName(closeButton, "إغلاق");
            grid.Children.Add(closeButton);

            var title = new TextBlock
            {
                Text = window.Title,
                Foreground = Brush("Brush.Text.OnPrimary"),
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(16, 0, 14, 0)
            };
            Grid.SetColumn(title, 2);
            grid.Children.Add(title);

            titleBar.Child = grid;
            return titleBar;
        }

        private static UIElement BuildTitleBarIcon(string iconKey, double size, double strokeThickness)
        {
            if (Application.Current.TryFindResource(iconKey) is not Geometry geometry)
            {
                return new Border { Width = size, Height = size };
            }

            return new Viewbox
            {
                Width = size,
                Height = size,
                Child = new Path
                {
                    Data = geometry,
                    Stroke = Brush("Brush.Text.OnPrimary"),
                    StrokeThickness = strokeThickness,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                }
            };
        }

        private static void DragWindow(Window window, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            try
            {
                window.DragMove();
            }
            catch (InvalidOperationException)
            {
                // DragMove can throw if Windows has already ended the mouse operation.
            }
        }

        private static void ApplyDefaults(DependencyObject element, HashSet<DependencyObject> visited)
        {
            if (!visited.Add(element))
            {
                return;
            }

            switch (element)
            {
                case Button button:
                    ApplyButton(button);
                    break;
                case TextBox textBox:
                    ApplyTextBox(textBox);
                    break;
                case ComboBox comboBox:
                    ApplyComboBox(comboBox);
                    break;
                case ListBox listBox:
                    ApplyListBox(listBox);
                    break;
                case TextBlock textBlock:
                    ApplyTextBlock(textBlock);
                    break;
            }

            foreach (object child in LogicalTreeHelper.GetChildren(element))
            {
                if (child is DependencyObject dependencyChild)
                {
                    ApplyDefaults(dependencyChild, visited);
                }
            }

            if (element is not Visual and not Visual3D)
            {
                return;
            }

            int childrenCount = VisualTreeHelper.GetChildrenCount(element);
            for (int index = 0; index < childrenCount; index++)
            {
                ApplyDefaults(VisualTreeHelper.GetChild(element, index), visited);
            }
        }

        private static void ApplyButton(Button button)
        {
            if (IsUnset(button, Control.StyleProperty))
            {
                button.Style = Style(button.IsDefault ? "PrimaryButton" : "BaseButton");
            }

            button.FontFamily = UiTypography.DefaultFontFamily;
            TextOptions.SetTextFormattingMode(button, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(button, TextRenderingMode.ClearType);
        }

        private static void ApplyTextBox(TextBox textBox)
        {
            textBox.FontFamily = UiTypography.DefaultFontFamily;
            SetIfUnset(textBox, Control.FontSizeProperty, 12d);
            SetIfUnset(textBox, Control.ForegroundProperty, Brush("Brush.Text.Primary"));
            SetIfUnset(textBox, Control.BackgroundProperty, Brushes.White);
            SetIfUnset(textBox, Control.BorderBrushProperty, Brush("Brush.Border.Control"));
            SetIfUnset(textBox, Control.BorderThicknessProperty, new Thickness(1));
            SetIfUnset(textBox, Control.PaddingProperty, new Thickness(8, textBox.AcceptsReturn ? 6 : 0, 8, textBox.AcceptsReturn ? 6 : 0));

            if (!textBox.AcceptsReturn && IsUnset(textBox, Control.VerticalContentAlignmentProperty))
            {
                textBox.VerticalContentAlignment = VerticalAlignment.Center;
            }

            TextOptions.SetTextFormattingMode(textBox, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(textBox, TextRenderingMode.ClearType);
        }

        private static void ApplyComboBox(ComboBox comboBox)
        {
            comboBox.FontFamily = UiTypography.DefaultFontFamily;
            SetIfUnset(comboBox, Control.FontSizeProperty, 12d);
            SetIfUnset(comboBox, Control.ForegroundProperty, Brush("Brush.Text.Primary"));
            SetIfUnset(comboBox, Control.BackgroundProperty, Brushes.White);
            SetIfUnset(comboBox, Control.BorderBrushProperty, Brush("Brush.Border.Control"));
            SetIfUnset(comboBox, Control.BorderThicknessProperty, new Thickness(1));
            SetIfUnset(comboBox, Control.PaddingProperty, new Thickness(8, 0, 8, 0));
            SetIfUnset(comboBox, Control.VerticalContentAlignmentProperty, VerticalAlignment.Center);
            TextOptions.SetTextFormattingMode(comboBox, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(comboBox, TextRenderingMode.ClearType);
        }

        private static void ApplyListBox(ListBox listBox)
        {
            listBox.FontFamily = UiTypography.DefaultFontFamily;
            SetIfUnset(listBox, Control.FontSizeProperty, 12d);
            SetIfUnset(listBox, Control.ForegroundProperty, Brush("Brush.Text.Primary"));
            SetIfUnset(listBox, Control.BackgroundProperty, Brushes.White);
            SetIfUnset(listBox, Control.BorderBrushProperty, Brush("Brush.Border.Control"));
            SetIfUnset(listBox, Control.BorderThicknessProperty, new Thickness(1));
            TextOptions.SetTextFormattingMode(listBox, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(listBox, TextRenderingMode.ClearType);
        }

        private static void ApplyTextBlock(TextBlock textBlock)
        {
            textBlock.FontFamily = UiTypography.DefaultFontFamily;
            TextOptions.SetTextFormattingMode(textBlock, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(textBlock, TextRenderingMode.ClearType);
        }

        private static bool IsUnset(DependencyObject element, DependencyProperty property)
            => element.ReadLocalValue(property) == DependencyProperty.UnsetValue;

        private static void SetIfUnset(DependencyObject element, DependencyProperty property, object value)
        {
            if (IsUnset(element, property))
            {
                element.SetValue(property, value);
            }
        }

        private static Style Style(string key)
            => (Style)Application.Current.FindResource(key);

        private static Brush Brush(string key)
            => (Brush)Application.Current.FindResource(key);
    }
}
