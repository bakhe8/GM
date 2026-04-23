using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Views
{
    public partial class GuaranteeHistoryWindow : Window
    {
        private readonly IDatabaseService _dbService;
        private readonly Guarantee _selectedGuarantee;
        private readonly GuaranteeHistoryReportService _reportService = new GuaranteeHistoryReportService();
        private List<Guarantee> _history = new List<Guarantee>();
        private bool _isBusy;

        public GuaranteeHistoryWindow(Guarantee guarantee, IDatabaseService dbService)
        {
            InitializeComponent();
            ButtonIconContentFactory.Apply(BtnClose, "Icon_Geometry_Close", "إغلاق");
            _selectedGuarantee = guarantee;
            _dbService = dbService;
            WindowStateService.Restore(this, nameof(GuaranteeHistoryWindow));
            Closing += GuaranteeHistoryWindow_Closing;
            LoadHistory();
        }

        private void View_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                UpdateLayout();
                foreach (var col in GridHistory.Columns)
                {
                    var w = col.Width;
                    col.Width = 0;
                    col.Width = w;
                }
            }
        }

        private Guarantee? SelectedVersion => GridHistory.SelectedItem as Guarantee ?? _history.FirstOrDefault();

        public void RefreshHistory()
        {
            LoadHistory();
        }

        private MainWindow? GetShell()
        {
            return Application.Current.MainWindow as MainWindow;
        }

        private void LoadHistory()
        {
            try
            {
                _history = _dbService.GetGuaranteeHistory(_selectedGuarantee.Id)
                    .OrderByDescending(g => g.VersionNumber)
                    .ThenByDescending(g => g.CreatedAt)
                    .ToList();

                GridHistory.ItemsSource = _history;

                if (!_history.Any())
                {
                    TxtGuaranteeNo.Text = $"سجل الضمان رقم {_selectedGuarantee.GuaranteeNo}";
                    TxtSupplierBank.Text = $"{_selectedGuarantee.Supplier} - {_selectedGuarantee.Bank}";
                    TxtContext.Text = "لم يتم العثور على إصدارات محفوظة لهذا الضمان.";
                    TxtCurrentVersion.Text = "-";
                    TxtFirstCreated.Text = "-";
                    TxtLastUpdated.Text = "-";
                    UpdateActionButtons();
                    UpdateHistoryActionState();
                    return;
                }

                var current = _history.FirstOrDefault(g => g.IsCurrent) ?? _history.First();
                var firstCreated = _history.Min(g => g.CreatedAt);
                var lastUpdated = _history.Max(g => g.CreatedAt);

                TxtGuaranteeNo.Text = $"سجل الضمان رقم {_selectedGuarantee.GuaranteeNo}";
                TxtSupplierBank.Text = $"{current.Supplier} - {current.Bank}";
                TxtContext.Text = $"إجمالي السلسلة: {_history.Count} إصدار(ات) محفوظة. الإصدار الحالي هو v{current.VersionNumber}.";
                TxtCurrentVersion.Text = $"v{current.VersionNumber}";
                TxtFirstCreated.Text = firstCreated.ToString("yyyy-MM-dd");
                TxtLastUpdated.Text = lastUpdated.ToString("yyyy-MM-dd HH:mm");
                GridHistory.SelectedItem = current;
                UpdateActionButtons();
                UpdateHistoryActionState();
                GetShell()?.SetStatus($"تم تحميل {_history.Count} إصدارًا للضمان رقم {_selectedGuarantee.GuaranteeNo}.", ShellStatusTone.Info);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تحميل سجل الضمان.");
                GetShell()?.SetStatus("فشل تحميل سجل الضمان.", ShellStatusTone.Error);
                Close();
            }
        }

        private void UpdateActionButtons()
        {
            Guarantee? selectedVersion = SelectedVersion;
            bool hasHistory = !_isBusy && _history.Any();
            bool hasAttachments = hasHistory && selectedVersion?.Attachments.Any() == true;

            BtnClose.IsEnabled = !_isBusy;
            BtnViewAttachments.IsEnabled = hasAttachments;
            BtnExportHistory.IsEnabled = hasHistory;
            BtnPrintHistory.IsEnabled = hasHistory;
        }

        private void SetBusy(bool isBusy, string? printText = null, string? exportText = null)
        {
            _isBusy = isBusy;
            UpdateActionButtons();
            UpdateHistoryActionState();

            string? statusMessage = !string.IsNullOrWhiteSpace(exportText)
                ? exportText
                : printText;

            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                SetLocalStatus(statusMessage, ShellStatusTone.Info, mirrorToShell: true);
            }
        }

        private void UpdateHistoryActionState()
        {
            Guarantee? selectedVersion = SelectedVersion;

            if (!_history.Any())
            {
                TxtHistoryActionsTitle.Text = "لا توجد إصدارات محفوظة";
                TxtHistoryActionsSummary.Text = "هذا الضمان لا يحتوي على سجل محفوظ يمكن استعراضه أو تصديره أو طباعته.";
                TxtAttachmentsActionHint.Text = "لا توجد مرفقات يمكن فتحها قبل توفر إصدار محفوظ داخل الجدول.";
                UpdateActionButtons();
                return;
            }

            if (selectedVersion == null)
            {
                TxtHistoryActionsTitle.Text = "الإجراءات المتاحة";
                TxtHistoryActionsSummary.Text = "حدد إصدارًا من الجدول أولًا لفتح مرفقاته، بينما يبقى التصدير والطباعة متاحين لسجل الضمان كاملًا.";
                TxtAttachmentsActionHint.Text = "حدد إصدارًا من الجدول لفتح مرفقاته المرتبطة به فقط.";
                UpdateActionButtons();
                return;
            }

            bool hasAttachments = selectedVersion.Attachments.Any();

            TxtHistoryActionsTitle.Text = $"الإجراءات المتاحة للإصدار v{selectedVersion.VersionNumber}";
            TxtHistoryActionsSummary.Text = $"{selectedVersion.GuaranteeType} | ينتهي في {selectedVersion.ExpiryDate:yyyy-MM-dd} | عدد المرفقات: {selectedVersion.Attachments.Count}";
            TxtAttachmentsActionHint.Text = hasAttachments
                ? "افتح الملفات المرافقة لهذا الإصدار فقط من دون مغادرة شاشة السجل."
                : "لا توجد مرفقات محفوظة لهذا الإصدار، لذلك سيبقى زر الفتح غير متاح.";
            UpdateActionButtons();
        }

        private void ViewSelectedVersionFiles_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedVersion = SelectedVersion;
            if (selectedVersion == null || !selectedVersion.Attachments.Any())
            {
                AppDialogService.ShowWarning("لا توجد مرفقات مرتبطة بالإصدار المحدد.");
                return;
            }

            OpenVersionAttachments(selectedVersion);
        }

        private void OpenVersionAttachments(Guarantee guarantee)
        {
            MainWindow? shell = GetShell();
            if (shell == null)
            {
                return;
            }

            var title = $"مرفقات الإصدار v{guarantee.VersionNumber}";
            shell.OpenAttachmentWindow(
                $"attachments:readonly:{guarantee.Id}",
                () =>
                {
                    var window = new AttachmentListWindow(guarantee.Attachments, _dbService, allowDelete: false, headerText: title);
                    window.Owner = shell;
                    return window;
                },
                $"تم فتح مرفقات الإصدار v{guarantee.VersionNumber}.",
                $"تم تنشيط نافذة مرفقات الإصدار v{guarantee.VersionNumber}.",
                existing =>
                {
                    if (existing is AttachmentListWindow attachmentWindow)
                    {
                        attachmentWindow.RefreshAttachments(guarantee.Attachments, title);
                    }
                });
        }

        private void GridHistoryRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridRow row)
            {
                return;
            }

            GridHistory.SelectedItem = row.Item;
            row.IsSelected = true;
            row.Focus();
        }

        private void GridHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateHistoryActionState();
        }

        private void GuaranteeHistoryWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            WindowStateService.Save(this, nameof(GuaranteeHistoryWindow));
        }

        private void ExportHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy || !_history.Any())
            {
                return;
            }

            try
            {
                SetBusy(true, exportText: "جارٍ التصدير...");
                bool exported = _reportService.ExportHistoryToExcel(_selectedGuarantee, _history);
                if (!exported)
                {
                    SetLocalStatus($"تم إلغاء تصدير سجل الضمان رقم {_selectedGuarantee.GuaranteeNo}.", ShellStatusTone.Info, mirrorToShell: true);
                    return;
                }

                string? outputPath = _reportService.LastOutputPath;
                SetLocalStatus(OutputFeedbackFormatter.BuildSavedFileStatusOrFallback($"تم تصدير سجل الضمان رقم {_selectedGuarantee.GuaranteeNo} إلى Excel", outputPath), ShellStatusTone.Success, mirrorToShell: true);
                AppDialogService.ShowSuccess(OutputFeedbackFormatter.BuildSavedFileSuccessMessageOrFallback($"تم تصدير سجل الضمان رقم {_selectedGuarantee.GuaranteeNo} إلى Excel بنجاح.", outputPath));
            }
            catch (Exception ex)
            {
                SetLocalStatus("فشل تصدير سجل الضمان.", ShellStatusTone.Error, mirrorToShell: true);
                AppDialogService.ShowError(ex, "تعذر تصدير سجل الضمان إلى Excel.");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy || !_history.Any())
            {
                return;
            }

            try
            {
                SetBusy(true, printText: "جارٍ تجهيز الطباعة...");
                bool printed = _reportService.PrintHistory(_selectedGuarantee, _history);
                if (!printed)
                {
                    SetLocalStatus($"تم إلغاء طباعة سجل الضمان رقم {_selectedGuarantee.GuaranteeNo}.", ShellStatusTone.Info, mirrorToShell: true);
                    return;
                }

                SetLocalStatus($"تم إرسال سجل الضمان رقم {_selectedGuarantee.GuaranteeNo} إلى الطباعة.", ShellStatusTone.Success, mirrorToShell: true);
            }
            catch (Exception ex)
            {
                SetLocalStatus("فشلت طباعة سجل الضمان.", ShellStatusTone.Error, mirrorToShell: true);
                AppDialogService.ShowError(ex, "تعذر طباعة سجل الضمان.");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetLocalStatus(string message, ShellStatusTone tone = ShellStatusTone.Info, bool mirrorToShell = false)
        {
            WindowLocalFeedbackPresenter.Show(
                LocalStatusBorder,
                TxtLocalStatus,
                message,
                tone,
                mirrorToShell ? static (statusMessage, statusTone) => ((MainWindow?)Application.Current.MainWindow)?.SetStatus(statusMessage, statusTone) : null);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
