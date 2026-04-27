using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using GuaranteeManager.Utils;
#if DEBUG
using GuaranteeManager.Development;
#endif
using Microsoft.Win32;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed class SettingsWorkspaceCoordinator
    {
        private readonly Services.BackupService _backupService;
        private readonly Services.IUiDiagnosticsService _diagnostics;
        private readonly Services.IShellStatusService _shellStatus;
#if DEBUG
        private readonly DataSeedingService? _seedingService;
#endif

        public SettingsWorkspaceCoordinator()
        {
            _backupService = App.CurrentApp.GetRequiredService<Services.BackupService>();
            _diagnostics = App.CurrentApp.GetRequiredService<Services.IUiDiagnosticsService>();
            _shellStatus = App.CurrentApp.GetRequiredService<Services.IShellStatusService>();
#if DEBUG
            _seedingService = App.CurrentApp.GetRequiredService<DataSeedingService>();
#endif
        }

        public void OpenPath(SettingPathItem? item)
        {
            if (item == null)
            {
                return;
            }

            string targetPath = Directory.Exists(item.OpenPath)
                ? item.OpenPath
                : Path.GetDirectoryName(item.OpenPath) ?? AppPaths.StorageRootDirectory;

            if (!Directory.Exists(targetPath))
            {
                MessageBox.Show("المجلد غير موجود.", "الإعدادات", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(targetPath) { UseShellExecute = true });
                _diagnostics.RecordEvent("settings.operation", "open-path", new { item.Label, targetPath });
                _shellStatus.ShowInfo("تم فتح المسار.", $"الإعدادات • {item.Label}");
            }
            catch (Exception ex)
            {
                _diagnostics.RecordEvent("settings.operation", "open-path-failed", new { item.Label, targetPath, ex.Message });
                MessageBox.Show(ex.Message, "الإعدادات", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void CopyPath(SettingPathItem? item)
        {
            if (item == null)
            {
                return;
            }

            try
            {
                Clipboard.SetText(item.Path);
                _diagnostics.RecordEvent("settings.operation", "copy-path", new { item.Label, item.Path });
                _shellStatus.ShowSuccess("تم نسخ المسار.", $"الإعدادات • {item.Label}");
            }
            catch (Exception ex)
            {
                _diagnostics.RecordEvent("settings.operation", "copy-path-failed", new { item.Label, item.Path, ex.Message });
                MessageBox.Show(ex.Message, "الإعدادات", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void CreateManualBackup()
        {
            SaveFileDialog dialog = new()
            {
                Title = "حفظ نسخة احتياطية",
                FileName = $"guarantees_manual_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db",
                Filter = "Database Files|*.db|All Files|*.*"
            };

            if (dialog.ShowDialog(Application.Current.MainWindow) != true)
            {
                return;
            }

            try
            {
                _backupService.CreateManualBackup(dialog.FileName);
                string output = _backupService.LastManualBackupPath ?? dialog.FileName;
                _diagnostics.RecordEvent("settings.operation", "manual-backup-created", new { OutputPath = output });
                _shellStatus.ShowSuccess("تم إنشاء النسخة الاحتياطية.", $"الإعدادات • {Path.GetFileName(output)}");
            }
            catch (Exception ex)
            {
                _diagnostics.RecordEvent("settings.operation", "manual-backup-failed", new { TargetPath = dialog.FileName, ex.Message });
                MessageBox.Show(ex.Message, "النسخ الاحتياطي", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void RestoreManualBackup(Action refresh)
        {
            OpenFileDialog dialog = new()
            {
                Title = "اختيار نسخة احتياطية للاسترجاع",
                Filter = "Database Files|*.db|All Files|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(Application.Current.MainWindow) != true)
            {
                return;
            }

            if (MessageBox.Show(
                    "سيتم استبدال قاعدة البيانات الحالية وإنشاء نسخة أمان قبل الاسترجاع. هل تريد المتابعة؟",
                    "تأكيد الاسترجاع",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _backupService.RestoreManualBackup(dialog.FileName);
                refresh();
                string safety = string.IsNullOrWhiteSpace(_backupService.LastPreRestoreSafetyBackupPath)
                    ? "لم يلزم إنشاء نسخة أمان."
                    : $"نسخة الأمان:\n{_backupService.LastPreRestoreSafetyBackupPath}";
                _diagnostics.RecordEvent("settings.operation", "manual-backup-restored", new
                {
                    SourcePath = _backupService.LastRestoreSourcePath ?? dialog.FileName,
                    SafetyPath = _backupService.LastPreRestoreSafetyBackupPath ?? string.Empty
                });
                MessageBox.Show(
                    $"تم استرجاع النسخة الاحتياطية بنجاح.\n\nالمصدر:\n{_backupService.LastRestoreSourcePath ?? dialog.FileName}\n\n{safety}",
                    "استرجاع نسخة احتياطية",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _diagnostics.RecordEvent("settings.operation", "manual-backup-restore-failed", new { SourcePath = dialog.FileName, ex.Message });
                MessageBox.Show(ex.Message, "استرجاع نسخة احتياطية", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void CreatePortableBackup()
        {
            SaveFileDialog dialog = new()
            {
                Title = "حفظ حزمة محمولة",
                FileName = $"guarantees_portable_backup_{DateTime.Now:yyyyMMdd_HHmmss}.gmpkg",
                Filter = "Guarantee Manager Portable Package|*.gmpkg|ZIP Archive|*.zip|All Files|*.*"
            };

            if (dialog.ShowDialog(Application.Current.MainWindow) != true)
            {
                return;
            }

            if (!GuidedTextPromptDialog.TryShow(
                    "حزمة محمولة",
                    "أدخل عبارة مرور لحماية الحزمة المحمولة. ستحتاجها عند الاسترجاع على هذا الجهاز أو جهاز آخر.",
                    "عبارة المرور",
                    "اعتماد العبارة",
                    string.Empty,
                    out string passphrase,
                    "استخدم 12 حرفًا على الأقل، مع حرف ورقم ورمز خاص، واحتفظ بها في مكان آمن."))
            {
                return;
            }

            try
            {
                _backupService.CreatePortableBackupPackage(dialog.FileName, passphrase);
                string output = _backupService.LastPortableBackupPackagePath ?? dialog.FileName;
                _diagnostics.RecordEvent("settings.operation", "portable-backup-created", new { OutputPath = output });
                _shellStatus.ShowSuccess("تم إنشاء الحزمة المحمولة.", $"الإعدادات • {Path.GetFileName(output)}");
            }
            catch (Exception ex)
            {
                _diagnostics.RecordEvent("settings.operation", "portable-backup-failed", new { TargetPath = dialog.FileName, ex.Message });
                MessageBox.Show(ex.Message, "حزمة محمولة", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void RestorePortableBackup(Action refresh)
        {
            OpenFileDialog dialog = new()
            {
                Title = "اختيار حزمة محمولة للاسترجاع",
                Filter = "Guarantee Manager Portable Package|*.gmpkg|ZIP Archive|*.zip|All Files|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(Application.Current.MainWindow) != true)
            {
                return;
            }

            if (!GuidedTextPromptDialog.TryShow(
                    "استرجاع حزمة محمولة",
                    "أدخل عبارة المرور الخاصة بالحزمة المحمولة المحددة.",
                    "عبارة المرور",
                    "متابعة الاسترجاع",
                    string.Empty,
                    out string passphrase,
                    "أدخل نفس عبارة المرور التي استُخدمت عند إنشاء الحزمة، حتى لو كانت من إصدار أقدم."))
            {
                return;
            }

            if (MessageBox.Show(
                    "سيستبدل النظام قاعدة البيانات الحالية والمرفقات ووثائق الطلبات بمحتوى الحزمة، وسيُنشئ حزمة أمان قبل الاسترجاع. هل تريد المتابعة؟",
                    "تأكيد استرجاع الحزمة المحمولة",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _backupService.RestorePortableBackupPackage(dialog.FileName, passphrase);
                refresh();
                string safety = string.IsNullOrWhiteSpace(_backupService.LastPortableRestoreSafetyPackagePath)
                    ? "لم يلزم إنشاء حزمة أمان."
                    : $"حزمة الأمان:\n{_backupService.LastPortableRestoreSafetyPackagePath}";
                _diagnostics.RecordEvent("settings.operation", "portable-backup-restored", new
                {
                    SourcePath = _backupService.LastPortableRestorePackagePath ?? dialog.FileName,
                    SafetyPath = _backupService.LastPortableRestoreSafetyPackagePath ?? string.Empty
                });
                MessageBox.Show(
                    $"تم استرجاع الحزمة المحمولة بنجاح.\n\nالمصدر:\n{_backupService.LastPortableRestorePackagePath ?? dialog.FileName}\n\n{safety}",
                    "استرجاع حزمة محمولة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _diagnostics.RecordEvent("settings.operation", "portable-backup-restore-failed", new { SourcePath = dialog.FileName, ex.Message });
                MessageBox.Show(ex.Message, "استرجاع حزمة محمولة", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void CopyOperationalPathsSummary()
        {
            try
            {
                string summary = OperationalPathsSummaryFormatter.Build(
                    AppPaths.StorageRootDirectory,
                    AppPaths.BaseDirectory,
                    AppPaths.DatabasePath,
                    AppPaths.AttachmentsFolder,
                    AppPaths.WorkflowFolder,
                    AppPaths.LogsFolder);
                Clipboard.SetText(summary);
                _diagnostics.RecordEvent("settings.operation", "copy-operational-summary", new { Length = summary.Length });
                _shellStatus.ShowSuccess("تم نسخ ملخص مسارات التشغيل.", "الإعدادات • الحافظة جاهزة");
            }
            catch (Exception ex)
            {
                _diagnostics.RecordEvent("settings.operation", "copy-operational-summary-failed", new { ex.Message });
                MessageBox.Show(ex.Message, "الإعدادات", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void SeedDevelopmentData(Action refresh)
        {
#if DEBUG
            if (_seedingService == null)
            {
                return;
            }

            if (MessageBox.Show(
                    "هذا الإجراء سيحذف البيانات الحالية ويولّد بيانات تجريبية جديدة. هل تريد المتابعة؟",
                    "تأكيد توليد البيانات",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _seedingService.Seed();
                refresh();
                _diagnostics.RecordEvent("settings.operation", "seed-development-data", new { Status = "Succeeded" });
                _shellStatus.ShowSuccess("تم توليد البيانات التجريبية.", "الإعدادات • بيئة التطوير");
            }
            catch (Exception ex)
            {
                _diagnostics.RecordEvent("settings.operation", "seed-development-data-failed", new { ex.Message });
                MessageBox.Show(ex.Message, "بيئة التطوير", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
#endif
        }
    }
}
