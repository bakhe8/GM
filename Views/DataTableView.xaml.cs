using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GuaranteeManager.Contracts;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using GuaranteeManager.ViewModels;

namespace GuaranteeManager.Views
{
    public partial class DataTableView : UserControl, IShellSearchableView
    {
        private readonly IDatabaseService _dbService;
        private readonly IWorkflowService _workflowService;
        private readonly IExcelService _excelService;
        private readonly IOperationalInquiryService _inquiryService;
        private readonly IContextActionService _contextActionService;
        private readonly DataTableViewModel _viewModel;
        private readonly GuaranteeHistoryReportService _historyReportService = new();
        private readonly SecondaryWindowManager _windowManager = SecondaryWindowManager.Instance;
        private bool _suppressSelectionSideSheet;

        public DataTableView(
            IDatabaseService dbService,
            IWorkflowService workflowService,
            IExcelService excelService,
            IOperationalInquiryService inquiryService,
            IContextActionService contextActionService)
        {
            InitializeComponent();
            _dbService = dbService;
            _workflowService = workflowService;
            _excelService = excelService;
            _inquiryService = inquiryService;
            _contextActionService = contextActionService;
            _viewModel = new DataTableViewModel(dbService);
            DataContext = _viewModel;
            Loaded += (_, _) => RefreshView();
            IsVisibleChanged += DataTableView_IsVisibleChanged;
        }

        private void DataTableView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                // Force layout update when the view becomes visible to fix the "bunching" issue
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    GridGuarantees.UpdateLayout();
                    foreach (var column in GridGuarantees.Columns)
                    {
                        var originalWidth = column.Width;
                        column.Width = 0;
                        column.Width = originalWidth;
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        public void RefreshView()
        {
            _suppressSelectionSideSheet = true;
            try
            {
                _viewModel.Refresh();
            }
            finally
            {
                _suppressSelectionSideSheet = false;
            }

            GetShell()?.CloseSideSheet();
        }

        public void ApplyShellSearch(string query)
        {
            _suppressSelectionSideSheet = true;
            try
            {
                _viewModel.ApplyShellSearch(query);
            }
            finally
            {
                _suppressSelectionSideSheet = false;
            }

            GetShell()?.CloseSideSheet();
        }

        private MainWindow? GetShell()
        {
            return Window.GetWindow(this) as MainWindow;
        }

        private Guarantee? SelectedGuarantee => _viewModel.SelectedGuarantee;

        private void ApplyFilters()
        {
            _suppressSelectionSideSheet = true;
            try
            {
                _viewModel.ApplyFilters(
                    TxtSearchLocal.Text,
                    CmbBankFilter.SelectedItem?.ToString(),
                    CmbTypeFilter.SelectedItem?.ToString(),
                    CmbStatusFilter.SelectedItem?.ToString());
            }
            finally
            {
                _suppressSelectionSideSheet = false;
            }

            GetShell()?.CloseSideSheet();
        }

        private void UpdateSideSheet(Guarantee? guarantee)
        {
            MainWindow? shell = GetShell();
            if (shell == null)
            {
                return;
            }

            if (guarantee == null)
            {
                shell.CloseSideSheet();
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            List<Guarantee> history = _dbService.GetGuaranteeHistory(guarantee.Id);
            List<WorkflowRequest> requests = _dbService.GetWorkflowRequestsByRootId(rootId);
            shell.ShowSideSheet(
                "معاينة الضمان",
                "ملخص تشغيلي سريع للسجل المحدد.",
                new GuaranteeSideSheetView(guarantee, history, requests, _dbService, _excelService));
        }

        private void FiltersChanged(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            ApplyFilters();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            _suppressSelectionSideSheet = true;
            try
            {
                _viewModel.ClearFilters();
            }
            finally
            {
                _suppressSelectionSideSheet = false;
            }

            GetShell()?.CloseSideSheet();
        }

        private void GridGuarantees_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionSideSheet)
            {
                return;
            }

            UpdateSideSheet(SelectedGuarantee);
        }

