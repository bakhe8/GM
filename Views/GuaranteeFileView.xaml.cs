using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GuaranteeManager.Contracts;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Views
{
    public partial class GuaranteeFileView : UserControl, IGuaranteeFileWorkspace
    {
        private readonly IDatabaseService _databaseService;
        private readonly IWorkflowService _workflowService;
        private readonly IExcelService _excelService;
        private readonly IOperationalInquiryService _inquiryService;
        private readonly IContextActionService _contextActionService;
        private Guarantee? _currentGuarantee;
        private List<WorkflowRequestListItem> _requestItems = new();
        private int? _requestIdToFocus;

        public GuaranteeFileView(
            IDatabaseService databaseService,
            IWorkflowService workflowService,
            IExcelService excelService,
            IOperationalInquiryService inquiryService,
            IContextActionService contextActionService)
        {
            InitializeComponent();
            _databaseService = databaseService;
            _workflowService = workflowService;
            _excelService = excelService;
            _inquiryService = inquiryService;
            _contextActionService = contextActionService;
            UpdateRequestActionState();
        }

        private void View_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    GridRelatedRequests.UpdateLayout();
                    foreach (var col in GridRelatedRequests.Columns)
                    {
                        var w = col.Width;
                        col.Width = 0;
                        col.Width = w;
                    }
                }), DispatcherPriority.Loaded);
            }
        }

        public void SetRequestFocus(int? requestId)
        {
            _requestIdToFocus = requestId;
        }

        public void LoadGuarantee(Guarantee guarantee, bool userInitiated = false)
        {
            _currentGuarantee = _databaseService.GetGuaranteeById(guarantee.Id) ?? guarantee;
            LoadView();

            if (userInitiated)
            {
                GetShell()?.SetStatus($"تم تحديث ملف الضمان رقم {_currentGuarantee.GuaranteeNo}.", ShellStatusTone.Info);
            }
        }

        public void RefreshView()
        {
            if (_currentGuarantee != null)
            {
                LoadGuarantee(_currentGuarantee, userInitiated: true);
            }
        }

        public void FocusSection(GuaranteeFileFocusArea area)
        {
            FrameworkElement? target = area switch
            {
                GuaranteeFileFocusArea.ExecutiveSummary => ExecutiveSummaryAnchor,
                GuaranteeFileFocusArea.Series => TimelineAnchor,
                GuaranteeFileFocusArea.Actions => ActionsAnchor,
                GuaranteeFileFocusArea.Attachments => AttachmentsAnchor,
                GuaranteeFileFocusArea.Outputs => ActionsAnchor,
                _ => null
            };

            if (target == null)
            {
                return;
            }

            Dispatcher.BeginInvoke(() => target.BringIntoView(), DispatcherPriority.Background);
        }

        private MainWindow? GetShell()
        {
            return Window.GetWindow(this) as MainWindow;
        }

        private WorkflowRequestListItem? SelectedRequest => GridRelatedRequests.SelectedItem as WorkflowRequestListItem;

        private void LoadView()
        {
            if (_currentGuarantee == null)
            {
                return;
            }

            TxtGuaranteeTitle.Text = $"ملف الضمان {_currentGuarantee.GuaranteeNo}";
            string referenceSummary = string.IsNullOrWhiteSpace(_currentGuarantee.ReferenceNumber)
                ? _currentGuarantee.ReferenceTypeLabel
                : $"{_currentGuarantee.ReferenceTypeLabel} { _currentGuarantee.ReferenceNumber }";
            TxtGuaranteeSubtitle.Text = $"{_currentGuarantee.Supplier} | {_currentGuarantee.Bank} | {referenceSummary}";
            TxtGuaranteeAmount.Text = _currentGuarantee.Amount.ToString("N2");
            TxtGuaranteeExpiry.Text = _currentGuarantee.ExpiryDate.ToString("yyyy-MM-dd");

            List<Guarantee> history = _databaseService.GetGuaranteeHistory(_currentGuarantee.Id)
                .OrderByDescending(item => item.CreatedAt)
                .ToList();

            int rootId = _currentGuarantee.RootId ?? _currentGuarantee.Id;
            _requestItems = _databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RootGuaranteeId = rootId,
                SortMode = WorkflowRequestQuerySortMode.RequestDateDescending
            });

            TxtGuaranteeVersionCount.Text = history.Count.ToString();
            TxtCurrentType.Text = _currentGuarantee.GuaranteeType;
            TxtCurrentStatus.Text = _currentGuarantee.StatusLabel;
            TxtLifecycleStatus.Text = _currentGuarantee.LifecycleStatusLabel;

            List<TimelineRow> timelineRows = BuildTimeline(history, _requestItems);
            TimelineItems.ItemsSource = timelineRows;
            TimelineScrollHost.Visibility = timelineRows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            TimelineEmptyStatePanel.Visibility = timelineRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            GridRelatedRequests.ItemsSource = _requestItems;

            List<AttachmentRow> attachmentRows = _currentGuarantee.Attachments
                .OrderByDescending(item => item.UploadedAt)
                .Select(item => new AttachmentRow(item))
                .ToList();

            AttachmentItems.ItemsSource = attachmentRows;
            TxtAttachmentCount.Text = $"{attachmentRows.Count} مرفق";
            AttachmentEmptyStatePanel.Visibility = attachmentRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            AttachmentScrollHost.Visibility = attachmentRows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            WorkflowRequestListItem? selection = _requestItems.FirstOrDefault(item => item.Request.Id == _requestIdToFocus) ?? _requestItems.FirstOrDefault();
            GridRelatedRequests.SelectedItem = selection;
            UpdateRequestActionState();
            UpdateEditSection();
            UpdateExtensionSection();
            UpdateReductionSection();
            UpdateReleaseSection();
            UpdateLiquidationSection();
            UpdateVerificationSection();
            UpdateReplacementSection();
            UpdateAnnulmentSection();
            UpdateInquirySection();
            UpdateActionHubVisibility();
        }

        private void UpdateInquirySection()
        {
            if (_currentGuarantee == null)
            {
                InquiryAnchor.Visibility = Visibility.Collapsed;
                InquiryActionPanel.Children.Clear();
                return;
            }

            IReadOnlyList<ContextActionSection> inquirySections = GuaranteeInquiryActionSupport.BuildSections(_contextActionService);
            if (inquirySections.Count == 0)
            {
                InquiryAnchor.Visibility = Visibility.Collapsed;
                InquiryActionPanel.Children.Clear();
                return;
            }

            InquiryAnchor.Visibility = Visibility.Visible;
            TxtInquiryHint.Text = "يمكنك طرح أسئلة مباشرة على هذا الضمان أو على البنك والمورد المرتبطين به، مع فتح نافذة جواب مدعومة بالأدلة.";

            ContextActionPanelFactory.Populate(
                InquiryActionPanel,
                inquirySections,
                ResolveInquiryActionHandler,
                true,
                ResolveInquiryActionAvailability,
                "guarantee.last-event",
                "guarantee.extension-timing",
                "summary.oldest-pending");
        }

        private void UpdateEditSection()
        {
            if (_currentGuarantee == null)
            {
                EditAnchor.Visibility = Visibility.Collapsed;
                BtnEditGuarantee.IsEnabled = false;
                return;
            }

            EditAnchor.Visibility = Visibility.Visible;
            BtnEditGuarantee.IsEnabled = true;
            TxtEditHint.Text = "يمكنك فتح نموذج التعديل لمراجعة بيانات هذا الضمان وتحديثها.";
        }

        private void UpdateExtensionSection()
        {
            if (_currentGuarantee == null)
            {
                ExtensionAnchor.Visibility = Visibility.Collapsed;
                BtnRequestExtension.IsEnabled = false;
                return;
            }

            bool canRequestExtension = _currentGuarantee.LifecycleStatus == GuaranteeLifecycleStatus.Active;
            ExtensionAnchor.Visibility = canRequestExtension ? Visibility.Visible : Visibility.Collapsed;
            if (!canRequestExtension)
            {
                BtnRequestExtension.IsEnabled = false;
                return;
            }

            int rootId = _currentGuarantee.RootId ?? _currentGuarantee.Id;
            bool hasPendingExtension = _databaseService.HasPendingWorkflowRequest(rootId, RequestType.Extension);
            BtnRequestExtension.IsEnabled = !hasPendingExtension;
            TxtExtensionHint.Text = hasPendingExtension
                ? "يوجد طلب تمديد معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات."
                : $"الضمان نشط وينتهي في {_currentGuarantee.ExpiryDate:yyyy-MM-dd}. يمكنك إنشاء طلب تمديد جديد من هنا.";
        }

        private void UpdateReductionSection()
        {
            if (_currentGuarantee == null)
            {
                ReductionAnchor.Visibility = Visibility.Collapsed;
                BtnRequestReduction.IsEnabled = false;
                return;
            }

            bool canRequestReduction = _currentGuarantee.LifecycleStatus == GuaranteeLifecycleStatus.Active
                                    && _currentGuarantee.Amount > 0;
            ReductionAnchor.Visibility = canRequestReduction ? Visibility.Visible : Visibility.Collapsed;
            if (!canRequestReduction)
            {
                BtnRequestReduction.IsEnabled = false;
                return;
            }

            int rootId = _currentGuarantee.RootId ?? _currentGuarantee.Id;
            bool hasPendingReduction = _databaseService.HasPendingWorkflowRequest(rootId, RequestType.Reduction);
            BtnRequestReduction.IsEnabled = !hasPendingReduction;
            TxtReductionHint.Text = hasPendingReduction
                ? "يوجد طلب تخفيض معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات."
                : $"المبلغ الحالي لهذا الضمان {_currentGuarantee.Amount:N2}. يمكنك إنشاء طلب تخفيض جديد من هنا.";
        }

        private void UpdateReleaseSection()
        {
            if (_currentGuarantee == null)
            {
                ReleaseAnchor.Visibility = Visibility.Collapsed;
                BtnRequestRelease.IsEnabled = false;
                return;
            }

            bool canRequestRelease = _currentGuarantee.LifecycleStatus == GuaranteeLifecycleStatus.Active;
            ReleaseAnchor.Visibility = canRequestRelease ? Visibility.Visible : Visibility.Collapsed;
            if (!canRequestRelease)
            {
                BtnRequestRelease.IsEnabled = false;
                return;
            }

            int rootId = _currentGuarantee.RootId ?? _currentGuarantee.Id;
            bool hasPendingRelease = _databaseService.HasPendingWorkflowRequest(rootId, RequestType.Release);
            BtnRequestRelease.IsEnabled = !hasPendingRelease;
            TxtReleaseHint.Text = hasPendingRelease
                ? "يوجد طلب إفراج معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات."
                : "يمكنك إنشاء طلب إفراج جديد لهذا الضمان من هنا.";
        }

        private void UpdateLiquidationSection()
        {
            if (_currentGuarantee == null)
            {
                LiquidationAnchor.Visibility = Visibility.Collapsed;
                BtnRequestLiquidation.IsEnabled = false;
                return;
            }

            bool canRequestLiquidation = _currentGuarantee.LifecycleStatus == GuaranteeLifecycleStatus.Active;
            LiquidationAnchor.Visibility = canRequestLiquidation ? Visibility.Visible : Visibility.Collapsed;
            if (!canRequestLiquidation)
            {
                BtnRequestLiquidation.IsEnabled = false;
                return;
            }

            int rootId = _currentGuarantee.RootId ?? _currentGuarantee.Id;
            bool hasPendingLiquidation = _databaseService.HasPendingWorkflowRequest(rootId, RequestType.Liquidation);
            BtnRequestLiquidation.IsEnabled = !hasPendingLiquidation;
            TxtLiquidationHint.Text = hasPendingLiquidation
                ? "يوجد طلب تسييل معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات."
                : "يمكنك إنشاء طلب تسييل جديد لهذا الضمان من هنا.";
        }

        private void UpdateVerificationSection()
        {
            if (_currentGuarantee == null)
            {
                VerificationAnchor.Visibility = Visibility.Collapsed;
                BtnRequestVerification.IsEnabled = false;
                return;
            }

            bool canRequestVerification = _currentGuarantee.LifecycleStatus == GuaranteeLifecycleStatus.Active;
            VerificationAnchor.Visibility = canRequestVerification ? Visibility.Visible : Visibility.Collapsed;
            if (!canRequestVerification)
            {
                BtnRequestVerification.IsEnabled = false;
                return;
            }

            int rootId = _currentGuarantee.RootId ?? _currentGuarantee.Id;
            bool hasPendingVerification = _databaseService.HasPendingWorkflowRequest(rootId, RequestType.Verification);
            BtnRequestVerification.IsEnabled = !hasPendingVerification;
            TxtVerificationHint.Text = hasPendingVerification
                ? "يوجد طلب تحقق معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات."
                : "يمكنك إنشاء طلب تحقق جديد لهذا الضمان من هنا.";
        }

        private void UpdateReplacementSection()
        {
            if (_currentGuarantee == null)
            {
                ReplacementAnchor.Visibility = Visibility.Collapsed;
                BtnRequestReplacement.IsEnabled = false;
                return;
            }

            bool canRequestReplacement = _currentGuarantee.LifecycleStatus == GuaranteeLifecycleStatus.Active;
            ReplacementAnchor.Visibility = canRequestReplacement ? Visibility.Visible : Visibility.Collapsed;
            if (!canRequestReplacement)
            {
                BtnRequestReplacement.IsEnabled = false;
                return;
            }

            int rootId = _currentGuarantee.RootId ?? _currentGuarantee.Id;
            bool hasPendingReplacement = _databaseService.HasPendingWorkflowRequest(rootId, RequestType.Replacement);
            BtnRequestReplacement.IsEnabled = !hasPendingReplacement;
            TxtReplacementHint.Text = hasPendingReplacement
                ? "يوجد طلب استبدال معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات."
                : "يمكنك إنشاء طلب استبدال جديد لهذا الضمان من هنا.";
        }

        private void UpdateAnnulmentSection()
        {
            if (_currentGuarantee == null)
            {
                AnnulmentAnchor.Visibility = Visibility.Collapsed;
                return;
            }

            bool isAnnullable = _currentGuarantee.LifecycleStatus == GuaranteeLifecycleStatus.Released
                             || _currentGuarantee.LifecycleStatus == GuaranteeLifecycleStatus.Liquidated;

            AnnulmentAnchor.Visibility = isAnnullable ? Visibility.Visible : Visibility.Collapsed;

            if (isAnnullable)
            {
                int rootId = _currentGuarantee.RootId ?? _currentGuarantee.Id;
                bool hasPendingAnnulment = _databaseService.HasPendingWorkflowRequest(rootId, RequestType.Annulment);
                TxtAnnulmentHint.Text = hasPendingAnnulment
                    ? "يوجد طلب نقض معلق لهذا الضمان. يمكنك تسجيل رد البنك من شاشة الطلبات."
                    : $"هذا الضمان في حالة {_currentGuarantee.LifecycleStatusLabel}. إذا كان الإجراء خاطئاً، يمكنك تقديم طلب نقض لإعادة تفعيله.";
            }
        }

        private void RequestExtension_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGuarantee == null)
            {
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(_currentGuarantee.Id) ?? _currentGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                GetShell()?.SetStatus("طلب التمديد متاح للضمانات النشطة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Extension))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب تمديد معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                GetShell()?.SetStatus("يوجد بالفعل طلب تمديد معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

            MainWindow? shell = GetShell();
            var dialog = new CreateExtensionRequestWindow(new List<Guarantee> { guarantee }, guarantee.Id);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _workflowService.CreateExtensionRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestedExpiryDate,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                RefreshView();
                shell?.SetStatus(
                    $"تم تسجيل طلب التمديد للضمان رقم {guarantee.GuaranteeNo}. انتقل إلى شاشة الطلبات لمتابعة رد البنك.",
                    ShellStatusTone.Success);
            }
            catch (InvalidOperationException ex)
            {
                AppDialogService.ShowWarning(ex.Message);
                shell?.SetStatus("تعذر تسجيل طلب التمديد.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل طلب التمديد.");
                shell?.SetStatus("فشل تسجيل طلب التمديد.", ShellStatusTone.Error);
            }
        }

        private void RequestReduction_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGuarantee == null)
            {
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(_currentGuarantee.Id) ?? _currentGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active || guarantee.Amount <= 0)
            {
                GetShell()?.SetStatus("طلب التخفيض متاح للضمانات النشطة التي يزيد مبلغها على صفر.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Reduction))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب تخفيض معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                GetShell()?.SetStatus("يوجد بالفعل طلب تخفيض معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

            MainWindow? shell = GetShell();
            var dialog = new CreateReductionRequestWindow(new List<Guarantee> { guarantee }, guarantee.Id);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _workflowService.CreateReductionRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestedAmount,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                RefreshView();
                shell?.SetStatus(
                    $"تم تسجيل طلب التخفيض للضمان رقم {guarantee.GuaranteeNo}. انتقل إلى شاشة الطلبات لمتابعة رد البنك.",
                    ShellStatusTone.Success);
            }
            catch (InvalidOperationException ex)
            {
                AppDialogService.ShowWarning(ex.Message);
                shell?.SetStatus("تعذر تسجيل طلب التخفيض.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل طلب التخفيض.");
                shell?.SetStatus("فشل تسجيل طلب التخفيض.", ShellStatusTone.Error);
            }
        }

        private void RequestRelease_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGuarantee == null)
            {
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(_currentGuarantee.Id) ?? _currentGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                GetShell()?.SetStatus("طلب الإفراج متاح للضمانات النشطة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Release))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب إفراج معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                GetShell()?.SetStatus("يوجد بالفعل طلب إفراج معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

            MainWindow? shell = GetShell();
            var dialog = new CreateReleaseRequestWindow(new List<Guarantee> { guarantee }, guarantee.Id);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _workflowService.CreateReleaseRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                RefreshView();
                shell?.SetStatus(
                    $"تم تسجيل طلب الإفراج للضمان رقم {guarantee.GuaranteeNo}. انتقل إلى شاشة الطلبات لمتابعة رد البنك.",
                    ShellStatusTone.Success);
            }
            catch (InvalidOperationException ex)
            {
                AppDialogService.ShowWarning(ex.Message);
                shell?.SetStatus("تعذر تسجيل طلب الإفراج.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل طلب الإفراج.");
                shell?.SetStatus("فشل تسجيل طلب الإفراج.", ShellStatusTone.Error);
            }
        }

        private void RequestLiquidation_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGuarantee == null)
            {
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(_currentGuarantee.Id) ?? _currentGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                GetShell()?.SetStatus("طلب التسييل متاح للضمانات النشطة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Liquidation))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب تسييل معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                GetShell()?.SetStatus("يوجد بالفعل طلب تسييل معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

            MainWindow? shell = GetShell();
            var dialog = new CreateLiquidationRequestWindow(new List<Guarantee> { guarantee }, guarantee.Id);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _workflowService.CreateLiquidationRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                RefreshView();
                shell?.SetStatus(
                    $"تم تسجيل طلب التسييل للضمان رقم {guarantee.GuaranteeNo}. انتقل إلى شاشة الطلبات لمتابعة رد البنك.",
                    ShellStatusTone.Success);
            }
            catch (InvalidOperationException ex)
            {
                AppDialogService.ShowWarning(ex.Message);
                shell?.SetStatus("تعذر تسجيل طلب التسييل.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل طلب التسييل.");
                shell?.SetStatus("فشل تسجيل طلب التسييل.", ShellStatusTone.Error);
            }
        }

        private void RequestVerification_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGuarantee == null)
            {
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(_currentGuarantee.Id) ?? _currentGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                GetShell()?.SetStatus("طلب التحقق متاح للضمانات النشطة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Verification))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب تحقق معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                GetShell()?.SetStatus("يوجد بالفعل طلب تحقق معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

            MainWindow? shell = GetShell();
            var dialog = new CreateVerificationRequestWindow(new List<Guarantee> { guarantee }, guarantee.Id);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _workflowService.CreateVerificationRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                RefreshView();
                shell?.SetStatus(
                    $"تم تسجيل طلب التحقق للضمان رقم {guarantee.GuaranteeNo}. انتقل إلى شاشة الطلبات لمتابعة رد البنك.",
                    ShellStatusTone.Success);
            }
            catch (InvalidOperationException ex)
            {
                AppDialogService.ShowWarning(ex.Message);
                shell?.SetStatus("تعذر تسجيل طلب التحقق.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل طلب التحقق.");
                shell?.SetStatus("فشل تسجيل طلب التحقق.", ShellStatusTone.Error);
            }
        }

        private void RequestReplacement_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGuarantee == null)
            {
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(_currentGuarantee.Id) ?? _currentGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                GetShell()?.SetStatus("طلب الاستبدال متاح للضمانات النشطة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Replacement))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب استبدال معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                GetShell()?.SetStatus("يوجد بالفعل طلب استبدال معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

            MainWindow? shell = GetShell();
            var dialog = new CreateReplacementRequestWindow(new List<Guarantee> { guarantee }, guarantee.Id);
            if (shell != null)
            {
                dialog.Owner = shell;
            }

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                _workflowService.CreateReplacementRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.ReplacementGuaranteeNo,
                    dialog.ReplacementSupplier,
                    dialog.ReplacementBank,
                    dialog.ReplacementAmount,
                    dialog.ReplacementExpiryDate,
                    dialog.ReplacementGuaranteeType,
                    dialog.ReplacementBeneficiary,
                    dialog.ReplacementReferenceType,
                    dialog.ReplacementReferenceNumber,
                    dialog.RequestNotes,
                    dialog.CreatedBy);

                RefreshView();
                shell?.SetStatus(
                    $"تم تسجيل طلب الاستبدال للضمان رقم {guarantee.GuaranteeNo}. انتقل إلى شاشة الطلبات لمتابعة رد البنك.",
                    ShellStatusTone.Success);
            }
            catch (InvalidOperationException ex)
            {
                AppDialogService.ShowWarning(ex.Message);
                shell?.SetStatus("تعذر تسجيل طلب الاستبدال.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل طلب الاستبدال.");
                shell?.SetStatus("فشل تسجيل طلب الاستبدال.", ShellStatusTone.Error);
            }
        }

        private void EditGuarantee_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGuarantee == null)
            {
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(_currentGuarantee.Id) ?? _currentGuarantee;
            GetShell()?.ShowEditGuarantee(guarantee, GuaranteeFormReturnTarget.GuaranteeFile);
        }

        private RoutedEventHandler? ResolveInquiryActionHandler(string actionId)
        {
            return GuaranteeInquiryActionSupport.IsInquiryAction(actionId)
                ? (_, _) => OpenInquiryResult(actionId)
                : null;
        }

        private ContextActionAvailability ResolveInquiryActionAvailability(ContextActionDefinition action)
        {
            if (_currentGuarantee == null)
            {
                return ContextActionAvailability.Disabled("افتح ملف ضمان أولًا.");
            }

            return GuaranteeInquiryActionSupport.GetAvailability(action.Id ?? string.Empty, _currentGuarantee);
        }

        private void OpenInquiryResult(string actionId)
        {
            if (_currentGuarantee == null)
            {
                return;
            }

            Guarantee guarantee = _databaseService.GetGuaranteeById(_currentGuarantee.Id) ?? _currentGuarantee;
            MainWindow? shell = GetShell();

            try
            {
                OperationalInquiryResult? result = GuaranteeInquiryActionSupport.Execute(
                    actionId,
                    guarantee,
                    _inquiryService,
                    PromptForEmployeeName);

                if (result == null)
                {
                    return;
                }

                var inquiryWindow = new InquiryResultWindow(result, _databaseService, _workflowService, _excelService);
                if (shell != null)
                {
                    inquiryWindow.Owner = shell;
                }

                inquiryWindow.Show();
                shell?.SetStatus($"تم فتح جواب: {result.Title}.", ShellStatusTone.Info);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر فتح جواب هذا الاستعلام.");
                shell?.SetStatus("فشل فتح جواب هذا الاستعلام.", ShellStatusTone.Error);
            }
        }

        private string? PromptForEmployeeName()
        {
            MainWindow? shell = GetShell();
            var dialog = new TextPromptWindow(
                "استعلام أداء موظف",
                "أدخل اسم الموظف الذي تريد مراجعة طلباته خلال الشهر الماضي على العقود.",
                "اسم الموظف",
                "عرض الجواب",
                nextStepHint: "سيعرض النظام عدد طلبات التمديد أو الإفراج التي أنشأها هذا الموظف خلال الشهر الماضي للعقود.");

            if (shell != null)
            {
                dialog.Owner = shell;
            }

            return dialog.ShowDialog() == true ? dialog.ResultText : null;
        }

        private List<TimelineRow> BuildTimeline(IEnumerable<Guarantee> history, IEnumerable<WorkflowRequestListItem> requests)
        {
            List<TimelineRow> rows = new();

            foreach (Guarantee version in history)
            {
                string description = $"{version.VersionLabel} | {version.StatusLabel} | القيمة {version.Amount:N2} | المرفقات {version.AttachmentCount}";
                rows.Add(new TimelineRow(version.CreatedAt, $"إصدار {version.VersionLabel}", description));
            }

            foreach (WorkflowRequestListItem request in requests)
            {
                string description = $"{request.Request.TypeLabel} | {request.Request.StatusLabel} | المطلوب: {request.RequestedValueDisplay}";
                if (request.Request.HasLetter || request.Request.HasResponseDocument)
                {
                    description += $" | خطاب: {(request.Request.HasLetter ? "موجود" : "غير موجود")} | رد: {(request.Request.HasResponseDocument ? "موجود" : "غير موجود")}";
                }

                rows.Add(new TimelineRow(request.Request.RequestDate, $"طلب {request.Request.SequenceNumber}", description));
            }

            return rows.OrderByDescending(item => item.SortKey).ToList();
        }

        private void UpdateActionHubVisibility()
        {
            bool hasVisibleQuickAction = new[]
            {
                EditAnchor.Visibility,
                ExtensionAnchor.Visibility,
                ReductionAnchor.Visibility,
                ReleaseAnchor.Visibility,
                LiquidationAnchor.Visibility,
                VerificationAnchor.Visibility,
                ReplacementAnchor.Visibility,
                AnnulmentAnchor.Visibility
            }.Any(visibility => visibility == Visibility.Visible);

            CommandHubAnchor.Visibility = hasVisibleQuickAction ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateRequestActionState()
        {
            bool hasRequests = _requestItems.Count > 0;
            GridRelatedRequests.Visibility = hasRequests ? Visibility.Visible : Visibility.Collapsed;
            RequestEmptyStatePanel.Visibility = hasRequests ? Visibility.Collapsed : Visibility.Visible;
            RequestActionBar.Visibility = hasRequests ? Visibility.Visible : Visibility.Collapsed;

            if (!hasRequests)
            {
                BtnOpenRequestLetter.IsEnabled = false;
                BtnOpenRequestResponse.IsEnabled = false;
                TxtRequestSelectionHint.Text = "لا توجد طلبات مرتبطة بهذه السلسلة حتى الآن.";
                return;
            }

            BtnOpenRequestLetter.IsEnabled = SelectedRequest?.Request.HasLetter == true;
            BtnOpenRequestResponse.IsEnabled = SelectedRequest?.Request.HasResponseDocument == true;
            TxtRequestSelectionHint.Text = SelectedRequest == null
                ? "حدد طلبًا لفتح خطاب الطلب أو مستند الرد."
                : SelectedRequest.Request.HasResponseDocument
                    ? $"{SelectedRequest.Request.TypeLabel} | {SelectedRequest.Request.StatusLabel}"
                    : SelectedRequest.Request.Status != RequestStatus.Pending
                        ? $"{SelectedRequest.Request.TypeLabel} | {SelectedRequest.Request.StatusLabel} | لا يوجد مستند رد محفوظ بعد. يمكنك إلحاقه لاحقًا من شاشة الطلبات."
                        : $"{SelectedRequest.Request.TypeLabel} | {SelectedRequest.Request.StatusLabel}";
        }

        private void RequestAnnulment_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGuarantee == null)
            {
                return;
            }

            int rootId = _currentGuarantee.RootId ?? _currentGuarantee.Id;
            if (_databaseService.HasPendingWorkflowRequest(rootId, RequestType.Annulment))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب نقض معلق لهذا الضمان. انتقل إلى شاشة الطلبات لتسجيل رد البنك.");
                return;
            }

            string statusLabel = _currentGuarantee.LifecycleStatusLabel;
            MessageBoxResult confirm = AppDialogService.Ask(
                $"هل تريد تقديم طلب نقض لإجراء {statusLabel} للضمان رقم {_currentGuarantee.GuaranteeNo}؟{Environment.NewLine}سيتم إنشاء طلب معلق يُنفَّذ بعد تأكيد البنك.",
                "تأكيد طلب النقض",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                _workflowService.CreateAnnulmentRequest(_currentGuarantee.Id, reason: string.Empty);
                RefreshView();
                GetShell()?.SetStatus($"تم تسجيل طلب النقض للضمان رقم {_currentGuarantee.GuaranteeNo}. انتقل إلى شاشة الطلبات لمتابعة رد البنك.", ShellStatusTone.Success);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل طلب النقض.");
                GetShell()?.SetStatus("فشل تسجيل طلب النقض.", ShellStatusTone.Error);
            }
        }

        private void BackToPortfolio_Click(object sender, RoutedEventArgs e)
        {
            GetShell()?.ShowDataTable();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshView();
        }

        private void ExportSingle_Click(object sender, RoutedEventArgs e)
        {
            if (_currentGuarantee == null)
            {
                return;
            }

            bool exported = _excelService.ExportSingleGuaranteeReport(_currentGuarantee);
            GetShell()?.SetStatus(
                exported
                    ? $"تم تصدير ملف الضمان رقم {_currentGuarantee.GuaranteeNo}."
                    : $"تم إلغاء تصدير ملف الضمان رقم {_currentGuarantee.GuaranteeNo}.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void GridRelatedRequests_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateRequestActionState();
        }

        private void OpenSelectedRequestLetter_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedRequest?.Request.HasLetter != true)
            {
                GetShell()?.SetStatus("لا يوجد خطاب طلب محفوظ لهذا السجل.", ShellStatusTone.Warning);
                return;
            }

            try
            {
                _workflowService.OpenRequestLetter(SelectedRequest.Request);
                GetShell()?.SetStatus("تم فتح خطاب الطلب.", ShellStatusTone.Info);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر فتح خطاب الطلب.");
                GetShell()?.SetStatus("فشل فتح خطاب الطلب.", ShellStatusTone.Error);
            }
        }

        private void OpenSelectedRequestResponse_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedRequest?.Request.HasResponseDocument != true)
            {
                GetShell()?.SetStatus("لا يوجد مستند رد محفوظ لهذا السجل بعد. يمكنك إلحاقه لاحقًا من شاشة الطلبات.", ShellStatusTone.Warning);
                return;
            }

            try
            {
                _workflowService.OpenResponseDocument(SelectedRequest.Request);
                GetShell()?.SetStatus("تم فتح مستند الرد.", ShellStatusTone.Info);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر فتح مستند الرد.");
                GetShell()?.SetStatus("فشل فتح مستند الرد.", ShellStatusTone.Error);
            }
        }

        private void OpenOperations_Click(object sender, RoutedEventArgs e)
        {
            GetShell()?.ShowOperationCenter(true, SelectedRequest?.Request.Id);
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

        private sealed class TimelineRow
        {
            public TimelineRow(DateTime sortKey, string title, string description)
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

        private sealed class AttachmentRow
        {
            public AttachmentRow(AttachmentRecord source)
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
