using System;
using System.Collections.Generic;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Services;

namespace GuaranteeManager.ViewModels
{
    public sealed class DataTableViewModel : ViewModelBase
    {
        private const string AllBanks = "كل البنوك";
        private const string AllTypes = "كل الأنواع";
        private const string AllStatuses = "كل الحالات";

        private readonly IDatabaseService _databaseService;

        private IReadOnlyList<Guarantee> _filteredGuarantees = Array.Empty<Guarantee>();
        private IReadOnlyList<string> _bankOptions = new[] { AllBanks };
        private IReadOnlyList<string> _typeOptions = new[] { AllTypes };
        private IReadOnlyList<string> _statusOptions = new[] { AllStatuses, "نشط", "قريب الانتهاء", "منتهي" };
        private string _searchText = string.Empty;
        private string _selectedBank = AllBanks;
        private string _selectedType = AllTypes;
        private string _selectedStatus = AllStatuses;
        private string _guaranteeCount = "0";
        private string _activeCount = "0";
        private string _pendingRequestCount = "0";
        private string _portfolioSummary = "اختر سجلًا لعرض اللوح الجانبي المختصر أو افتح الملف الكامل.";
        private bool _hasFilteredGuarantees;
        private Guarantee? _selectedGuarantee;
        private int _totalGuaranteeCount;

