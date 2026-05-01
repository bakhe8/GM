using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;
using Microsoft.Win32;

namespace GuaranteeManager
{
    public static class WorkspaceSurfaceChrome
    {
        public static T Resource<T>(string key) where T : class
        {
            return (T)Application.Current.FindResource(key);
        }

        public static Style Style(string key)
        {
            return Resource<Style>(key);
        }

        public static Brush BrushResource(string key)
        {
            return Resource<Brush>(key);
        }

        public static Grid Root()
        {
            return new Grid
            {
                Margin = new Thickness(20),
                FlowDirection = FlowDirection.RightToLeft,
                Background = BrushFrom("#F7F9FC")
            };
        }

        public static Border Card(Thickness padding)
        {
            return new Border
            {
                Style = Style("Card"),
                Padding = padding
            };
        }

        public static Grid BuildReferenceWorkspace(
            UIElement toolbar,
            UIElement metrics,
            UIElement table,
            UIElement? detailPanel,
            UIElement? guidanceStrip = null)
        {
            var root = new Grid
            {
                Background = BrushResource("Brush.Canvas"),
                FlowDirection = FlowDirection.LeftToRight
            };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (detailPanel != null)
            {
                root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
            }

            var main = new Grid
            {
                Margin = new Thickness(20),
                FlowDirection = FlowDirection.RightToLeft
            };
            main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            if (guidanceStrip != null)
            {
                main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
                main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }
            else
            {
                main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }

            main.Children.Add(toolbar);
            Grid.SetRow(metrics, 2);
            main.Children.Add(metrics);
            if (guidanceStrip != null)
            {
                Grid.SetRow(table, 4);
                Grid.SetRow(guidanceStrip, 6);
                main.Children.Add(guidanceStrip);
            }
            else
            {
                Grid.SetRow(table, 4);
            }

            main.Children.Add(table);

            root.Children.Add(main);
            if (detailPanel != null)
            {
                Grid.SetColumn(detailPanel, 1);
                root.Children.Add(detailPanel);
            }

            return root;
        }

