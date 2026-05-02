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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Microsoft.Win32;

namespace GuaranteeManager
{
    public sealed class BanksSummaryDialog : Window
    {
        private BanksSummaryDialog(IReadOnlyList<Guarantee> guarantees)
        {
            Title = "البنوك";
            Width = 540;
            Height = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = UiTypography.DefaultFontFamily;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(this, nameof(BanksSummaryDialog));

            var list = new ListBox { Margin = new Thickness(16) };
            foreach (var group in guarantees.GroupBy(item => item.Bank).OrderByDescending(group => group.Sum(item => item.Amount)))
            {
                list.Items.Add($"{group.Key} | {group.Count():N0} ضمان | {ArabicAmountFormatter.FormatSaudiRiyals(group.Sum(item => item.Amount))}");
            }

            Content = list;
        }

        public static void ShowFor(IReadOnlyList<Guarantee> guarantees)
        {
            App.CurrentApp.GetRequiredService<SecondaryWindowManager>().ShowDialog(
                "banks-summary",
                () => new BanksSummaryDialog(guarantees),
                "البنوك",
                "نافذة البنوك مفتوحة بالفعل.");
        }
    }

    public sealed class ReportPickerDialog : Window
    {
        private readonly ListBox _list = new();
        private string _selectedKey = string.Empty;

        private ReportPickerDialog()
        {
            Title = "التقارير";
            Width = 520;
            Height = 430;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = UiTypography.DefaultFontFamily;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(this, nameof(ReportPickerDialog));

            var root = new DockPanel { Margin = new Thickness(16) };
            foreach (var action in WorkspaceReportCatalog.PortfolioActions.Concat(WorkspaceReportCatalog.OperationalActions))
            {
                _list.Items.Add(new ReportOption(action.Key, action.Title, action.Description));
            }

            _list.DisplayMemberPath = nameof(ReportOption.Display);
            _list.SelectedIndex = 0;

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 12, 0, 0)
            };
            DockPanel.SetDock(actions, Dock.Bottom);
            var runButton = new Button { Content = "إنشاء التقرير", Width = 112, Height = 32, IsDefault = true, Margin = new Thickness(8, 0, 0, 0) };
            runButton.Click += (_, _) => Accept();
            var cancelButton = new Button { Content = "إلغاء", Width = 90, Height = 32, IsCancel = true };
            actions.Children.Add(runButton);
            actions.Children.Add(cancelButton);
            root.Children.Add(actions);
            root.Children.Add(_list);
            Content = root;
        }

        public static bool TryShow(out string reportKey)
        {
            var dialog = new ReportPickerDialog();
            bool accepted = App.CurrentApp.GetRequiredService<SecondaryWindowManager>().ShowDialog(
                                "report-picker",
                                () => dialog,
                                "التقارير",
                                "نافذة اختيار التقارير مفتوحة بالفعل.") == true;
            reportKey = dialog._selectedKey;
            return accepted && !string.IsNullOrWhiteSpace(reportKey);
        }

        private void Accept()
        {
            if (_list.SelectedItem is not ReportOption option)
            {
                return;
            }

            _selectedKey = option.Key;
            DialogResult = true;
        }

        private sealed record ReportOption(string Key, string Title, string Description)
        {
            public string Display => $"{Title} - {Description}";
        }
    }

    public sealed class SettingsDialog : Window
    {
        private SettingsDialog()
        {
            Title = "الإعدادات";
            Width = 540;
            Height = 360;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = UiTypography.DefaultFontFamily;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F9FC"));
            DialogWindowSupport.Attach(this, nameof(SettingsDialog));

            var text = new TextBox
            {
                Margin = new Thickness(16),
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Text =
                    $"قاعدة البيانات:\n{AppPaths.DatabasePath}\n\n" +
                    $"المرفقات:\n{AppPaths.AttachmentsFolder}\n\n" +
                    $"خطابات الطلبات:\n{AppPaths.WorkflowLettersFolder}\n\n" +
                    $"ردود البنوك:\n{AppPaths.WorkflowResponsesFolder}"
            };

            Content = text;
        }

        public static void ShowFor()
        {
            App.CurrentApp.GetRequiredService<SecondaryWindowManager>().ShowDialog(
                "settings-summary",
                () => new SettingsDialog(),
                "الإعدادات",
                "نافذة الإعدادات مفتوحة بالفعل.");
        }
    }

}
