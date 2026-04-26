using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Views
{
    public partial class InquiryResultWindow : Window
    {
        private readonly OperationalInquiryResult _result;
        private readonly IDatabaseService _databaseService;
        private readonly IWorkflowService _workflowService;
        private readonly IExcelService _excelService;
        private readonly GuaranteeHistoryReportService _historyReportService = new GuaranteeHistoryReportService();

        public InquiryResultWindow(
            OperationalInquiryResult result,
            IDatabaseService databaseService,
            IWorkflowService workflowService,
            IExcelService excelService)
        {
            InitializeComponent();
            ButtonIconContentFactory.Apply(BtnClose, "Icon_Geometry_Close", "إغلاق");
            _result = result;
            _databaseService = databaseService;
            _workflowService = workflowService;
            _excelService = excelService;
            WindowStateService.Restore(this, nameof(InquiryResultWindow));
            Closing += InquiryResultWindow_Closing;
            LoadResult();
        }

        private void View_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                UpdateLayout();
                foreach (var col in GridTimeline.Columns)
                {
                    var w = col.Width;
                    col.Width = 0;
                    col.Width = w;
                }
            }
        }

        private MainWindow? GetShell()
        {
            return Application.Current.MainWindow as MainWindow;
        }

        private Guarantee? EffectiveGuarantee => _result.CurrentGuarantee ?? _result.SelectedGuarantee;

        private void LoadResult()
        {
            TxtTitle.Text = _result.Title;
            TxtSubject.Text = _result.Subject;
            TxtEventDate.Text = $"تاريخ الحدث الأخير: {_result.EventDateLabel}";
            TxtAnswer.Text = _result.Answer;
            TxtExplanation.Text = _result.Explanation;
            FactsList.ItemsSource = _result.Facts;
            GridTimeline.ItemsSource = _result.Timeline;
            TxtLetterEvidence.Text = _result.CanOpenRequestLetter ? "موجود ومتاح للفتح" : "غير متاح";
            TxtResponseEvidence.Text = _result.CanOpenResponseDocument ? "موجود ومتاح للفتح" : "غير متاح";
            TxtGuaranteeReportEvidence.Text = EffectiveGuarantee != null ? "متاح للتصدير من هذه النافذة" : "غير متاح";
            TxtHistoryEvidence.Text = EffectiveGuarantee != null ? "متاح للتصدير وفتح السجل" : "غير متاح";
            UpdateInquiryActionPanel();
        }

        private void UpdateInquiryActionPanel()
        {
            TxtInquiryActionsTitle.Text = "ما الذي تريد فعله بعد هذا الجواب؟";
            TxtInquiryActionsSummary.Text = "الجواب موضح أعلاه، ومن هنا يمكنك فتح الملفات الداعمة المرتبطة أو استخراج التقارير المساندة.";

            ContextActionPanelFactory.Populate(
                InquiryActionPanel,
                BuildInquiryActionSections(),
                ResolveInquiryActionHandler,
                true,
                "inquiry.export-guarantee",
                "inquiry.open-history");
        }

        private IReadOnlyList<ContextActionSection> BuildInquiryActionSections()
        {
            var sections = new System.Collections.Generic.List<ContextActionSection>();

            var openActions = new System.Collections.Generic.List<ContextActionDefinition>();
            if (_result.CanOpenHistory)
            {
                openActions.Add(ContextActionDefinition.Action("inquiry.open-history", "افتح ملف الضمان", ContextActionResultKind.ManagedReferenceWindow, "يفتح ملف الضمان عند القسم الأنسب لهذا الجواب."));
            }

            if (_result.CanOpenRequestLetter)
            {
                openActions.Add(ContextActionDefinition.Action("inquiry.open-letter", "فتح خطاب الطلب", ContextActionResultKind.ExternalDocument, "يفتح خطاب الطلب المرتبط بهذا الجواب."));
            }

            if (_result.CanOpenResponseDocument)
            {
                openActions.Add(ContextActionDefinition.Action("inquiry.open-response", "فتح مستند رد البنك", ContextActionResultKind.ExternalDocument, "يفتح مستند رد البنك المرتبط بهذا الجواب."));
            }

            if (openActions.Count > 0)
            {
                sections.Add(new ContextActionSection("افتح", "افتح سجل الضمان أو الملفات الداعمة التي يستند إليها هذا الجواب.", openActions.ToArray()));
            }

            var exportActions = new System.Collections.Generic.List<ContextActionDefinition>();
            if (EffectiveGuarantee != null)
            {
                exportActions.Add(ContextActionDefinition.Action("inquiry.export-guarantee", "تصدير تقرير الضمان", ContextActionResultKind.Export, "يصدر تقرير الضمان المرتبط بهذا الجواب."));
                exportActions.Add(ContextActionDefinition.Action("inquiry.export-history", "تصدير تاريخ الضمان", ContextActionResultKind.Export, "يصدر تاريخ الضمان المرتبط بهذا الجواب."));
            }

            if (exportActions.Count > 0)
            {
                sections.Add(new ContextActionSection("صدّر", "استخرج تقارير أو ملفات داعمة لهذا الجواب.", exportActions.ToArray()));
            }

            return sections;
        }

        private RoutedEventHandler? ResolveInquiryActionHandler(string actionId)
        {
            return actionId switch
            {
                "inquiry.open-history" => OpenHistory_Click,
                "inquiry.open-letter" => OpenLetter_Click,
                "inquiry.open-response" => OpenResponse_Click,
                "inquiry.export-guarantee" => ExportGuaranteeReport_Click,
                "inquiry.export-history" => ExportHistoryReport_Click,
                _ => null
            };
        }

        private void InquiryResultWindow_Closing(object? sender, CancelEventArgs e)
        {
            WindowStateService.Save(this, nameof(InquiryResultWindow));
        }

        private void OpenHistory_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = EffectiveGuarantee;
            if (guarantee == null)
            {
                return;
            }

            MainWindow? shell = GetShell();
            if (shell == null)
            {
                return;
            }

            if (InquiryFileRoutingResolver.TryResolve(_result, out GuaranteeFileFocusArea focusArea, out int? requestIdToFocus))
            {
                shell.ShowGuaranteeFile(guarantee, "نتيجة الاستعلام", focusArea: focusArea, requestIdToFocus: requestIdToFocus);
                return;
            }

            shell.ShowGuaranteeFile(guarantee, "نتيجة الاستعلام", focusArea: GuaranteeFileFocusArea.Series);
        }


        private void ExportGuaranteeReport_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = EffectiveGuarantee;
            if (guarantee == null)
            {
                return;
            }

            try
            {
                bool exported = _excelService.ExportSingleGuaranteeReport(guarantee);
                if (!exported)
                {
                    SetLocalStatus($"تم إلغاء تصدير تقرير الضمان رقم {guarantee.GuaranteeNo}.", ShellStatusTone.Info, mirrorToShell: true);
                    return;
                }

                string? outputPath = _excelService.LastOutputPath;
                SetLocalStatus(OutputFeedbackFormatter.BuildSavedFileStatusOrFallback($"تم تصدير تقرير الضمان رقم {guarantee.GuaranteeNo}", outputPath), ShellStatusTone.Success, mirrorToShell: true);
                AppDialogService.ShowSuccess(OutputFeedbackFormatter.BuildSavedFileSuccessMessageOrFallback($"تم تصدير تقرير الضمان رقم {guarantee.GuaranteeNo} بنجاح.", outputPath));
            }
            catch (Exception ex)
            {
                SetLocalStatus("تعذر تصدير تقرير الضمان.", ShellStatusTone.Error, mirrorToShell: true);
                AppDialogService.ShowError(ex, "تعذر تصدير تقرير الضمان.");
            }
        }

        private void ExportHistoryReport_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = EffectiveGuarantee;
            if (guarantee == null)
            {
                return;
            }

            try
            {
                var history = _databaseService.GetGuaranteeHistory(guarantee.Id)
                    .OrderByDescending(g => g.VersionNumber)
                    .ThenByDescending(g => g.CreatedAt)
                    .ToList();

                bool exported = _historyReportService.ExportHistoryToExcel(guarantee, history);
                if (!exported)
                {
                    SetLocalStatus($"تم إلغاء تصدير تاريخ الضمان رقم {guarantee.GuaranteeNo}.", ShellStatusTone.Info, mirrorToShell: true);
                    return;
                }

                string? outputPath = _historyReportService.LastOutputPath;
                SetLocalStatus(OutputFeedbackFormatter.BuildSavedFileStatusOrFallback($"تم تصدير تاريخ الضمان رقم {guarantee.GuaranteeNo}", outputPath), ShellStatusTone.Success, mirrorToShell: true);
                AppDialogService.ShowSuccess(OutputFeedbackFormatter.BuildSavedFileSuccessMessageOrFallback($"تم تصدير تاريخ الضمان رقم {guarantee.GuaranteeNo} بنجاح.", outputPath));
            }
            catch (Exception ex)
            {
                SetLocalStatus("تعذر تصدير تاريخ الضمان.", ShellStatusTone.Error, mirrorToShell: true);
                AppDialogService.ShowError(ex, "تعذر تصدير تاريخ الضمان.");
            }
        }

        private void OpenLetter_Click(object sender, RoutedEventArgs e)
        {
            if (_result.RelatedRequest == null)
            {
                return;
            }

            try
            {
                _workflowService.OpenRequestLetter(_result.RelatedRequest);
                SetLocalStatus(ExternalOpenFeedbackFormatter.BuildOpenedRequestLetterStatus(_result.RelatedRequest), ShellStatusTone.Info, mirrorToShell: true);
            }
            catch (Exception ex)
            {
                SetLocalStatus("تعذر فتح خطاب الطلب.", ShellStatusTone.Error, mirrorToShell: true);
                AppDialogService.ShowError(ex, "تعذر فتح خطاب الطلب.");
            }
        }

        private void OpenResponse_Click(object sender, RoutedEventArgs e)
        {
            if (_result.RelatedRequest == null)
            {
                return;
            }

            try
            {
                _workflowService.OpenResponseDocument(_result.RelatedRequest);
                SetLocalStatus(ExternalOpenFeedbackFormatter.BuildOpenedResponseDocumentStatus(_result.RelatedRequest), ShellStatusTone.Info, mirrorToShell: true);
            }
            catch (Exception ex)
            {
                SetLocalStatus("تعذر فتح مستند رد البنك.", ShellStatusTone.Error, mirrorToShell: true);
                AppDialogService.ShowError(ex, "تعذر فتح مستند رد البنك.");
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

        private void GridTimelineRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridRow row)
            {
                return;
            }

            GridTimeline.SelectedItem = row.Item;
            row.IsSelected = true;
            row.Focus();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
