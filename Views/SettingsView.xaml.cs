using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using GuaranteeManager.Contracts;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using GuaranteeManager.ViewModels;
#if DEBUG
using GuaranteeManager.Development;
#endif
using Microsoft.Win32;

namespace GuaranteeManager.Views
{
    public partial class SettingsView : UserControl, IRefreshableView
    {
        private readonly BackupService _backupService;
#if DEBUG
        private readonly DataSeedingService? _seedingService;
#endif
        private readonly SettingsViewModel _viewModel;

        public SettingsView(
            IDatabaseService databaseService,
            BackupService backupService
#if DEBUG
            , DataSeedingService seedingService
#endif
            )
        {
            InitializeComponent();
            _backupService = backupService;
#if DEBUG
            _seedingService = seedingService;
#endif
            _viewModel = new SettingsViewModel(databaseService, backupService);
            DataContext = _viewModel;

#if !DEBUG
            DevToolsSection.Visibility = Visibility.Collapsed;
#endif
            Loaded += (_, _) => RefreshView();
        }

        public void RefreshView()
        {
            _viewModel.Refresh();
        }

        private MainWindow? GetShell()
        {
            return Window.GetWindow(this) as MainWindow;
        }

        private void ManualBackup_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                Title = "حفظ نسخة احتياطية",
                FileName = $"guarantees_manual_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db",
                Filter = "Database Files|*.db|All Files|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _backupService.CreateManualBackup(dialog.FileName);
                RefreshView();
                GetShell()?.SetStatus("تم إنشاء النسخة الاحتياطية اليدوية بنجاح.", ShellStatusTone.Success);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر إنشاء النسخة الاحتياطية.");
                GetShell()?.SetStatus("فشل إنشاء النسخة الاحتياطية اليدوية.", ShellStatusTone.Error);
            }
        }

        private void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Title = "اختيار نسخة احتياطية للاسترجاع",
                Filter = "Database Files|*.db|All Files|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            MessageBoxResult confirmation = AppDialogService.Ask(
                "سيستبدل النظام قاعدة البيانات الحالية بمحتوى النسخة المحددة، وسيُنشئ نسخة أمان قبل الاسترجاع. هل تريد المتابعة؟",
                "تأكيد الاسترجاع",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _backupService.RestoreManualBackup(dialog.FileName);
                RefreshView();
                GetShell()?.SetStatus("تم استرجاع النسخة الاحتياطية بنجاح بعد التحقق منها.", ShellStatusTone.Success);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر استرجاع النسخة الاحتياطية.");
                GetShell()?.SetStatus("فشل استرجاع النسخة الاحتياطية.", ShellStatusTone.Error);
            }
        }

        private void PortableBackup_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new()
            {
                Title = "حفظ حزمة محمولة",
                FileName = $"guarantees_portable_backup_{DateTime.Now:yyyyMMdd_HHmmss}.gmpkg",
                Filter = "Guarantee Manager Portable Package|*.gmpkg|ZIP Archive|*.zip|All Files|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            string? passphrase = PromptForPortablePassphrase(
                "حزمة محمولة",
                "أدخل عبارة مرور لحماية الحزمة المحمولة. ستحتاجها عند الاسترجاع على هذا الجهاز أو جهاز آخر.",
                "عبارة المرور",
                "استخدم 12 حرفًا على الأقل، مع حرف ورقم ورمز خاص، واحتفظ بها في مكان آمن.");

            if (string.IsNullOrWhiteSpace(passphrase))
            {
                return;
            }

            try
            {
                _backupService.CreatePortableBackupPackage(dialog.FileName, passphrase);
                RefreshView();
                GetShell()?.SetStatus("تم إنشاء الحزمة المحمولة بنجاح.", ShellStatusTone.Success);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر إنشاء الحزمة المحمولة.");
                GetShell()?.SetStatus("فشل إنشاء الحزمة المحمولة.", ShellStatusTone.Error);
            }
        }

        private void RestorePortableBackup_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Title = "اختيار حزمة محمولة للاسترجاع",
                Filter = "Guarantee Manager Portable Package|*.gmpkg|ZIP Archive|*.zip|All Files|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            string? passphrase = PromptForPortablePassphrase(
                "استرجاع حزمة محمولة",
                "أدخل عبارة المرور الخاصة بالحزمة المحمولة المحددة.",
                "عبارة المرور",
                "أدخل نفس عبارة المرور التي استُخدمت عند إنشاء الحزمة، حتى لو كانت من إصدار أقدم.");

            if (string.IsNullOrWhiteSpace(passphrase))
            {
                return;
            }

            MessageBoxResult confirmation = AppDialogService.Ask(
                "سيستبدل النظام قاعدة البيانات الحالية والمرفقات ووثائق الطلبات بمحتوى الحزمة المحمولة، وسيُنشئ حزمة أمان قبل الاسترجاع. هل تريد المتابعة؟",
                "تأكيد استرجاع الحزمة المحمولة",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _backupService.RestorePortableBackupPackage(dialog.FileName, passphrase);
                RefreshView();
                GetShell()?.SetStatus("تم استرجاع الحزمة المحمولة بنجاح.", ShellStatusTone.Success);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر استرجاع الحزمة المحمولة.");
                GetShell()?.SetStatus("فشل استرجاع الحزمة المحمولة.", ShellStatusTone.Error);
            }
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(AppPaths.DataFolder, "مجلد البيانات");
        private void OpenAttachmentsFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(AppPaths.AttachmentsFolder, "مجلد المرفقات");
        private void OpenWorkflowFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(AppPaths.WorkflowFolder, "مجلد الطلبات والردود");
        private void OpenLogsFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(AppPaths.LogsFolder, "مجلد السجلات");

        private void SeedData_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            if (_seedingService == null) return;

            MessageBoxResult confirmation = AppDialogService.Ask(
                "هذا الإجراء سيقوم بحذف كافة البيانات الحالية وتوليد 20 ضماناً جديداً مع سجلاتها التاريخية وطلباتها. هل أنت متأكد؟",
                "تأكيد توليد البيانات",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes) return;

            try
            {
                _seedingService.Seed();
                RefreshView();
                GetShell()?.SetStatus("تم توليد البيانات التجريبية بنجاح. يرجى مراجعة شاشات البرنامج.", ShellStatusTone.Success);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر توليد البيانات.");
            }
#endif
        }

        private string? PromptForPortablePassphrase(string title, string prompt, string label, string nextStepHint)
        {
            MainWindow? owner = GetShell();
            TextPromptWindow dialog = new(
                title,
                prompt,
                label,
                "اعتماد العبارة والمتابعة",
                nextStepHint: nextStepHint);

            if (owner != null)
            {
                dialog.Owner = owner;
            }

            if (dialog.ShowDialog() != true)
            {
                return null;
            }

            return dialog.ResultText;
        }

        private void OpenFolder(string folderPath, string label)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                Process.Start("explorer.exe", folderPath);
                GetShell()?.SetStatus(ExternalOpenFeedbackFormatter.BuildOpenedFolderStatusOrFallback(label, folderPath), ShellStatusTone.Info);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, $"تعذر فتح {label}.");
                GetShell()?.SetStatus($"فشل فتح {label}.", ShellStatusTone.Error);
            }
        }
    }
}
