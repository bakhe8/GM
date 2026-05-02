using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace GuaranteeManager
{
    internal static class DialogChrome
    {
        public static void ApplyWindowDefaults(Window window)
        {
            window.FontFamily = UiTypography.DefaultFontFamily;
            window.Background = Brush("Brush.Canvas");
            TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(window, TextRenderingMode.ClearType);

            if (window.WindowStartupLocation == WindowStartupLocation.Manual)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
        }

        public static void ApplyContentDefaults(Window window)
        {
            if (window.Content is not DependencyObject root)
            {
                return;
            }

            ApplyDefaults(root, new HashSet<DependencyObject>());
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