        private void GridGuarantees_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataGridContextMenuAssist.TryGetRowItem<Guarantee>(e, out Guarantee? guarantee) &&
                guarantee != null &&
                SelectedGuarantee?.Id == guarantee.Id)
            {
                UpdateSideSheet(guarantee);
            }
        }

        private void GridGuarantees_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _suppressSelectionSideSheet = true;
            try
            {
                if (!DataGridContextMenuAssist.TrySelectRowFromRightClick(GridGuarantees, e) || SelectedGuarantee == null)
                {
                    return;
                }
            }
            finally
            {
                _suppressSelectionSideSheet = false;
            }

            ContextMenu contextMenu = ContextActionMenuFactory.Build(
                WorkspaceContextMenuSections.BuildGuaranteeSections(_contextActionService, includeVisibleListExport: true),
                ResolveGuaranteeContextActionHandler,
                ResolveGuaranteeContextActionAvailability);

            DataGridContextMenuAssist.OpenContextMenuAtPointer(GridGuarantees, contextMenu);
            e.Handled = true;
        }

        private void GridGuarantees_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (SelectedGuarantee == null || !DataGridContextMenuAssist.IsPointerOverRow(GridGuarantees))
            {
                GridGuarantees.ContextMenu = null;
                e.Handled = true;
                return;
            }

            GridGuarantees.ContextMenu = ContextActionMenuFactory.Build(
                WorkspaceContextMenuSections.BuildGuaranteeSections(_contextActionService, includeVisibleListExport: true),
                ResolveGuaranteeContextActionHandler,
                ResolveGuaranteeContextActionAvailability);
        }

        private void OpenSelected_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedGuarantee == null)
            {
                GetShell()?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            GetShell()?.ShowGuaranteeFile(SelectedGuarantee, "شاشة الضمانات", true);
        }

        private void ExportFiltered_Click(object sender, RoutedEventArgs e)
        {
            bool exported = _excelService.ExportGuarantees(_viewModel.FilteredGuarantees);
            GetShell()?.SetStatus(
                exported ? "تم تصدير قائمة الضمانات المعروضة." : "تم إلغاء تصدير قائمة الضمانات المعروضة.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshView();
            GetShell()?.SetStatus("تم تحديث محفظة الضمانات.", ShellStatusTone.Success);
        }

        private void GridGuarantees_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenSelected_Click(sender, new RoutedEventArgs());
        }

        private RoutedEventHandler? ResolveGuaranteeContextActionHandler(string actionId)
        {
            return actionId switch
            {
                "workspace.guarantee.open-file" => OpenSelected_Click,
                "execute.create-extension" => CreateExtensionRequest_Click,
                "execute.create-reduction" => CreateReductionRequest_Click,
                "execute.create-release" => CreateReleaseRequest_Click,
                "execute.create-liquidation" => CreateLiquidationRequest_Click,
                "execute.create-verification" => CreateVerificationRequest_Click,
                "execute.create-replacement" => CreateReplacementRequest_Click,
                "execute.create-annulment" => CreateAnnulmentRequest_Click,
                "execute.edit-guarantee" => EditGuarantee_Click,
                "navigate.history" => OpenGuaranteeHistory_Click,
                "evidence.attachments" => OpenGuaranteeAttachments_Click,
                "export.guarantee-report" => ExportGuaranteeReport_Click,
                "export.guarantee-history" => ExportGuaranteeHistory_Click,
                "export.same-bank" => ExportSameBank_Click,
                "export.same-supplier" => ExportSameSupplier_Click,
                "export.same-temporal-status" => ExportSameTemporalStatus_Click,
                "export.visible-list" => ExportFiltered_Click,
                _ when GuaranteeInquiryActionSupport.IsInquiryAction(actionId) => (_, _) => OpenGuaranteeInquiry(actionId),
                "copy.guarantee-no" => CopyGuaranteeNumber_Click,
                "copy.supplier" => CopyGuaranteeSupplier_Click,
                "copy.reference-type" => CopyGuaranteeReferenceType_Click,
                "copy.reference-number" => CopyGuaranteeReferenceNumber_Click,
                _ => null
            };
        }

        private ContextActionAvailability ResolveGuaranteeContextActionAvailability(ContextActionDefinition action)
        {
            Guarantee? guarantee = SelectedGuarantee;
            if (guarantee == null)
            {
                return ContextActionAvailability.Disabled("حدد سجلًا أولًا.");
            }

            if (GuaranteeInquiryActionSupport.IsInquiryAction(action.Id ?? string.Empty))
            {
                return GuaranteeInquiryActionSupport.GetAvailability(action.Id ?? string.Empty, guarantee);
            }

            return _viewModel.GetContextActionAvailability(action.Id, guarantee);
        }

        private void OpenGuaranteeInquiry(string actionId)
        {
            Guarantee? selectedGuarantee = SelectedGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _dbService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;

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

                var inquiryWindow = new InquiryResultWindow(result, _dbService, _workflowService, _excelService);
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

        private void CreateExtensionRequest_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedGuarantee = SelectedGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _dbService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                shell?.SetStatus("طلب التمديد متاح للضمانات النشطة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_dbService.HasPendingWorkflowRequest(rootId, RequestType.Extension))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب تمديد معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                shell?.SetStatus("يوجد بالفعل طلب تمديد معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

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

        private void CreateReductionRequest_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedGuarantee = SelectedGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _dbService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active || guarantee.Amount <= 0)
            {
                shell?.SetStatus("طلب التخفيض متاح للضمانات النشطة التي يزيد مبلغها على صفر.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_dbService.HasPendingWorkflowRequest(rootId, RequestType.Reduction))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب تخفيض معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                shell?.SetStatus("يوجد بالفعل طلب تخفيض معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

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

        private void CreateReleaseRequest_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedGuarantee = SelectedGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _dbService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                shell?.SetStatus("طلب الإفراج متاح للضمانات النشطة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_dbService.HasPendingWorkflowRequest(rootId, RequestType.Release))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب إفراج معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                shell?.SetStatus("يوجد بالفعل طلب إفراج معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

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

        private void CreateLiquidationRequest_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedGuarantee = SelectedGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _dbService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                shell?.SetStatus("طلب التسييل متاح للضمانات النشطة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_dbService.HasPendingWorkflowRequest(rootId, RequestType.Liquidation))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب تسييل معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                shell?.SetStatus("يوجد بالفعل طلب تسييل معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

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

        private void CreateVerificationRequest_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedGuarantee = SelectedGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _dbService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                shell?.SetStatus("طلب التحقق متاح للضمانات النشطة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_dbService.HasPendingWorkflowRequest(rootId, RequestType.Verification))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب تحقق معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                shell?.SetStatus("يوجد بالفعل طلب تحقق معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

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

        private void CreateReplacementRequest_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedGuarantee = SelectedGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _dbService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                shell?.SetStatus("طلب الاستبدال متاح للضمانات النشطة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_dbService.HasPendingWorkflowRequest(rootId, RequestType.Replacement))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب استبدال معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                shell?.SetStatus("يوجد بالفعل طلب استبدال معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

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

        private void CreateAnnulmentRequest_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedGuarantee = SelectedGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _dbService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;
            if (guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Released &&
                guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Liquidated)
            {
                shell?.SetStatus("طلب النقض متاح للضمانات المفرج عنها أو المسيّلة فقط.", ShellStatusTone.Warning);
                return;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            if (_dbService.HasPendingWorkflowRequest(rootId, RequestType.Annulment))
            {
                AppDialogService.ShowWarning("يوجد بالفعل طلب نقض معلق لهذا الضمان. يمكنك متابعة رد البنك من شاشة الطلبات.");
                shell?.SetStatus("يوجد بالفعل طلب نقض معلق لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

            var dialog = new CreateAnnulmentRequestWindow(new List<Guarantee> { guarantee }, guarantee.Id);
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
                _workflowService.CreateAnnulmentRequest(
                    dialog.SelectedGuaranteeId,
                    dialog.RequestReason,
                    dialog.CreatedBy);

                RefreshView();
                shell?.SetStatus(
                    $"تم تسجيل طلب النقض للضمان رقم {guarantee.GuaranteeNo}. انتقل إلى شاشة الطلبات لمتابعة رد البنك.",
                    ShellStatusTone.Success);
            }
            catch (InvalidOperationException ex)
            {
                AppDialogService.ShowWarning(ex.Message);
                shell?.SetStatus("تعذر تسجيل طلب النقض.", ShellStatusTone.Warning);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(ex, "تعذر تسجيل طلب النقض.");
                shell?.SetStatus("فشل تسجيل طلب النقض.", ShellStatusTone.Error);
            }
        }

        private void EditGuarantee_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? selectedGuarantee = SelectedGuarantee;
            MainWindow? shell = GetShell();
            if (selectedGuarantee == null)
            {
                shell?.SetStatus("حدد ضمانًا أولًا.", ShellStatusTone.Warning);
                return;
            }

            Guarantee guarantee = _dbService.GetGuaranteeById(selectedGuarantee.Id) ?? selectedGuarantee;
            shell?.ShowEditGuarantee(guarantee, GuaranteeFormReturnTarget.DataTable);
        }

        private void OpenGuaranteeHistory_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = SelectedGuarantee;
            if (guarantee == null)
            {
                return;
            }

            bool opened = _windowManager.ShowOrActivate(
                $"history:{guarantee.RootId ?? guarantee.Id}",
                () => new GuaranteeHistoryWindow(guarantee, _dbService),
                existing =>
                {
                    if (existing is GuaranteeHistoryWindow historyWindow)
                    {
                        historyWindow.RefreshHistory();
                    }
                });

            GetShell()?.SetStatus(
                opened ? $"تم فتح سجل الضمان رقم {guarantee.GuaranteeNo}." : $"تم تنشيط سجل الضمان رقم {guarantee.GuaranteeNo}.",
                ShellStatusTone.Info);
        }

        private void OpenGuaranteeAttachments_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = SelectedGuarantee;
            MainWindow? shell = GetShell();
            if (guarantee == null || shell == null || guarantee.Attachments.Count == 0)
            {
                return;
            }

            shell.OpenAttachmentWindow(
                $"attachments:readonly:{guarantee.Id}",
                () => new AttachmentListWindow(
                    guarantee.Attachments,
                    _dbService,
                    allowDelete: false,
                    headerText: $"مرفقات الضمان رقم {guarantee.GuaranteeNo}"),
                $"تم فتح مرفقات الضمان رقم {guarantee.GuaranteeNo}.",
                $"تم تنشيط مرفقات الضمان رقم {guarantee.GuaranteeNo}.",
                existing =>
                {
                    if (existing is AttachmentListWindow attachmentWindow)
                    {
                        attachmentWindow.RefreshAttachments(
                            guarantee.Attachments,
                            $"مرفقات الضمان رقم {guarantee.GuaranteeNo}");
                    }
                });
        }

        private void ExportGuaranteeReport_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = SelectedGuarantee;
            if (guarantee == null)
            {
                return;
            }

            bool exported = _excelService.ExportSingleGuaranteeReport(guarantee);
            GetShell()?.SetStatus(
                exported
                    ? $"تم تصدير تقرير الضمان رقم {guarantee.GuaranteeNo}."
                    : $"تم إلغاء تصدير تقرير الضمان رقم {guarantee.GuaranteeNo}.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void ExportGuaranteeHistory_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = SelectedGuarantee;
            if (guarantee == null)
            {
                return;
            }

            List<Guarantee> history = _viewModel.GetSortedHistory(guarantee.Id);
            bool exported = _historyReportService.ExportHistoryToExcel(guarantee, history);
            GetShell()?.SetStatus(
                exported
                    ? $"تم تصدير تاريخ الضمان رقم {guarantee.GuaranteeNo}."
                    : $"تم إلغاء تصدير تاريخ الضمان رقم {guarantee.GuaranteeNo}.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void ExportSameBank_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = SelectedGuarantee;
            if (guarantee == null || string.IsNullOrWhiteSpace(guarantee.Bank))
            {
                return;
            }

            List<Guarantee> guarantees = _viewModel.GetGuaranteesByBank(guarantee.Bank);
            bool exported = _excelService.ExportGuaranteesByBank(guarantee.Bank, guarantees);
            GetShell()?.SetStatus(
                exported
                    ? $"تم تصدير جميع ضمانات البنك {guarantee.Bank}."
                    : $"تم إلغاء تصدير ضمانات البنك {guarantee.Bank}.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void ExportSameSupplier_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = SelectedGuarantee;
            if (guarantee == null || string.IsNullOrWhiteSpace(guarantee.Supplier))
            {
                return;
            }

            List<Guarantee> guarantees = _viewModel.GetGuaranteesBySupplier(guarantee.Supplier);
            bool exported = _excelService.ExportGuaranteesBySupplier(guarantee.Supplier, guarantees);
            GetShell()?.SetStatus(
                exported
                    ? $"تم تصدير جميع ضمانات المورد {guarantee.Supplier}."
                    : $"تم إلغاء تصدير ضمانات المورد {guarantee.Supplier}.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void ExportSameTemporalStatus_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = SelectedGuarantee;
            if (guarantee == null || !TryGetTemporalStatus(guarantee, out GuaranteeTimeStatus temporalStatus))
            {
                GetShell()?.SetStatus("تعذر تحديد الحالة الزمنية لهذا الضمان.", ShellStatusTone.Warning);
                return;
            }

            List<Guarantee> guarantees = _viewModel.GetGuaranteesByTemporalStatus(temporalStatus);
            bool exported = _excelService.ExportGuaranteesByTemporalStatus(guarantee.StatusLabel, guarantees);
            GetShell()?.SetStatus(
                exported
                    ? $"تم تصدير جميع الضمانات ذات الحالة الزمنية {guarantee.StatusLabel}."
                    : $"تم إلغاء تصدير الضمانات ذات الحالة الزمنية {guarantee.StatusLabel}.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }

        private void CopyGuaranteeNumber_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(SelectedGuarantee?.GuaranteeNo, "رقم الضمان");
        }

        private void CopyGuaranteeSupplier_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(SelectedGuarantee?.Supplier, "اسم المورد");
        }

        private void CopyGuaranteeReferenceType_Click(object sender, RoutedEventArgs e)
        {
            Guarantee? guarantee = SelectedGuarantee;
            CopyToClipboard(guarantee?.ReferenceTypeLabel, "نوع المرجع");
        }

        private void CopyGuaranteeReferenceNumber_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(SelectedGuarantee?.ReferenceNumber, "رقم المرجع");
        }

        private void CopyToClipboard(string? value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                GetShell()?.SetStatus($"لا توجد قيمة متاحة لنسخ {label}.", ShellStatusTone.Warning);
                return;
            }

            Clipboard.SetText(value);
            GetShell()?.SetStatus($"تم نسخ {label}.", ShellStatusTone.Success);
        }

        private static bool TryGetTemporalStatus(Guarantee guarantee, out GuaranteeTimeStatus temporalStatus)
        {
            if (guarantee.IsExpired)
            {
                temporalStatus = GuaranteeTimeStatus.Expired;
                return true;
            }

            if (guarantee.IsExpiringSoon)
            {
                temporalStatus = GuaranteeTimeStatus.ExpiringSoon;
                return true;
            }

            temporalStatus = GuaranteeTimeStatus.Active;
            return true;
        }
    }
}
