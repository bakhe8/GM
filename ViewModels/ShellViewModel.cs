using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using GuaranteeManager.Contracts;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager.ViewModels
{
    public sealed class ShellViewModel : ViewModelBase
    {
        private static readonly IReadOnlyDictionary<string, string> WorkspaceAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["الرئيسية"] = "Today",
            ["إدخال"] = "Today",
            ["إدخال الضمان"] = "Today",
            ["المتابعات"] = "Reception",
            ["متابعات"] = "Reception",
            ["الضمانات"] = "Portfolio",
            ["الطلبات"] = "BankRoom",
            ["الإعدادات"] = "Administration"
        };

        private readonly IDatabaseService _databaseService;
        private readonly IViewFactory _viewFactory;
        private readonly RelayCommand _resumeLastFileCommand;

        private IRefreshableView? _todayDeskView;
        private IShellSearchableView? _dataTableView;
        private IOperationCenterWorkspace? _operationCenterView;
        private IGuaranteeFileWorkspace? _guaranteeFileView;
        private object? _addEntryView;
        private IRefreshableView? _settingsView;

        private object? _currentContent;
        private string _searchQuery = string.Empty;
        private string _currentWorkspaceName = "الرئيسية";
        private string _currentWorkspaceDescription = "المساحة الرئيسية لإدخال الضمانات الجديدة بشكل موحد وواضح.";
        private string _statusMessage = "جاهز";
        private ShellStatusTone _statusTone = ShellStatusTone.Info;
        private string? _activeNavigationKey = "Today";

        private int? _lastOpenedGuaranteeId;
        private string _lastOpenedGuaranteeTitle = "لا يوجد ملف أخير";
        private string _lastOpenedGuaranteeSummary = "ابدأ من شاشة الضمانات أو الإدخال لفتح ملف.";

        public ShellViewModel(IDatabaseService databaseService, IViewFactory viewFactory)
        {
            _databaseService = databaseService;
            _viewFactory = viewFactory;

            SearchCommand = new RelayCommand(_ => ExecuteUnifiedSearch());
            NavigateCommand = new RelayCommand(parameter => NavigateByKey(parameter as string));
            _resumeLastFileCommand = new RelayCommand(_ => ResumeLastFile(), _ => HasLastFile);
            ResumeLastFileCommand = _resumeLastFileCommand;

            ShowAddEntryScreen();
            RaiseLastFileStateChanged();
        }

        public object? CurrentContent => _currentContent;

        public string SearchQuery
        {
            get => _searchQuery;
            set => SetProperty(ref _searchQuery, value);
        }

        public string CurrentWorkspaceName
        {
            get => _currentWorkspaceName;
            private set => SetProperty(ref _currentWorkspaceName, value);
        }

        public string CurrentWorkspaceDescription
        {
            get => _currentWorkspaceDescription;
            private set => SetProperty(ref _currentWorkspaceDescription, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public ShellStatusTone StatusTone
        {
            get => _statusTone;
            private set => SetProperty(ref _statusTone, value);
        }

        public string? ActiveNavigationKey
        {
            get => _activeNavigationKey;
            private set => SetProperty(ref _activeNavigationKey, value);
        }

        public bool HasLastFile => _lastOpenedGuaranteeId.HasValue;

        public string LastFileToolTip => HasLastFile
            ? $"{_lastOpenedGuaranteeTitle}{Environment.NewLine}{_lastOpenedGuaranteeSummary}"
            : "لا يوجد ملف أخير بعد.";

        public ICommand SearchCommand { get; }

        public ICommand NavigateCommand { get; }

        public ICommand ResumeLastFileCommand { get; }

        public void WarmUpViews()
        {
            _todayDeskView ??= _viewFactory.CreateTodayDesk();
            _dataTableView ??= _viewFactory.CreateDataTable();
            _operationCenterView ??= _viewFactory.CreateOperationCenter();
            _settingsView ??= _viewFactory.CreateSettings();

            SetStatus("جاهز للعمل.", ShellStatusTone.Info);
        }

        public bool CanNavigateAway()
        {
            return CurrentContent is not INavigationGuard guard || guard.ConfirmNavigationAway();
        }

        public void SetStatus(string message, ShellStatusTone tone = ShellStatusTone.Info)
        {
            StatusMessage = message;
            StatusTone = tone;
        }

        public void ShowTodayDesk(bool refreshExisting = false)
        {
            _todayDeskView ??= _viewFactory.CreateTodayDesk();

            if (!TryNavigateToContent(
                _todayDeskView,
                "المتابعات",
                "لوحة المتابعات اليومية للضمانات والطلبات والمحفزات الفورية.",
                "Reception",
                "تم فتح المتابعات."))
            {
                return;
            }

            if (refreshExisting)
            {
                _todayDeskView.RefreshView();
            }
        }

        public void ShowDataTable(bool refreshExisting = false)
        {
            _dataTableView ??= _viewFactory.CreateDataTable();

            if (!TryNavigateToContent(
                _dataTableView,
                "الضمانات",
                "محفظة الضمانات اليومية مع معاينة جانبية وسياق تشغيلي سريع.",
                "Portfolio",
                "تم فتح الضمانات."))
            {
                return;
            }

            if (refreshExisting)
            {
                _dataTableView.RefreshView();
            }
        }

        public void ShowOperationCenter(bool refreshExisting = false, int? requestIdToFocus = null)
        {
            _operationCenterView ??= _viewFactory.CreateOperationCenter();
            _operationCenterView.SetRequestFocus(requestIdToFocus);

            if (!TryNavigateToContent(
                _operationCenterView,
                "الطلبات",
                "متابعة الطلبات البنكية وحالتها والوثائق المرتبطة بها.",
                "BankRoom",
                requestIdToFocus.HasValue
                    ? "تم فتح الطلبات وتحديد الطلب المطلوب."
                    : "تم فتح الطلبات."))
            {
                return;
            }

            if (refreshExisting || requestIdToFocus.HasValue)
            {
                _operationCenterView.RefreshView();
            }
        }

        public void ShowSettings(bool refreshExisting = false)
        {
            _settingsView ??= _viewFactory.CreateSettings();

            if (!TryNavigateToContent(
                _settingsView,
                "الإعدادات",
                "المجلدات، النسخ الاحتياطي، ومؤشرات التشغيل المحلية.",
                "Administration",
                "تم فتح الإعدادات."))
            {
                return;
            }

            if (refreshExisting)
            {
                _settingsView.RefreshView();
            }
        }

        public void ShowAddEntryScreen(bool resetExisting = false)
        {
            if (resetExisting || _addEntryView == null)
            {
                _addEntryView = _viewFactory.CreateAddEntry(returnTarget: GuaranteeFormReturnTarget.AddEntry);
            }

            _ = TryNavigateToContent(
                _addEntryView,
                "الرئيسية",
                "المساحة الرئيسية لإدخال الضمانات الجديدة بشكل موحد وواضح.",
                "Today",
                "تم فتح الرئيسية.");
        }

        public void ShowAddEntry(GuaranteeFormReturnTarget returnTarget = GuaranteeFormReturnTarget.DataTable)
        {
            object addEntryView = _viewFactory.CreateAddEntry(returnTarget: returnTarget);

            if (!TryNavigateToContent(
                addEntryView,
                "الرئيسية",
                "المساحة الرئيسية لإدخال الضمانات الجديدة بشكل موحد وواضح.",
                "Today",
                "تم تجهيز نموذج ضمان جديد."))
            {
                return;
            }

            _addEntryView = addEntryView;
        }

        public void ShowEditGuarantee(Guarantee guarantee, GuaranteeFormReturnTarget returnTarget = GuaranteeFormReturnTarget.DataTable)
        {
            object addEntryView = _viewFactory.CreateAddEntry(guarantee, returnTarget);

            if (!TryNavigateToContent(
                addEntryView,
                "تعديل الضمان",
                $"مراجعة وتحديث بيانات الضمان رقم {guarantee.GuaranteeNo}.",
                "Today",
                $"تم فتح تعديل الضمان رقم {guarantee.GuaranteeNo}."))
            {
                return;
            }

            _addEntryView = addEntryView;
        }

        public void ShowGuaranteeFile(
            Guarantee guarantee,
            string? sourceLabel = null,
            bool refreshExisting = false,
            GuaranteeFileFocusArea focusArea = GuaranteeFileFocusArea.None,
            int? requestIdToFocus = null)
        {
            if (!CanNavigateAway())
            {
                SetStatus("تم إلغاء التنقل بسبب وجود تغييرات غير محفوظة.", ShellStatusTone.Warning);
                return;
            }

            _guaranteeFileView ??= _viewFactory.CreateGuaranteeFile();
            _guaranteeFileView.SetRequestFocus(requestIdToFocus);
            _guaranteeFileView.LoadGuarantee(guarantee, userInitiated: refreshExisting);
            RecordLastGuaranteeContext(guarantee, sourceLabel ?? "ملف الضمان");

            SetCurrentWorkspace(
                _guaranteeFileView,
                "ملف الضمان",
                "الرؤية الكاملة للملف: الملخص، التسلسل الزمني، المرفقات، والطلبات.",
                null,
                $"تم فتح ملف الضمان رقم {guarantee.GuaranteeNo}.");

            if (focusArea != GuaranteeFileFocusArea.None)
            {
                _guaranteeFileView.FocusSection(focusArea);
            }
        }

        private bool TryNavigateToContent(object? content, string workspaceName, string description, string? navKey, string statusMessage)
        {
            if (content == null)
            {
                return false;
            }

            if (!CanNavigateAway())
            {
                SetStatus("تم إلغاء التنقل بسبب وجود تغييرات غير محفوظة.", ShellStatusTone.Warning);
                return false;
            }

            SetCurrentWorkspace(content, workspaceName, description, navKey, statusMessage);
            return true;
        }

        private void SetCurrentWorkspace(object content, string workspaceName, string description, string? navKey, string statusMessage)
        {
            _currentContent = content;
            OnPropertyChanged(nameof(CurrentContent));
            CurrentWorkspaceName = workspaceName;
            CurrentWorkspaceDescription = description;
            ActiveNavigationKey = navKey;
            SetStatus(statusMessage, ShellStatusTone.Info);
        }

        private void RecordLastGuaranteeContext(Guarantee guarantee, string sourceLabel)
        {
            _lastOpenedGuaranteeId = guarantee.Id;
            _lastOpenedGuaranteeTitle = $"ملف الضمان {guarantee.GuaranteeNo}";
            _lastOpenedGuaranteeSummary = $"{guarantee.Supplier} | {guarantee.Bank} | {sourceLabel}";
            RaiseLastFileStateChanged();
        }

        private void RaiseLastFileStateChanged()
        {
            OnPropertyChanged(nameof(HasLastFile));
            OnPropertyChanged(nameof(LastFileToolTip));
            _resumeLastFileCommand.RaiseCanExecuteChanged();
        }

        private void ExecuteUnifiedSearch()
        {
            string query = SearchQuery?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(query))
            {
                SetStatus("أدخل عبارة بحث أولاً.", ShellStatusTone.Warning);
                return;
            }

            if (WorkspaceAliases.TryGetValue(query, out string? workspaceKey))
            {
                NavigateByKey(workspaceKey);
                return;
            }

            Guarantee? exact = _databaseService.GetCurrentGuaranteeByNo(query);
            if (exact != null)
            {
                ShowGuaranteeFile(exact, "البحث الموحد", true);
                return;
            }

            if (_databaseService.QueryGuarantees(new GuaranteeQueryOptions
                {
                    SearchText = query,
                    IncludeAttachments = false,
                    Limit = 1,
                    SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
                }).Any())
            {
                ShowDataTable();
                _dataTableView?.ApplyShellSearch(query);
                SetStatus($"تم توجيه البحث إلى شاشة الضمانات باستخدام العبارة: {query}", ShellStatusTone.Info);
                return;
            }

            if (_databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    SearchText = query,
                    Limit = 1,
                    SortMode = WorkflowRequestQuerySortMode.RequestDateDescending
                }).Any())
            {
                ShowOperationCenter();
                _operationCenterView?.ApplyShellSearch(query);
                SetStatus($"تم توجيه البحث إلى شاشة الطلبات باستخدام العبارة: {query}", ShellStatusTone.Info);
                return;
            }

            SetStatus($"لم يتم العثور على نتائج لعبارة: {query}", ShellStatusTone.Warning);
        }

        private void NavigateByKey(string? workspaceKey)
        {
            switch (workspaceKey)
            {
                case "Today":
                    ShowAddEntryScreen();
                    break;
                case "Reception":
                    ShowTodayDesk();
                    break;
                case "Portfolio":
                    ShowDataTable();
                    break;
                case "BankRoom":
                    ShowOperationCenter();
                    break;
                case "Administration":
                    ShowSettings();
                    break;
            }
        }

        private void ResumeLastFile()
        {
            if (!_lastOpenedGuaranteeId.HasValue)
            {
                SetStatus("لا يوجد ملف أخير لاستئنافه.", ShellStatusTone.Warning);
                return;
            }

            Guarantee? guarantee = _databaseService.GetGuaranteeById(_lastOpenedGuaranteeId.Value);
            if (guarantee == null)
            {
                SetStatus("تعذر العثور على الملف الأخير.", ShellStatusTone.Warning);
                return;
            }

            ShowGuaranteeFile(guarantee, "آخر ملف", true);
        }
    }
}
