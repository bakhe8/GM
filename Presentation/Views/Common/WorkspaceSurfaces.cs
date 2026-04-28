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
using GuaranteeManager.Services;
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

        public static Grid Header(string title, string subtitle, Action? closeRequested)
        {
            var header = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFrom("#0F172A")
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFrom("#64748B"),
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            var closeButton = ActionButton("إغلاق", "White", "#D8E1EE", "#1F2937");
            closeButton.Click += (_, _) => closeRequested?.Invoke();
            Grid.SetColumn(closeButton, 1);
            header.Children.Add(titleStack);
            header.Children.Add(closeButton);
            return header;
        }

        public static Border Card(Thickness padding)
        {
            return new Border
            {
                Style = Style("Card"),
                Padding = padding
            };
        }

        public static Grid BuildReferenceWorkspace(UIElement toolbar, UIElement metrics, UIElement table, UIElement detailPanel)
        {
            var root = new Grid
            {
                Background = BrushResource("Brush.Canvas"),
                FlowDirection = FlowDirection.LeftToRight
            };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(310) });

            var main = new Grid
            {
                Margin = new Thickness(20),
                FlowDirection = FlowDirection.RightToLeft
            };
            main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            main.Children.Add(toolbar);
            Grid.SetRow(metrics, 2);
            main.Children.Add(metrics);
            Grid.SetRow(table, 4);
            main.Children.Add(table);

            root.Children.Add(main);
            Grid.SetColumn(detailPanel, 1);
            root.Children.Add(detailPanel);
            return root;
        }

        public static Border BuildReferenceDetailPanel(UIElement scrollContent, UIElement? quickActions = null)
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
            grid.RowDefinitions.Add(new RowDefinition { Height = quickActions == null ? new GridLength(0) : new GridLength(90) });

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

        public static Grid BuildReferencePager(string summaryText)
        {
            var grid = new Grid
            {
                Style = Style("ReferenceTablePager")
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var pageButton = new Button
            {
                Content = "1",
                Margin = new Thickness(6, 0, 0, 0),
                Style = Style("ReferenceTablePagerActiveButton")
            };

            var pageSizeButton = new Button
            {
                Content = "10",
                MinWidth = 46,
                Margin = new Thickness(12, 0, 0, 0),
                Style = Style("ReferenceTablePagerButton")
            };

            buttons.Children.Add(new Button
            {
                Content = "←",
                Style = Style("ReferenceTablePagerButton")
            });
            buttons.Children.Add(pageButton);
            buttons.Children.Add(pageSizeButton);
            buttons.Children.Add(new TextBlock
            {
                Text = "لكل صفحة",
                FontSize = 11,
                Foreground = BrushResource("Brush.Muted"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            });
            grid.Children.Add(buttons);

            var summary = new TextBlock
            {
                Text = summaryText,
                Style = Style("ReferenceTableFooterSummary")
            };
            grid.Children.Add(summary);
            return grid;
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
            var border = Card(new Thickness(14, 10, 14, 10));
            border.Margin = new Thickness(0, 0, 10, 0);
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFrom(accent),
                TextAlignment = TextAlignment.Right
            });
            stack.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = value.Length > 14 ? 18 : 27,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFrom("#0F172A"),
                Margin = new Thickness(0, 4, 0, 0),
                TextAlignment = TextAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            border.Child = stack;
            return border;
        }

        public static Grid InfoLine(string label, TextBlock value)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFrom("#94A3C8"),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(value, 1);
            grid.Children.Add(value);
            return grid;
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

    public sealed class SimpleWorkspaceSurface : UserControl
    {
        private SimpleWorkspaceSurface(
            string title,
            string subtitle,
            IReadOnlyList<SimpleWorkspaceSection> sections,
            Action? closeRequested)
        {
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = BrushFrom("#F7F9FC");
            Content = BuildLayout(title, subtitle, sections, closeRequested);
        }

        public static SimpleWorkspaceSurface CreateBanksWorkspace(IReadOnlyList<Guarantee> guarantees, Action? closeRequested)
        {
            var bankGroups = guarantees
                .GroupBy(item => item.Bank)
                .Select(group => new
                {
                    Bank = group.Key,
                    Count = group.Count(),
                    Amount = group.Sum(item => item.Amount),
                    Active = group.Count(item => item.LifecycleStatus == GuaranteeLifecycleStatus.Active)
                })
                .OrderByDescending(item => item.Amount)
                .ToList();

            List<string> overview =
            [
                $"عدد البنوك: {bankGroups.Count.ToString("N0", CultureInfo.InvariantCulture)}",
                $"إجمالي الضمانات: {guarantees.Count.ToString("N0", CultureInfo.InvariantCulture)}",
                $"إجمالي القيمة: {guarantees.Sum(item => item.Amount).ToString("N0", CultureInfo.InvariantCulture)} ريال"
            ];

            List<string> distribution = bankGroups
                .Select(item => $"{item.Bank} | {item.Count.ToString("N0", CultureInfo.InvariantCulture)} ضمان | نشط {item.Active.ToString("N0", CultureInfo.InvariantCulture)} | {item.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال")
                .DefaultIfEmpty("لا توجد بيانات بنكية.")
                .ToList();

            return new SimpleWorkspaceSurface(
                "البنوك",
                "تجميع سريع لمحفظة الضمانات حسب البنك والقيمة والحالة.",
                [
                    new SimpleWorkspaceSection("ملخص البنوك", overview),
                    new SimpleWorkspaceSection("توزيع الضمانات", distribution)
                ],
                closeRequested);
        }

        public static SimpleWorkspaceSurface CreateSettingsWorkspace(Action? closeRequested)
        {
            return new SimpleWorkspaceSurface(
                "الإعدادات",
                "مسارات التشغيل الحالية وقنوات حفظ الملفات التي يستخدمها البرنامج.",
                [
                    new SimpleWorkspaceSection(
                        "المسارات",
                        [
                            $"قاعدة البيانات: {AppPaths.DatabasePath}",
                            $"المرفقات: {AppPaths.AttachmentsFolder}",
                            $"خطابات الطلبات: {AppPaths.WorkflowLettersFolder}",
                            $"ردود البنوك: {AppPaths.WorkflowResponsesFolder}"
                        ])
                ],
                closeRequested);
        }

        private static Grid BuildLayout(
            string title,
            string subtitle,
            IReadOnlyList<SimpleWorkspaceSection> sections,
            Action? closeRequested)
        {
            var root = new Grid
            {
                Margin = new Thickness(20),
                FlowDirection = FlowDirection.RightToLeft,
                Background = BrushFrom("#F7F9FC")
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.Children.Add(BuildHeader(title, subtitle, closeRequested));

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Grid.SetRow(scroll, 1);

            var stack = new StackPanel();
            foreach (SimpleWorkspaceSection section in sections)
            {
                stack.Children.Add(BuildSectionCard(section));
            }

            scroll.Content = stack;
            root.Children.Add(scroll);
            return root;
        }

        private static Grid BuildHeader(string title, string subtitle, Action? closeRequested)
        {
            var header = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFrom("#0F172A")
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = BrushFrom("#64748B"),
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            var closeButton = BuildActionButton("إغلاق", "White", "#D8E1EE", "#1F2937");
            closeButton.Click += (_, _) => closeRequested?.Invoke();
            Grid.SetColumn(closeButton, 1);
            header.Children.Add(titleStack);
            header.Children.Add(closeButton);
            return header;
        }

        private static Border BuildSectionCard(SimpleWorkspaceSection section)
        {
            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = BrushFrom("#E1E8F2"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = section.Title,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = BrushFrom("#111827"),
                Margin = new Thickness(0, 0, 0, 10)
            });

            foreach (string item in section.Items)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = item,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = BrushFrom("#374151"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                });
            }

            card.Child = stack;
            return card;
        }

        private static string FormatGuaranteeAlert(Guarantee guarantee)
        {
            return $"{guarantee.GuaranteeNo} | {guarantee.Supplier} | {guarantee.Bank} | الانتهاء {guarantee.ExpiryDate:yyyy/MM/dd}";
        }

        private static Button BuildActionButton(string text, string background, string border, string foreground)
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

        private static SolidColorBrush BrushFrom(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }

        private sealed record SimpleWorkspaceSection(string Title, IReadOnlyList<string> Items);
    }

}
