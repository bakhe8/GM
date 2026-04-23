using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Views
{
    public partial class GuaranteeSideSheetView : UserControl
    {
        private readonly Guarantee _guarantee;
        private readonly List<Guarantee> _history;
        private readonly List<WorkflowRequest> _requests;
        private readonly IDatabaseService _databaseService;
        private readonly IExcelService _excelService;

        public GuaranteeSideSheetView(
            Guarantee guarantee,
            IEnumerable<Guarantee> history,
            IEnumerable<WorkflowRequest> requests,
            IDatabaseService databaseService,
            IExcelService excelService)
        {
            InitializeComponent();
            _guarantee = guarantee;
            _history = history.OrderByDescending(item => item.CreatedAt).ToList();
            _requests = requests.OrderByDescending(item => item.RequestDate).ToList();
            _databaseService = databaseService;
            _excelService = excelService;
            LoadView();
        }

        private void LoadView()
        {
            TxtGuaranteeNo.Text = _guarantee.GuaranteeNo;
            TxtGuaranteeSummary.Text = $"{_guarantee.Supplier} | {_guarantee.Bank}";
            TxtStatus.Text = _guarantee.StatusLabel;
            TxtAmount.Text = _guarantee.Amount.ToString("N2");
            TxtExpiry.Text = _guarantee.ExpiryDate.ToString("yyyy-MM-dd");

            var attachments = _guarantee.Attachments
                .OrderByDescending(item => item.UploadedAt)
                .Select(item => new AttachmentPreviewItem(item))
                .ToList();

            AttachmentsItems.ItemsSource = attachments;
            bool hasAttachments = attachments.Count > 0;
            AttachmentsItems.Visibility = hasAttachments ? Visibility.Visible : Visibility.Collapsed;
            AttachmentEmptyState.Visibility = hasAttachments ? Visibility.Collapsed : Visibility.Visible;

            List<TimelinePreviewItem> timeline = BuildTimeline();
            TimelineItems.ItemsSource = timeline;
            bool hasTimeline = timeline.Count > 0;
            TimelineItems.Visibility = hasTimeline ? Visibility.Visible : Visibility.Collapsed;
            TimelineEmptyState.Visibility = hasTimeline ? Visibility.Collapsed : Visibility.Visible;
        }

        private List<TimelinePreviewItem> BuildTimeline()
        {
            List<TimelinePreviewItem> entries = new();

            foreach (Guarantee version in _history)
            {
                string description = $"{version.VersionLabel} | {version.StatusLabel} | مرفقات: {version.AttachmentCount}";
                entries.Add(new TimelinePreviewItem(version.CreatedAt, $"إصدار {version.VersionLabel}", description));
            }

            foreach (WorkflowRequest request in _requests)
            {
                string description = $"{request.TypeLabel} | {request.StatusLabel}";
                if (request.HasLetter || request.HasResponseDocument)
                {
                    description += $" | خطاب: {(request.HasLetter ? "نعم" : "لا")} | رد: {(request.HasResponseDocument ? "نعم" : "لا")}";
                }

                entries.Add(new TimelinePreviewItem(request.RequestDate, $"طلب {request.SequenceNumber}", description));
            }

            return entries
                .OrderByDescending(item => item.SortKey)
                .Take(6)
                .ToList();
        }

        private MainWindow? GetShell()
        {
            return Window.GetWindow(this) as MainWindow;
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            GetShell()?.ShowGuaranteeFile(_guarantee, "المعاينة الجانبية", true);
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            bool exported = _excelService.ExportSingleGuaranteeReport(_guarantee);
            GetShell()?.SetStatus(
                exported
                    ? $"تم تصدير تقرير الضمان رقم {_guarantee.GuaranteeNo}."
                    : $"تم إلغاء تصدير تقرير الضمان رقم {_guarantee.GuaranteeNo}.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? refreshed = _databaseService.GetGuaranteeById(_guarantee.Id);
            if (refreshed == null)
            {
                GetShell()?.SetStatus("تعذر تحديث بيانات الضمان المحدد.", ShellStatusTone.Warning);
                return;
            }

            GetShell()?.ShowGuaranteeFile(refreshed, "المعاينة الجانبية", true);
        }

        private void AttachmentOpen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string filePath)
            {
                return;
            }

            try
            {
                if (!File.Exists(filePath))
                {
                    AppDialogService.ShowWarning("الملف المحدد غير موجود.");
                    return;
                }

                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                GetShell()?.SetStatus(
                    ExternalOpenFeedbackFormatter.BuildOpenedFileStatusOrFallback("المرفق", filePath),
                    ShellStatusTone.Info);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر فتح المرفق.");
                GetShell()?.SetStatus("فشل فتح المرفق.", ShellStatusTone.Error);
            }
        }

        private sealed class TimelinePreviewItem
        {
            public TimelinePreviewItem(DateTime sortKey, string title, string description)
            {
                SortKey = sortKey;
                Title = title;
                Description = description;
                Stamp = sortKey.ToString("yyyy-MM-dd HH:mm");
            }

            public DateTime SortKey { get; }
            public string Title { get; }
            public string Description { get; }
            public string Stamp { get; }
        }

        private sealed class AttachmentPreviewItem
        {
            public AttachmentPreviewItem(AttachmentRecord source)
            {
                OriginalFileName = source.OriginalFileName;
                FilePath = source.FilePath;
                UploadedAtLabel = source.UploadedAt.ToString("yyyy-MM-dd HH:mm");
            }

            public string OriginalFileName { get; }
            public string FilePath { get; }
            public string UploadedAtLabel { get; }
        }
    }
}
