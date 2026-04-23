using System;
using System.Collections.Generic;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Services;

namespace GuaranteeManager.ViewModels
{
    public sealed class OperationCenterViewModel : ViewModelBase
    {
        private const string AllTypes = "كل الأنواع";
        private const string AllStatuses = "كل الحالات";

        private readonly IWorkflowService _workflowService;
        private readonly IDatabaseService _databaseService;

        private IReadOnlyList<WorkflowRequestListItem> _filteredRequests = Array.Empty<WorkflowRequestListItem>();
        private IReadOnlyList<string> _typeOptions = new[] { AllTypes };
        private IReadOnlyList<string> _statusOptions = new[] { AllStatuses };
        private string _searchText = string.Empty;
        private string _selectedType = AllTypes;
        private string _selectedStatus = AllStatuses;
        private string _requestCount = "0";
        private string _pendingCount = "0";
        private string _executedCount = "0";
        private string _operationSummary = "اختر طلبًا لمراجعة حالته ووثائقه من اللوح الجانبي.";
        private WorkflowRequestListItem? _selectedItem;
        private int _totalRequestCount;
        private int? _requestIdToFocus;
        private bool _resetFiltersForFocusedRequest;
        private int _eligibleExtensionCount;
        private int _eligibleReductionCount;
        private int _eligibleReleaseCount;
        private int _eligibleLiquidationCount;
        private int _eligibleVerificationCount;
        private int _eligibleReplacementCount;
        private int _eligibleAnnulmentCount;
        private string _requestCreationSummary = "ابدأ نوع الطلب أولًا، ثم اختر الضمان المؤهل من النافذة التالية.";

        public OperationCenterViewModel(IWorkflowService workflowService, IDatabaseService databaseService)
        {
            _workflowService = workflowService;
            _databaseService = databaseService;
        }

        public IReadOnlyList<WorkflowRequestListItem> FilteredRequests
        {
            get => _filteredRequests;
            private set => SetProperty(ref _filteredRequests, value);
        }

        public IReadOnlyList<string> TypeOptions
        {
            get => _typeOptions;
            private set => SetProperty(ref _typeOptions, value);
        }

        public IReadOnlyList<string> StatusOptions
        {
            get => _statusOptions;
            private set => SetProperty(ref _statusOptions, value);
        }

        public string SearchText
        {
            get => _searchText;
            private set => SetProperty(ref _searchText, value);
        }

        public string SelectedType
        {
            get => _selectedType;
            private set => SetProperty(ref _selectedType, value);
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            private set => SetProperty(ref _selectedStatus, value);
        }

        public string RequestCount
        {
            get => _requestCount;
            private set => SetProperty(ref _requestCount, value);
        }

        public string PendingCount
        {
            get => _pendingCount;
            private set => SetProperty(ref _pendingCount, value);
        }

        public string ExecutedCount
        {
            get => _executedCount;
            private set => SetProperty(ref _executedCount, value);
        }

        public string OperationSummary
        {
            get => _operationSummary;
            private set => SetProperty(ref _operationSummary, value);
        }

        public WorkflowRequestListItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (!SetProperty(ref _selectedItem, value))
                {
                    return;
                }

