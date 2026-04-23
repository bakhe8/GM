using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GuaranteeManager.Contracts;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using GuaranteeManager.ViewModels;
using Microsoft.Win32;

namespace GuaranteeManager.Views
{
    public partial class AddEntryView : UserControl, INavigationGuard, ISaveShortcutTarget
    {
        private readonly IDatabaseService _dbService;
        private readonly IExcelService _excelService;
        private readonly Guarantee? _editSource;
        private readonly GuaranteeFormReturnTarget _returnTarget;
        private readonly AddEntryViewModel _viewModel;

        public AddEntryView(
            IDatabaseService dbService,
            IExcelService excelService,
            Guarantee? editG = null,
            GuaranteeFormReturnTarget returnTarget = GuaranteeFormReturnTarget.DataTable)
        {
            InitializeComponent();
            _dbService = dbService;
            _excelService = excelService;
            _editSource = editG;
            _returnTarget = returnTarget;
            _viewModel = new AddEntryViewModel(_dbService, _editSource);
            DataContext = _viewModel;

            RegisterValidationHooks();
        }

        public bool HasUnsavedChanges => _viewModel.HasUnsavedChanges;
        public bool CanExecuteSave => !_viewModel.IsSaving;

        public string GetSaveShortcutUnavailableReason()
        {
            return _viewModel.IsSaving ? "جاري تنفيذ الحفظ الآن." : "النموذج غير جاهز للحفظ.";
        }

        public void ExecuteSaveShortcut()
        {
            SaveForm();
        }

        public bool ConfirmNavigationAway()
        {
            return !HasUnsavedChanges || AppDialogService.ConfirmDiscardChanges();
        }

        private MainWindow? GetShell()
        {
            return Window.GetWindow(this) as MainWindow;
        }

        private void RegisterValidationHooks()
        {
            TxtGuaranteeNo.LostFocus += (_, _) => _viewModel.ValidateGuaranteeNo();
            TxtAmount.LostFocus += (_, _) => _viewModel.ValidateAmount();
            TxtSupplier.LostFocus += (_, _) => _viewModel.ValidateSupplier();
            TxtBank.LostFocus += (_, _) => _viewModel.ValidateBank();
        }

        private void BrowseAttachments_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Multiselect = true,
                Filter = "All Files|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            _viewModel.AddAttachments(dialog.FileNames);
        }

        private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string fileName)
            {
                return;
            }

            _viewModel.RemoveAttachmentByFileName(fileName);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveForm();
        }

        private void SaveForm()
        {
            if (_viewModel.IsSaving)
            {
                return;
            }

            AddEntryViewModel.SaveResult result = _viewModel.Save();
            if (!result.Success)
            {
                if (!string.IsNullOrWhiteSpace(result.WarningMessage))
                {
                    AppDialogService.ShowWarning(result.WarningMessage);
                }
                else if (result.Exception != null)
                {
                    AppDialogService.ShowError(result.Exception, result.ErrorMessage ?? "تعذر حفظ بيانات الضمان.");
                    GetShell()?.SetStatus("فشل حفظ بيانات الضمان.", ShellStatusTone.Error);
                }
                return;
            }

            if (result.SavedGuarantee != null)
            {
                GetShell()?.ShowGuaranteeFile(
                    result.SavedGuarantee,
                    _editSource == null ? "إدخال الضمان" : "تعديل الضمان",
                    true);
            }

            if (!string.IsNullOrWhiteSpace(result.WarningMessage))
            {
                AppDialogService.ShowWarning(result.WarningMessage);
            }

            if (!string.IsNullOrWhiteSpace(result.SuccessStatusMessage))
            {
                GetShell()?.SetStatus(
                    result.SuccessStatusMessage,
                    string.IsNullOrWhiteSpace(result.WarningMessage) ? ShellStatusTone.Success : ShellStatusTone.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmNavigationAway())
            {
                return;
            }

            MainWindow? shell = GetShell();
            if (shell == null)
            {
                return;
            }

            switch (_returnTarget)
            {
                case GuaranteeFormReturnTarget.TodayDesk:
                    shell.ShowTodayDesk();
                    break;
                case GuaranteeFormReturnTarget.OperationCenter:
                    shell.ShowOperationCenter();
                    break;
                case GuaranteeFormReturnTarget.GuaranteeFile when _editSource != null:
                    shell.ShowGuaranteeFile(_editSource, "إلغاء من التعديل", true);
                    break;
                default:
                    shell.ShowDataTable();
                    break;
            }
        }

        private void RunWorkspaceReport_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string reportKey)
            {
                return;
            }

            bool exported = WorkspaceReportCatalog.Run(
                reportKey,
                _dbService,
                _excelService);

            string reportTitle = WorkspaceReportCatalog.PortfolioActions
                .Concat(WorkspaceReportCatalog.OperationalActions)
                .FirstOrDefault(action => action.Key == reportKey)?.Title ?? "التقرير";

            GetShell()?.SetStatus(
                exported
                    ? $"تم إنشاء تقرير {reportTitle} من البيانات المحفوظة الحالية."
                    : $"تم إلغاء إنشاء تقرير {reportTitle}.",
                exported ? ShellStatusTone.Success : ShellStatusTone.Warning);
        }
    }
}