        public DataTableViewModel(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public IReadOnlyList<Guarantee> FilteredGuarantees
        {
            get => _filteredGuarantees;
            private set => SetProperty(ref _filteredGuarantees, value);
        }

        public IReadOnlyList<string> BankOptions
        {
            get => _bankOptions;
            private set => SetProperty(ref _bankOptions, value);
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

        public string SelectedBank
        {
            get => _selectedBank;
            private set => SetProperty(ref _selectedBank, value);
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

        public string GuaranteeCount
        {
            get => _guaranteeCount;
            private set => SetProperty(ref _guaranteeCount, value);
        }

        public string ActiveCount
        {
            get => _activeCount;
            private set => SetProperty(ref _activeCount, value);
        }

        public string PendingRequestCount
        {
            get => _pendingRequestCount;
            private set => SetProperty(ref _pendingRequestCount, value);
        }

        public string PortfolioSummary
        {
            get => _portfolioSummary;
            private set => SetProperty(ref _portfolioSummary, value);
        }

        public bool HasFilteredGuarantees
        {
            get => _hasFilteredGuarantees;
            private set => SetProperty(ref _hasFilteredGuarantees, value);
        }

        public Guarantee? SelectedGuarantee
        {
            get => _selectedGuarantee;
            set => SetProperty(ref _selectedGuarantee, value);
        }

        public void Refresh()
        {
            string currentSearch = SearchText;
            string currentBank = SelectedBank;
            string currentType = SelectedType;
            string currentStatus = SelectedStatus;

            _totalGuaranteeCount = _databaseService.CountGuarantees();
            BankOptions = BuildOptions(_databaseService.GetUniqueValues("Bank"), AllBanks);
            TypeOptions = BuildOptions(_databaseService.GetUniqueValues("GuaranteeType"), AllTypes);
            StatusOptions = new[] { AllStatuses, "نشط", "قريب الانتهاء", "منتهي" };

            SearchText = currentSearch;
            SelectedBank = EnsureSelection(currentBank, BankOptions, AllBanks);
            SelectedType = EnsureSelection(currentType, TypeOptions, AllTypes);
            SelectedStatus = EnsureSelection(currentStatus, StatusOptions, AllStatuses);

            ApplyCurrentFilters();
        }

        public void ApplyShellSearch(string query)
        {
            SearchText = query?.Trim() ?? string.Empty;
            ApplyCurrentFilters();
        }

        public void ApplyFilters(string? search, string? bank, string? type, string? status)
        {
            SearchText = search?.Trim() ?? string.Empty;
            SelectedBank = EnsureSelection(bank, BankOptions, AllBanks);
            SelectedType = EnsureSelection(type, TypeOptions, AllTypes);
            SelectedStatus = EnsureSelection(status, StatusOptions, AllStatuses);
            ApplyCurrentFilters();
        }

        public void ClearFilters()
        {
            SearchText = string.Empty;
            SelectedBank = AllBanks;
            SelectedType = AllTypes;
            SelectedStatus = AllStatuses;
            ApplyCurrentFilters();
        }

        private void ApplyCurrentFilters()
        {
            int? selectedId = SelectedGuarantee?.Id;
            GuaranteeQueryOptions options = CreateCurrentQueryOptions();
            List<Guarantee> filtered = _databaseService.QueryGuarantees(options);

            FilteredGuarantees = filtered;
            GuaranteeCount = filtered.Count.ToString();
            ActiveCount = filtered.Count(item => item.StatusLabel == "نشط").ToString();
            PendingRequestCount = _databaseService.GetPendingWorkflowRequestCount().ToString();
            PortfolioSummary = filtered.Count == _totalGuaranteeCount
                ? $"جميع الضمانات المعروضة الآن: {_totalGuaranteeCount}."
                : $"النتائج الحالية: {filtered.Count} من أصل {_totalGuaranteeCount}.";
            HasFilteredGuarantees = filtered.Count > 0;
            SelectedGuarantee = filtered.FirstOrDefault(item => item.Id == selectedId) ?? filtered.FirstOrDefault();
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

        public List<Guarantee> GetGuaranteesByBank(string bank) =>
            _databaseService.QueryGuarantees(new GuaranteeQueryOptions
            {
                Bank = bank,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            });

        public List<Guarantee> GetGuaranteesBySupplier(string supplier) =>
            _databaseService.QueryGuarantees(new GuaranteeQueryOptions
            {
                Supplier = supplier,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            });

        public List<Guarantee> GetGuaranteesByTemporalStatus(GuaranteeTimeStatus temporalStatus) =>
            _databaseService.QueryGuarantees(new GuaranteeQueryOptions
            {
                TimeStatus = temporalStatus,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            });

        public List<Guarantee> GetSortedHistory(int guaranteeId) =>
            _databaseService.GetGuaranteeHistory(guaranteeId)
                .OrderByDescending(g => g.VersionNumber)
                .ThenByDescending(g => g.CreatedAt)
                .ToList();

        public ContextActionAvailability GetContextActionAvailability(string? actionId, Guarantee guarantee) =>
            actionId switch
            {
                "evidence.attachments" when guarantee.Attachments.Count == 0 =>
                    ContextActionAvailability.Disabled("لا توجد مرفقات لهذا الضمان."),
                "export.same-bank" when string.IsNullOrWhiteSpace(guarantee.Bank) =>
                    ContextActionAvailability.Disabled("لا يوجد بنك مرتبط بهذا السجل."),
                "export.same-supplier" when string.IsNullOrWhiteSpace(guarantee.Supplier) =>
                    ContextActionAvailability.Disabled("لا يوجد مورد مرتبط بهذا السجل."),
                "execute.create-extension" when guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active =>
                    ContextActionAvailability.Disabled("طلب التمديد متاح للضمانات النشطة فقط."),
                "execute.create-extension" when _databaseService.HasPendingWorkflowRequest(guarantee.RootId ?? guarantee.Id, RequestType.Extension) =>
                    ContextActionAvailability.Disabled("يوجد بالفعل طلب تمديد معلق لهذا الضمان."),
                "execute.create-reduction" when guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active || guarantee.Amount <= 0 =>
                    ContextActionAvailability.Disabled("طلب التخفيض متاح للضمانات النشطة التي يزيد مبلغها على صفر."),
                "execute.create-reduction" when _databaseService.HasPendingWorkflowRequest(guarantee.RootId ?? guarantee.Id, RequestType.Reduction) =>
                    ContextActionAvailability.Disabled("يوجد بالفعل طلب تخفيض معلق لهذا الضمان."),
                "execute.create-release" when guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active =>
                    ContextActionAvailability.Disabled("طلب الإفراج متاح للضمانات النشطة فقط."),
                "execute.create-release" when _databaseService.HasPendingWorkflowRequest(guarantee.RootId ?? guarantee.Id, RequestType.Release) =>
                    ContextActionAvailability.Disabled("يوجد بالفعل طلب إفراج معلق لهذا الضمان."),
                "execute.create-liquidation" when guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active =>
                    ContextActionAvailability.Disabled("طلب التسييل متاح للضمانات النشطة فقط."),
                "execute.create-liquidation" when _databaseService.HasPendingWorkflowRequest(guarantee.RootId ?? guarantee.Id, RequestType.Liquidation) =>
                    ContextActionAvailability.Disabled("يوجد بالفعل طلب تسييل معلق لهذا الضمان."),
                "execute.create-verification" when guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active =>
                    ContextActionAvailability.Disabled("طلب التحقق متاح للضمانات النشطة فقط."),
                "execute.create-verification" when _databaseService.HasPendingWorkflowRequest(guarantee.RootId ?? guarantee.Id, RequestType.Verification) =>
                    ContextActionAvailability.Disabled("يوجد بالفعل طلب تحقق معلق لهذا الضمان."),
                "execute.create-replacement" when guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active =>
                    ContextActionAvailability.Disabled("طلب الاستبدال متاح للضمانات النشطة فقط."),
                "execute.create-replacement" when _databaseService.HasPendingWorkflowRequest(guarantee.RootId ?? guarantee.Id, RequestType.Replacement) =>
                    ContextActionAvailability.Disabled("يوجد بالفعل طلب استبدال معلق لهذا الضمان."),
                "execute.create-annulment" when guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Released && guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Liquidated =>
                    ContextActionAvailability.Disabled("طلب النقض متاح للضمانات المفرج عنها أو المسيّلة فقط."),
                "execute.create-annulment" when _databaseService.HasPendingWorkflowRequest(guarantee.RootId ?? guarantee.Id, RequestType.Annulment) =>
                    ContextActionAvailability.Disabled("يوجد بالفعل طلب نقض معلق لهذا الضمان."),
                "copy.guarantee-no" when string.IsNullOrWhiteSpace(guarantee.GuaranteeNo) =>
                    ContextActionAvailability.Disabled("رقم الضمان غير متاح."),
                "copy.supplier" when string.IsNullOrWhiteSpace(guarantee.Supplier) =>
                    ContextActionAvailability.Disabled("اسم المورد غير متاح."),
                "copy.reference-number" when string.IsNullOrWhiteSpace(guarantee.ReferenceNumber) =>
                    ContextActionAvailability.Disabled("رقم المرجع غير متاح."),
                "export.visible-list" when !HasFilteredGuarantees =>
                    ContextActionAvailability.Disabled("لا توجد نتائج معروضة حاليًا."),
                _ => ContextActionAvailability.Enabled()
            };

        private GuaranteeQueryOptions CreateCurrentQueryOptions()
        {
            return new GuaranteeQueryOptions
            {
                SearchText = SearchText,
                Bank = string.Equals(SelectedBank, AllBanks, StringComparison.OrdinalIgnoreCase) ? null : SelectedBank,
                GuaranteeType = string.Equals(SelectedType, AllTypes, StringComparison.OrdinalIgnoreCase) ? null : SelectedType,
                TimeStatus = SelectedStatus switch
                {
                    "نشط" => GuaranteeTimeStatus.Active,
                    "قريب الانتهاء" => GuaranteeTimeStatus.ExpiringSoon,
                    "منتهي" => GuaranteeTimeStatus.Expired,
                    _ => null
                },
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            };
        }
    }
}