                OnPropertyChanged(nameof(HasSelectedRequest));
                OnPropertyChanged(nameof(CanOpenLetter));
                OnPropertyChanged(nameof(CanOpenResponse));
                OnPropertyChanged(nameof(CanAttachResponseDocument));
                OnPropertyChanged(nameof(CanUseResponseAction));
                OnPropertyChanged(nameof(ResponseActionLabel));
            }
        }

        public bool HasSelectedRequest => SelectedItem != null;

        public bool CanOpenLetter => SelectedItem?.Request.HasLetter == true;

        public bool CanOpenResponse => SelectedItem?.Request.HasResponseDocument == true;

        public bool CanAttachResponseDocument => SelectedItem != null
            && !SelectedItem.Request.HasResponseDocument;

        public bool CanUseResponseAction => SelectedItem != null;

        public string ResponseActionLabel => "مستند الرد";

        public string RequestCreationSummary
        {
            get => _requestCreationSummary;
            private set => SetProperty(ref _requestCreationSummary, value);
        }

        public void Refresh()
        {
            string currentSearch = SearchText;
            string currentType = SelectedType;
            string currentStatus = SelectedStatus;

            UpdateEligibleRequestCounts();
            _totalRequestCount = _databaseService.CountWorkflowRequests();
            TypeOptions = new[] { AllTypes }.Concat(Enum.GetValues<RequestType>().Select(GetTypeLabel)).ToList();
            StatusOptions = new[] { AllStatuses }.Concat(Enum.GetValues<RequestStatus>().Select(GetStatusLabel)).ToList();

            SearchText = currentSearch;
            SelectedType = EnsureSelection(currentType, TypeOptions, AllTypes);
            SelectedStatus = EnsureSelection(currentStatus, StatusOptions, AllStatuses);

            ApplyCurrentFilters();
        }

        public void SetRequestFocus(int? requestId)
        {
            _requestIdToFocus = requestId;
            _resetFiltersForFocusedRequest = requestId.HasValue;
        }

        public void ApplyShellSearch(string query)
        {
            SetRequestFocus(null);
            SearchText = query?.Trim() ?? string.Empty;
            ApplyCurrentFilters();
        }

        public void ApplyFilters(string? search, string? type, string? status)
        {
            SetRequestFocus(null);
            SearchText = search?.Trim() ?? string.Empty;
            SelectedType = EnsureSelection(type, TypeOptions, AllTypes);
            SelectedStatus = EnsureSelection(status, StatusOptions, AllStatuses);
            ApplyCurrentFilters();
        }

        public void ClearFilters()
        {
            SetRequestFocus(null);
            SearchText = string.Empty;
            SelectedType = AllTypes;
            SelectedStatus = AllStatuses;
            ApplyCurrentFilters();
        }

        private void ApplyCurrentFilters()
        {
            int? selectedRequestId = _requestIdToFocus ?? SelectedItem?.Request.Id;

            if (_resetFiltersForFocusedRequest)
            {
                SearchText = string.Empty;
                SelectedType = AllTypes;
                SelectedStatus = AllStatuses;
                _resetFiltersForFocusedRequest = false;
            }

            WorkflowRequestQueryOptions options = CreateCurrentQueryOptions();
            List<WorkflowRequestListItem> filtered = _databaseService.QueryWorkflowRequests(options);

            FilteredRequests = filtered;
            RequestCount = filtered.Count.ToString();
            PendingCount = filtered.Count(item => item.Request.Status == RequestStatus.Pending).ToString();
            ExecutedCount = filtered.Count(item => item.Request.Status == RequestStatus.Executed).ToString();
            OperationSummary = filtered.Count == _totalRequestCount
                ? $"جميع الطلبات المعروضة الآن: {_totalRequestCount}."
                : $"النتائج الحالية: {filtered.Count} من أصل {_totalRequestCount}.";
            SelectedItem = filtered.FirstOrDefault(item => item.Request.Id == selectedRequestId) ?? filtered.FirstOrDefault();
            _requestIdToFocus = null;
        }

        private static IReadOnlyList<string> BuildOptions(IEnumerable<string> values, string allLabel)
        {
            List<string> items = new() { allLabel };
            items.AddRange(values.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().OrderBy(item => item));
            return items;
        }

        private static string EnsureSelection(string? candidate, IEnumerable<string> options, string fallback)
        {
            return options.Contains(candidate) ? candidate! : fallback;
        }

        public Guarantee? GetCurrentGuaranteeForItem(WorkflowRequestListItem item) =>
            _databaseService.GetGuaranteeById(item.CurrentGuaranteeId);

        public List<WorkflowRequestListItem> GetPendingRequestsByType(RequestType type) =>
            _databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RequestType = type,
                RequestStatus = RequestStatus.Pending,
                SortMode = WorkflowRequestQuerySortMode.RequestDateAscending
            });

        public ContextActionAvailability GetContextActionAvailability(string? actionId, WorkflowRequestListItem item)
        {
            return actionId switch
            {
                "request.record-response" when item.Request.Status != RequestStatus.Pending =>
                    ContextActionAvailability.Disabled("تسجيل الاستجابة متاح للطلبات المعلقة فقط."),
                "request.open-letter" when item.Request.HasLetter != true =>
                    ContextActionAvailability.Disabled("لا يوجد خطاب طلب محفوظ لهذا السجل."),
                "request.open-current-guarantee" when _databaseService.GetGuaranteeById(item.CurrentGuaranteeId) == null =>
                    ContextActionAvailability.Disabled("تعذر العثور على ملف الضمان المرتبط."),
                "request.open-history" when _databaseService.GetGuaranteeById(item.CurrentGuaranteeId) == null =>
                    ContextActionAvailability.Disabled("تعذر العثور على ملف الضمان المرتبط."),
                "request.export-pending-same-type" when _databaseService.CountWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RequestType = item.Request.Type,
                    RequestStatus = RequestStatus.Pending
                }) == 0 =>
                    ContextActionAvailability.Disabled("لا توجد طلبات معلقة من نفس النوع حاليًا."),
                "request.export-pending-extension" when _databaseService.CountWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RequestType = RequestType.Extension,
                    RequestStatus = RequestStatus.Pending
                }) == 0 =>
                    ContextActionAvailability.Disabled("لا توجد طلبات تمديد معلقة حاليًا."),
                "request.export-pending-reduction" when _databaseService.CountWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RequestType = RequestType.Reduction,
                    RequestStatus = RequestStatus.Pending
                }) == 0 =>
                    ContextActionAvailability.Disabled("لا توجد طلبات تخفيض معلقة حاليًا."),
                "request.export-pending-release" when _databaseService.CountWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RequestType = RequestType.Release,
                    RequestStatus = RequestStatus.Pending
                }) == 0 =>
                    ContextActionAvailability.Disabled("لا توجد طلبات إفراج معلقة حاليًا."),
                "request.export-pending-liquidation" when _databaseService.CountWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RequestType = RequestType.Liquidation,
                    RequestStatus = RequestStatus.Pending
                }) == 0 =>
                    ContextActionAvailability.Disabled("لا توجد طلبات تسييل معلقة حاليًا."),
                "request.export-pending-verification" when _databaseService.CountWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RequestType = RequestType.Verification,
                    RequestStatus = RequestStatus.Pending
                }) == 0 =>
                    ContextActionAvailability.Disabled("لا توجد طلبات تحقق معلقة حاليًا."),
                "request.export-pending-replacement" when _databaseService.CountWorkflowRequests(new WorkflowRequestQueryOptions
                {
                    RequestType = RequestType.Replacement,
                    RequestStatus = RequestStatus.Pending
                }) == 0 =>
                    ContextActionAvailability.Disabled("لا توجد طلبات استبدال معلقة حاليًا."),
                "workspace.request.copy-guarantee-no" when string.IsNullOrWhiteSpace(item.GuaranteeNo) =>
                    ContextActionAvailability.Disabled("رقم الضمان غير متاح."),
                "workspace.request.copy-supplier" when string.IsNullOrWhiteSpace(item.Supplier) =>
                    ContextActionAvailability.Disabled("اسم المورد غير متاح."),
                _ => ContextActionAvailability.Enabled()
            };
        }

        public ContextActionAvailability GetRequestCreationAvailability(string? actionId)
        {
            return actionId switch
            {
                "execute.create-extension" when _eligibleExtensionCount == 0 =>
                    ContextActionAvailability.Disabled("لا توجد ضمانات نشطة مؤهلة الآن لطلب التمديد."),
                "execute.create-reduction" when _eligibleReductionCount == 0 =>
                    ContextActionAvailability.Disabled("لا توجد ضمانات نشطة ذات مبلغ قابل للتخفيض الآن."),
                "execute.create-release" when _eligibleReleaseCount == 0 =>
                    ContextActionAvailability.Disabled("لا توجد ضمانات نشطة مؤهلة الآن لطلب الإفراج."),
                "execute.create-liquidation" when _eligibleLiquidationCount == 0 =>
                    ContextActionAvailability.Disabled("لا توجد ضمانات نشطة مؤهلة الآن لطلب التسييل."),
                "execute.create-verification" when _eligibleVerificationCount == 0 =>
                    ContextActionAvailability.Disabled("لا توجد ضمانات نشطة مؤهلة الآن لطلب التحقق."),
                "execute.create-replacement" when _eligibleReplacementCount == 0 =>
                    ContextActionAvailability.Disabled("لا توجد ضمانات نشطة مؤهلة الآن لطلب الاستبدال."),
                "execute.create-annulment" when _eligibleAnnulmentCount == 0 =>
                    ContextActionAvailability.Disabled("لا توجد ضمانات مفرج عنها أو مسيّلة متاحة الآن لطلب النقض."),
                _ => ContextActionAvailability.Enabled()
            };
        }

        private WorkflowRequestQueryOptions CreateCurrentQueryOptions()
        {
            return new WorkflowRequestQueryOptions
            {
                SearchText = SearchText,
                RequestType = string.Equals(SelectedType, AllTypes, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : ParseTypeLabel(SelectedType),
                RequestStatus = string.Equals(SelectedStatus, AllStatuses, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : ParseStatusLabel(SelectedStatus),
                SortMode = WorkflowRequestQuerySortMode.RequestDateDescending
            };
        }

        private void UpdateEligibleRequestCounts()
        {
            _eligibleExtensionCount = _workflowService.GetGuaranteesEligibleForExtension().Count;
            _eligibleReductionCount = _workflowService.GetGuaranteesEligibleForReduction().Count;
            _eligibleReleaseCount = _workflowService.GetGuaranteesEligibleForRelease().Count;
            _eligibleLiquidationCount = _workflowService.GetGuaranteesEligibleForLiquidation().Count;
            _eligibleVerificationCount = _workflowService.GetGuaranteesEligibleForVerification().Count;
            _eligibleReplacementCount = _workflowService.GetGuaranteesEligibleForReplacement().Count;
            _eligibleAnnulmentCount = _workflowService.GetGuaranteesEligibleForAnnulment()
                .Count(item => !_databaseService.HasPendingWorkflowRequest(item.RootId ?? item.Id, RequestType.Annulment));

            List<string> availableTypes = new();
            if (_eligibleExtensionCount > 0)
            {
                availableTypes.Add("تمديد");
            }

            if (_eligibleReductionCount > 0)
            {
                availableTypes.Add("تخفيض");
            }

            if (_eligibleReleaseCount > 0)
            {
                availableTypes.Add("إفراج");
            }

            if (_eligibleLiquidationCount > 0)
            {
                availableTypes.Add("تسييل");
            }

            if (_eligibleVerificationCount > 0)
            {
                availableTypes.Add("تحقق");
            }

            if (_eligibleReplacementCount > 0)
            {
                availableTypes.Add("استبدال");
            }

            if (_eligibleAnnulmentCount > 0)
            {
                availableTypes.Add("نقض");
            }

            RequestCreationSummary = availableTypes.Count == 0
                ? "لا توجد الآن ضمانات مؤهلة لهذا المسار. جرّب من ملف ضمان محدد أو بعد تحديث الحالات الحالية."
                : $"اختر نوع الطلب، ثم اختر الضمان من القائمة المؤهلة. الأنواع المتاحة الآن: {string.Join("، ", availableTypes)}.";
        }

        private static string GetTypeLabel(RequestType type)
        {
            return new WorkflowRequest { Type = type }.TypeLabel;
        }

        private static string GetStatusLabel(RequestStatus status)
        {
            return new WorkflowRequest { Status = status }.StatusLabel;
        }

        private static RequestType? ParseTypeLabel(string label)
        {
            foreach (RequestType type in Enum.GetValues<RequestType>())
            {
                if (string.Equals(GetTypeLabel(type), label, StringComparison.Ordinal))
                {
                    return type;
                }
            }

            return null;
        }

        private static RequestStatus? ParseStatusLabel(string label)
        {
            foreach (RequestStatus status in Enum.GetValues<RequestStatus>())
            {
                if (string.Equals(GetStatusLabel(status), label, StringComparison.Ordinal))
                {
                    return status;
                }
            }

            return null;
        }
    }
}