        public static Border BuildReferenceDetailPanel(
            UIElement scrollContent,
            UIElement? quickActions = null,
            double quickActionsHeight = 90)
        {
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = BrushFrom("#E3E9F2"),
                BorderThickness = new Thickness(1, 0, 0, 0),
                FlowDirection = FlowDirection.RightToLeft
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = quickActions == null ? new GridLength(0) : new GridLength(quickActionsHeight)
            });

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                Content = scrollContent
            };
            grid.Children.Add(scroll);

            if (quickActions != null)
            {
                Grid.SetRow(quickActions, 1);
                grid.Children.Add(quickActions);
            }

            border.Child = grid;
            return border;
        }

        public static Border BuildReferenceTableShell(UIElement header, ListBox listBox, UIElement footer)
        {
            listBox.Style = Style("ReferenceTableRowsListBox");

            var border = new Border
            {
                Style = Style("ReferenceTableContainer")
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });

            Grid.SetRow(header, 0);
            grid.Children.Add(header);
            Grid.SetRow(listBox, 1);
            grid.Children.Add(listBox);
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);

            border.Child = grid;
            return border;
        }

        public static Grid BuildReferenceHeaderBand(params (string Text, int Column, bool RightAligned)[] columns)
        {
            var header = new Grid
            {
                Style = Style("ReferenceTableHeaderBand")
            };
            header.Children.Add(new Border
            {
                Style = Style("ReferenceTableHeaderDivider")
            });

            var inner = new Grid
            {
                Margin = new Thickness(9, 0, 9, 0)
            };

            foreach ((_, _, _) in columns)
            {
                inner.ColumnDefinitions.Add(new ColumnDefinition());
            }

            for (int index = 0; index < columns.Length; index++)
            {
                var text = new TextBlock
                {
                    Text = columns[index].Text,
                    Style = columns[index].RightAligned ? Style("TableHeaderRight") : Style("TableHeaderText")
                };
                Grid.SetColumn(text, columns[index].Column);
                inner.Children.Add(text);
            }

            header.Children.Add(inner);
            return header;
        }

        public static Grid BankTableCell(string bankName, ImageSource? logoSource, int column, double logoSize = 16, double textMaxWidth = 132)
        {
            var cell = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true
            };

            var content = new Grid
            {
                FlowDirection = FlowDirection.RightToLeft,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var logo = new Image
            {
                Source = logoSource,
                Width = logoSize,
                Height = logoSize,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
            Grid.SetColumn(logo, 0);
            content.Children.Add(logo);

            var name = new TextBlock
            {
                Text = bankName,
                Style = Style("TableCellRight"),
                MaxWidth = textMaxWidth,
                HorizontalAlignment = HorizontalAlignment.Right,
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(name, 1);
            content.Children.Add(name);

            cell.Children.Add(content);

            Grid.SetColumn(cell, column);
            return cell;
        }

        public static Button ToolbarButton(string text, bool primary = false, string? automationId = null)
        {
            Button button = new Button
            {
                Content = text,
                Height = 36,
                MinWidth = primary ? 138 : 96,
                Style = Style(primary ? "PrimaryButton" : "BaseButton")
            };

            AutomationProperties.SetName(button, text);
            if (!string.IsNullOrWhiteSpace(automationId))
            {
                AutomationProperties.SetAutomationId(button, automationId);
            }

            return button;
        }

        public static ComboBox ToolbarComboBox()
        {
            return new ComboBox
            {
                Style = Style("FilterComboBox")
            };
        }

        public static Border ToolbarSearchBox(TextBox textBox, string placeholder)
        {
            textBox.Style = Style("SearchTextBox");

            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = BrushFrom("#D8E1EE"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Height = 36,
                Padding = new Thickness(11, 0, 11, 0)
            };

            var grid = new Grid
            {
                FlowDirection = FlowDirection.RightToLeft
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var placeholderText = new TextBlock
            {
                Text = placeholder,
                FontSize = 11,
                Foreground = BrushFrom("#94A3B8"),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            placeholderText.SetBinding(
                UIElement.VisibilityProperty,
                new Binding("Text")
                {
                    Source = textBox,
                    Converter = Resource<IValueConverter>("EmptyStringToVisibilityConverter")
                });

            Grid.SetColumn(textBox, 0);
            Grid.SetColumn(placeholderText, 0);
            grid.Children.Add(textBox);
            grid.Children.Add(placeholderText);

            var icon = new TextBlock
            {
                Text = "⌕",
                FontSize = 14,
                Foreground = BrushFrom("#64748B"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(icon, 1);
            grid.Children.Add(icon);

            border.Child = grid;
            return border;
        }

        public static Border MetricCard(string label, string value, string accent)
        {
            TextBlock valueBlock = MetricValueText(value.Length > 16 ? 24 : 32);
            valueBlock.Text = value;
            return MetricCard(label, valueBlock, accent);
        }

        public static Border MetricCard(string label, TextBlock value, string accent)
        {
            return MetricCard(MetricLabelText(label, accent), value);
        }

        public static Border MetricCard(TextBlock label, TextBlock value)
        {
            ConfigureMetricLabel(label);
            ConfigureMetricValue(value);
            var border = Card(new Thickness(16, 12, 16, 12));
            border.Margin = new Thickness(0);
            border.MinHeight = 84;

            var stack = new StackPanel();
            stack.Children.Add(label);
            stack.Children.Add(value);
            border.Child = stack;
            return border;
        }

        public static TextBlock MetricValueText(double fontSize = 32)
        {
            var textBlock = new TextBlock
            {
                Text = "0",
                FontSize = fontSize
            };
            ConfigureMetricValue(textBlock);
            return textBlock;
        }

        public static TextBlock MetricLabelText(string label, string accent)
        {
            var textBlock = new TextBlock
            {
                Text = label,
                Foreground = BrushFrom(accent)
            };
            ConfigureMetricLabel(textBlock);
            return textBlock;
        }

        public static void ConfigureMetricLabel(TextBlock label)
        {
            label.FontSize = 13;
            label.FontWeight = FontWeights.SemiBold;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.TextAlignment = TextAlignment.Center;
            label.TextTrimming = TextTrimming.CharacterEllipsis;
        }

        public static void ConfigureMetricValue(TextBlock value)
        {
            value.FontWeight = FontWeights.Bold;
            value.Foreground = BrushFrom("#0F172A");
            value.Margin = new Thickness(0, 2, 0, 0);
            value.HorizontalAlignment = HorizontalAlignment.Center;
            value.TextAlignment = TextAlignment.Center;
            value.TextTrimming = TextTrimming.CharacterEllipsis;
        }

        public static void ApplyMetricCardSpacing(Panel metrics)
        {
            for (int index = 0; index < metrics.Children.Count; index++)
            {
                if (metrics.Children[index] is FrameworkElement element)
                {
                    element.Margin = index == metrics.Children.Count - 1
                        ? new Thickness(0)
                        : new Thickness(0, 0, 10, 0);
                }
            }
        }

        public static void ApplyToolbarGroupSpacing(params FrameworkElement[] elements)
        {
            for (int index = 0; index < elements.Length; index++)
            {
                elements[index].Margin = index == elements.Length - 1
                    ? new Thickness(0)
                    : new Thickness(0, 0, 8, 0);
            }
        }

        public static Grid InfoLine(string label, TextBlock value)
        {
            return DetailFactLine(label, value, "Icon.Document");
        }

        public static Grid DetailFactLine(
            string label,
            TextBlock value,
            string iconKey,
            RoutedEventHandler? copyClicked = null,
            string? automationId = null,
            string? copyName = null)
        {
            return DetailFactRow(
                BuildDetailFactLabel(label),
                value,
                iconKey,
                allowWrapping: false,
                copyClicked,
                automationId,
                copyName);
        }

        public static Grid DetailFactLine(
            TextBlock label,
            TextBlock value,
            string iconKey,
            RoutedEventHandler? copyClicked = null,
            string? automationId = null,
            string? copyName = null)
        {
            return DetailFactRow(label, value, iconKey, false, copyClicked, automationId, copyName);
        }

        public static Grid DetailFactBlock(
            string label,
            TextBlock value,
            string iconKey,
            RoutedEventHandler? copyClicked = null,
            string? automationId = null,
            string? copyName = null)
        {
            return DetailFactRow(
                BuildDetailFactLabel(label),
                value,
                iconKey,
                allowWrapping: true,
                copyClicked,
                automationId,
                copyName);
        }

        public static Grid DetailFactBlock(
            TextBlock label,
            TextBlock value,
            string iconKey,
            RoutedEventHandler? copyClicked = null,
            string? automationId = null,
            string? copyName = null)
        {
            return DetailFactRow(label, value, iconKey, true, copyClicked, automationId, copyName);
        }

        private static Grid DetailFactRow(
            TextBlock label,
            TextBlock value,
            string iconKey,
            bool allowWrapping,
            RoutedEventHandler? copyClicked,
            string? automationId,
            string? copyName)
        {
            var grid = new Grid
            {
                MinHeight = 28,
                Margin = new Thickness(0, 0, 0, allowWrapping ? 8 : 0),
                FlowDirection = FlowDirection.RightToLeft
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

            label.VerticalAlignment = allowWrapping ? VerticalAlignment.Top : VerticalAlignment.Center;
            if (label.FontSize <= 0)
            {
                label.FontSize = 10;
            }

            var labelPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = allowWrapping ? VerticalAlignment.Top : VerticalAlignment.Center,
                Margin = new Thickness(0, allowWrapping ? 3 : 0, 0, 0)
            };
            labelPanel.Children.Add(CreateDetailFactIcon(iconKey, "#94A3B8", 12));
            labelPanel.Children.Add(new Border { Width = 7 });
            labelPanel.Children.Add(label);
            grid.Children.Add(labelPanel);

            value.VerticalAlignment = allowWrapping ? VerticalAlignment.Top : VerticalAlignment.Center;
            value.TextAlignment = TextAlignment.Right;
            if (allowWrapping)
            {
                value.TextWrapping = TextWrapping.Wrap;
                value.Margin = new Thickness(0, 3, 0, 0);
            }
            else
            {
                value.TextWrapping = TextWrapping.NoWrap;
                value.TextTrimming = TextTrimming.CharacterEllipsis;
            }

            Grid.SetColumn(value, 2);
            grid.Children.Add(value);

            UIElement affordance = copyClicked == null
                ? BuildDetailFactInfoGlyph(label.Text)
                : BuildDetailFactCopyButton(
                    copyClicked,
                    automationId,
                    copyName ?? $"نسخ {label.Text}");
            Grid.SetColumn(affordance, 3);
            grid.Children.Add(affordance);

            return grid;
        }

        private static TextBlock BuildDetailFactLabel(string label)
        {
            return new TextBlock
            {
                Text = label,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFrom("#94A3C8"),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static Button BuildDetailFactCopyButton(
            RoutedEventHandler copyClicked,
            string? automationId,
            string? copyName)
        {
            string name = string.IsNullOrWhiteSpace(copyName) ? "نسخ القيمة" : copyName;
            var button = new Button
            {
                Style = Style("IconOnlyButton"),
                ToolTip = name,
                Content = CreateDetailFactIcon("Icon.Copy", "#64748B", 12),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            button.Click += copyClicked;
            AutomationProperties.SetName(button, name);
            if (!string.IsNullOrWhiteSpace(automationId))
            {
                AutomationProperties.SetAutomationId(button, automationId);
            }

            return button;
        }

        private static UIElement BuildDetailFactInfoGlyph(string label)
        {
            string name = string.IsNullOrWhiteSpace(label) ? "معلومة" : $"معلومة: {label}";
            var glyph = new Border
            {
                Width = 28,
                Height = 28,
                Child = CreateDetailFactIcon("Icon.Info", "#94A3B8", 12),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = name
            };
            AutomationProperties.SetName(glyph, name);
            return glyph;
        }

        private static UIElement CreateDetailFactIcon(string iconKey, string strokeColor, double size)
        {
            if (Application.Current.TryFindResource(iconKey) is not Geometry geometry)
            {
                return new Border { Width = size, Height = size };
            }

            return new Viewbox
            {
                Width = size,
                Height = size,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new System.Windows.Shapes.Path
                {
                    Data = geometry,
                    Stroke = BrushFrom(strokeColor),
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                }
            };
        }

        public static Border Divider()
        {
            return new Border
            {
                Height = 1,
                Background = BrushFrom("#EDF2F7"),
                Margin = new Thickness(0, 16, 0, 16)
            };
        }

        public static TextBlock Text(double fontSize, FontWeight fontWeight, string foreground)
        {
            return new TextBlock
            {
                FontSize = fontSize,
                FontWeight = fontWeight,
                Foreground = BrushFrom(foreground),
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap
            };
        }

        public static Button ActionButton(string text, string background, string border, string foreground)
        {
            return new Button
            {
                Content = text,
                Height = 34,
                MinWidth = 102,
                Padding = new Thickness(12, 0, 12, 0),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Background = BrushFrom(background),
                BorderBrush = BrushFrom(border),
                Foreground = BrushFrom(foreground),
                BorderThickness = new Thickness(1)
            };
        }

        public static SolidColorBrush BrushFrom(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }
    }

}
