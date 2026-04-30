using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager
{
    public sealed partial class ShellViewModel
    {
        private const int PageSize = 10;

        private readonly IDatabaseService _database;
        private readonly GuaranteeWorkspaceCoordinator _guaranteeWorkspace;
        private readonly GuaranteeWorkspaceDataService _guaranteeData;
        private readonly INavigationGuard _navigationGuard;
        private readonly IShellStatusService _shellStatus;
        private readonly IUiDiagnosticsService _diagnostics;
        private readonly ShellSessionCoordinator _sessionCoordinator;
        private readonly ShellWorkspaceFactory _workspaceFactory;
        private GuaranteeRow? _selectedGuarantee;
        private GuaranteeRow? _selectedTableGuarantee;
        private int? _focusedGuaranteeRequestId;
        private int _guaranteeFocusRequestVersion;
        private GuaranteeFocusArea _currentGuaranteeFocusArea = GuaranteeFocusArea.None;
        private OperationalInquiryResult? _latestInquiryResult;
        private GuaranteeOutputPreviewItem? _latestLetterOutput;
        private GuaranteeOutputPreviewItem? _latestResponseOutput;
        private OperationalInquiryOption? _selectedOperationalInquiryOption;
        private string _selectedOperationalInquiryDescription = "اختر سؤالاً تشغيليًا لعرض جواب مدعوم بالأدلة.";
        private string _pendingRequestCount = "0";
        private string _pendingRequestMeta = "إجمالي القيمة 0 ريال";
        private string _expiredCount = "0";
        private string _expiredMeta = "إجمالي القيمة 0 ريال";
        private string _expiredFollowUpCount = "0";
        private string _expiredFollowUpMeta = "إجمالي القيمة 0 ريال";
        private string _expiringSoonCount = "0";
        private string _expiringSoonMeta = "إجمالي القيمة 0 ريال";
        private string _activeCount = "0";
        private string _activeMeta = "إجمالي القيمة 0 ريال";
        private string _footerSummary = "لا توجد عناصر";
        private string _searchText = string.Empty;
        private string _globalSearchText = string.Empty;
        private int _currentGuaranteePage = 1;
        private int _totalGuaranteePages = 1;
        private FilterOption _selectedTimeStatus = FilterOption.AllTimeStatuses;
        private string _selectedBank = AllBanksLabel;
        private string _selectedGuaranteeType = AllTypesLabel;
        private string _currentWorkspaceKey = ShellWorkspaceKeys.Dashboard;
        private FrameworkElement? _activeWorkspaceContent;
        private ShellLastFileState _lastFileState = ShellLastFileState.Empty;
        private bool _trackSelectedGuaranteeAsLastFile = true;

        public ObservableCollection<GuaranteeRow> Guarantees { get; } = new();
        public ObservableCollection<TimelineItem> Timeline { get; } = new();
        public ObservableCollection<AttachmentItem> Attachments { get; } = new();
        public ObservableCollection<GuaranteeRequestPreviewItem> GuaranteeRequestsPreview { get; } = new();
        public ObservableCollection<GuaranteeOutputPreviewItem> GuaranteeOutputsPreview { get; } = new();
        public string GuaranteeRequestsSummaryText => _focusedGuaranteeRequestId.HasValue
            ? "ابدأ بالطلب الذي فتح هذا الملف الآن، ثم راجع أحدث الطلبات في نفس السلسلة قبل تنفيذ الخطوة التالية."
            : GuaranteeRequestsPreview.Count == 0
                ? "لا توجد طلبات مرتبطة تغيّر القرار الآن. إذا كنت تراجع الأثر فقط فانتقل إلى الخط الزمني أو المرفقات."
                : $"هذه هي الطلبات الأقرب للتنفيذ الآن في نفس السلسلة. يظهر هنا آخر {GuaranteeRequestsPreview.Count.ToString("N0", CultureInfo.InvariantCulture)} طلبات مرتبطة.";
        public string GuaranteeRequestsContextLabel => _focusedGuaranteeRequestId.HasValue
            ? "الطلب المفتوح الآن"
            : GuaranteeRequestsPreview.Count == 0
                ? "لا توجد حركة حالية"
                : "الأقرب للتنفيذ الآن";
        public string TimelineSummaryText => Timeline.Count == 0
            ? "سجل المراحل الرئيسية لهذا الضمان سيظهر هنا عند توفر طلبات أو أحداث موثقة."
            : SelectedGuarantee?.IsCurrentVersion == false
                ? "يعرض هذا القسم أحداث هذا الإصدار بترتيبها من الأقدم إلى الأحدث."
            : "يعرض هذا التسلسل أحداث الضمان منذ الإنشاء وحتى أحدث حدث موثق.";
        public string TimelineStationsLabel => $"{Timeline.Count.ToString("N0", CultureInfo.InvariantCulture)} محطات مرتبطة";
        public string OutputsSummaryText
        {
            get
            {
                int outputCount = GuaranteeOutputsPreview.Count;
                int letterCount = SelectedGuarantee?.ActionProfile.LetterOutputCount
                    ?? GuaranteeOutputsPreview.Count(item => item.CanOpenLetter);
                int responseCount = SelectedGuarantee?.ActionProfile.ResponseOutputCount
                    ?? GuaranteeOutputsPreview.Count(item => item.CanOpenResponse);

                return outputCount == 0
                    ? "لا تظهر هنا إلا المخرجات الجاهزة للفتح الآن. عند عدم وجودها تبقى المراجعة عبر الطلبات أو المرفقات الرسمية."
                    : $"تظهر هنا المستندات الناتجة عن الطلبات والجاهزة للفتح الآن: {letterCount.ToString("N0", CultureInfo.InvariantCulture)} خطاب طلب و{responseCount.ToString("N0", CultureInfo.InvariantCulture)} رد بنك.";
            }
        }

        public string OutputsAvailabilityLabel => GuaranteeOutputsPreview.Count == 0
            ? "لا توجد ملفات جاهزة"
            : $"{GuaranteeOutputsPreview.Count.ToString("N0", CultureInfo.InvariantCulture)} مخرجات جاهزة";
        public string AttachmentsSummaryText => Attachments.Count == 0
            ? "لا توجد مرفقات رسمية مجمعة على هذا الملف حاليًا."
            : SelectedGuarantee?.IsCurrentVersion == false
                ? $"يوجد {FormatOfficialAttachmentCount(Attachments.Count)} على الإصدار {SelectedGuarantee.VersionLabel}."
            : $"يوجد {FormatOfficialAttachmentCount(Attachments.Count)} عبر كل إصدارات الملف.";
        public bool HasOutputShortcuts => HasLatestLetterOutput || HasLatestResponseOutput;
        public string OutputShortcutsSummaryText => HasOutputShortcuts
            ? "اختصارات آخر المستندات الناتجة"
            : "لا توجد اختصارات لمستندات ناتجة الآن";
        public int GuaranteeFocusRequestVersion => _guaranteeFocusRequestVersion;
        public GuaranteeFocusArea CurrentGuaranteeFocusArea => _currentGuaranteeFocusArea;
        public int? CurrentGuaranteeFocusRequestId => _focusedGuaranteeRequestId;
        public ObservableCollection<FilterOption> TimeStatusOptions { get; } = new();
        public ObservableCollection<string> BankOptions { get; } = new();
        public ObservableCollection<string> GuaranteeTypeOptions { get; } = new();
        public ObservableCollection<ReferenceTablePagerButtonItem> GuaranteePagerButtons { get; } = new();
        public ObservableCollection<OperationalInquiryOption> OperationalInquiryOptions { get; } = new();
        public ICommand CreateNewGuaranteeCommand { get; }
        public ICommand EditGuaranteeCommand { get; }
        public ICommand PreviousGuaranteePageCommand { get; }
        public ICommand NextGuaranteePageCommand { get; }
        public ICommand GoToGuaranteePageCommand { get; }
        public ICommand SelectGuaranteeCommand { get; }
        public ICommand CreateExtensionRequestCommand { get; }
        public ICommand CreateReleaseRequestCommand { get; }
        public ICommand CreateReductionRequestCommand { get; }
        public ICommand CreateLiquidationRequestCommand { get; }
        public ICommand CreateVerificationRequestCommand { get; }
        public ICommand CreateReplacementRequestCommand { get; }
        public ICommand RegisterBankResponseCommand { get; }
        public ICommand CopyGuaranteeNoCommand { get; }
        public ICommand CopyGuaranteeSupplierCommand { get; }
        public ICommand CopyGuaranteeReferenceTypeCommand { get; }
        public ICommand CopyGuaranteeReferenceNumberCommand { get; }
        public ICommand CopyGuaranteeTypeCommand { get; }
        public ICommand CopyGuaranteeIssueDateCommand { get; }
        public ICommand CopyGuaranteeExpiryDateCommand { get; }
        public ICommand OpenAttachmentCommand { get; }
        public ICommand OpenTimelineEvidenceCommand { get; }
        public ICommand OpenOutputLetterCommand { get; }
        public ICommand OpenOutputResponseCommand { get; }
        public ICommand ShowAllAttachmentsCommand { get; }
        public ICommand ResumeLastFileCommand { get; }
        public ICommand ShowDashboardCommand { get; }
        public ICommand ShowGuaranteesCommand { get; }
        public ICommand ShowBanksCommand { get; }
        public ICommand ShowReportsCommand { get; }
        public ICommand ShowSettingsCommand { get; }
        public ICommand ExecuteGlobalSearchCommand { get; }
        public ICommand ExitCommand { get; }

        private const string AllBanksLabel = "كل البنوك";
        private const string AllTypesLabel = "كل الأنواع";

        public GuaranteeRow? SelectedGuarantee
        {
            get => _selectedGuarantee;
            set
            {
                if (ReferenceEquals(_selectedGuarantee, value))
                {
                    return;
                }

                if (_selectedGuarantee?.RootId != value?.RootId)
                {
                    LatestInquiryResult = null;
                }

                _selectedGuarantee = value;
                SynchronizeSelectedTableGuarantee(value);
                if (_focusedGuaranteeRequestId.HasValue)
                {
                    _focusedGuaranteeRequestId = null;
                    RaiseGuaranteeContextSectionTextProperties();
                }

                OnPropertyChanged();
                if (_trackSelectedGuaranteeAsLastFile && value != null)
                {
                    RememberLastFile(value);
                }
                RefreshSelectedGuaranteeArtifacts();
                UpdateSelectedOperationalInquiryDescription();
                RaiseSelectionCommandStates();
            }
        }

        public GuaranteeRow? SelectedTableGuarantee
        {
            get => _selectedTableGuarantee;
            set
            {
                if (ReferenceEquals(_selectedTableGuarantee, value))
                {
                    return;
                }

                _selectedTableGuarantee = value;
                OnPropertyChanged();
                if (value != null
                    && !ReferenceEquals(_selectedGuarantee, value)
                    && (_selectedGuarantee == null
                        || _selectedGuarantee.IsCurrentVersion
                        || _selectedGuarantee.RootId != value.RootId))
                {
                    SelectedGuarantee = value;
                }
            }
        }

        public OperationalInquiryOption? SelectedOperationalInquiryOption
        {
            get => _selectedOperationalInquiryOption;
            set
            {
                if (ReferenceEquals(_selectedOperationalInquiryOption, value))
                {
                    return;
                }

                _selectedOperationalInquiryOption = value;
                OnPropertyChanged();
                UpdateSelectedOperationalInquiryDescription();
                RaiseSelectionCommandStates();
            }
        }

        public OperationalInquiryResult? LatestInquiryResult
        {
            get => _latestInquiryResult;
            private set
            {
                if (ReferenceEquals(_latestInquiryResult, value))
                {
                    return;
                }

                _latestInquiryResult = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasLatestInquiryResult));
                OnPropertyChanged(nameof(HasLatestInquirySuggestedSection));
                OnPropertyChanged(nameof(LatestInquirySuggestedSectionLabel));
                RaiseInquiryCommandStates();
            }
        }

        public bool HasLatestInquiryResult => LatestInquiryResult != null;
        public bool HasLatestInquirySuggestedSection => ResolveLatestInquirySuggestedArea(LatestInquiryResult) != GuaranteeFocusArea.None;
        public GuaranteeOutputPreviewItem? LatestLetterOutput
        {
            get => _latestLetterOutput;
            private set
            {
                if (ReferenceEquals(_latestLetterOutput, value))
                {
                    return;
                }

                _latestLetterOutput = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasLatestLetterOutput));
            }
        }

        public GuaranteeOutputPreviewItem? LatestResponseOutput
        {
            get => _latestResponseOutput;
            private set
            {
                if (ReferenceEquals(_latestResponseOutput, value))
                {
                    return;
                }

                _latestResponseOutput = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasLatestResponseOutput));
            }
        }

        public bool HasLatestLetterOutput => LatestLetterOutput != null;
        public bool HasLatestResponseOutput => LatestResponseOutput != null;
        public string LatestInquirySuggestedSectionLabel => ResolveLatestInquirySuggestedLabel(LatestInquiryResult);

        public string SelectedOperationalInquiryDescription
        {
            get => _selectedOperationalInquiryDescription;
            private set => SetProperty(ref _selectedOperationalInquiryDescription, value);
        }

        public bool HasLastFile => _lastFileState.HasLastFile;

        public string LastFileGuaranteeNo
        {
            get => _lastFileState.GuaranteeNo;
        }

        public string LastFileSummary
        {
            get => _lastFileState.Summary;
        }

        public string PendingRequestCount
        {
            get => _pendingRequestCount;
            private set => SetProperty(ref _pendingRequestCount, value);
        }

        public string PendingRequestMeta
        {
            get => _pendingRequestMeta;
            private set => SetProperty(ref _pendingRequestMeta, value);
        }

        public string ExpiredCount
        {
            get => _expiredCount;
            private set => SetProperty(ref _expiredCount, value);
        }

        public string ExpiredMeta
        {
            get => _expiredMeta;
            private set => SetProperty(ref _expiredMeta, value);
        }

        public string ExpiredFollowUpCount
        {
            get => _expiredFollowUpCount;
            private set => SetProperty(ref _expiredFollowUpCount, value);
        }

        public string ExpiredFollowUpMeta
        {
            get => _expiredFollowUpMeta;
            private set => SetProperty(ref _expiredFollowUpMeta, value);
        }

        public string ExpiringSoonCount
        {
            get => _expiringSoonCount;
            private set => SetProperty(ref _expiringSoonCount, value);
        }

        public string ExpiringSoonMeta
        {
            get => _expiringSoonMeta;
            private set => SetProperty(ref _expiringSoonMeta, value);
        }

        public string ActiveCount
        {
            get => _activeCount;
            private set => SetProperty(ref _activeCount, value);
        }

        public string ActiveMeta
        {
            get => _activeMeta;
            private set => SetProperty(ref _activeMeta, value);
        }

        public string FooterSummary
        {
            get => _footerSummary;
            private set => SetProperty(ref _footerSummary, value);
        }

        public int CurrentGuaranteePage
        {
            get => _currentGuaranteePage;
            private set
            {
                if (SetProperty(ref _currentGuaranteePage, value))
                {
                    OnPropertyChanged(nameof(CanGoToPreviousGuaranteePage));
                    OnPropertyChanged(nameof(CanGoToNextGuaranteePage));
                    RaiseGuaranteePagerCommandStates();
                }
            }
        }

        public int TotalGuaranteePages
        {
            get => _totalGuaranteePages;
            private set
            {
                if (SetProperty(ref _totalGuaranteePages, Math.Max(1, value)))
                {
                    OnPropertyChanged(nameof(CanGoToPreviousGuaranteePage));
                    OnPropertyChanged(nameof(CanGoToNextGuaranteePage));
                    RaiseGuaranteePagerCommandStates();
                }
            }
        }

        public string GuaranteePageSizeText => PageSize.ToString(CultureInfo.InvariantCulture);

        public bool CanGoToPreviousGuaranteePage => CurrentGuaranteePage > 1;

        public bool CanGoToNextGuaranteePage => CurrentGuaranteePage < TotalGuaranteePages;

        public FrameworkElement? ActiveWorkspaceContent
        {
            get => _activeWorkspaceContent;
            private set => SetProperty(ref _activeWorkspaceContent, value);
        }

        public string CurrentWorkspaceKey
        {
            get => _currentWorkspaceKey;
            private set => SetProperty(ref _currentWorkspaceKey, value);
        }

        public string CurrentWorkspaceDisplayTitle => CurrentWorkspaceKey switch
        {
            ShellWorkspaceKeys.Dashboard => "اليوم",
            ShellWorkspaceKeys.Guarantees => "الضمانات",
            ShellWorkspaceKeys.Banks => "البنوك",
            ShellWorkspaceKeys.Reports => "التقارير",
            ShellWorkspaceKeys.Settings => "الإعدادات",
            _ => "إدارة الضمانات البنكية"
        };

        public string ShellStatusPrimaryText => _shellStatus.PrimaryText;

        public string ShellStatusSecondaryText => _shellStatus.SecondaryText;

        public Brush ShellStatusPrimaryBrush => _shellStatus.Tone switch
        {
            ShellStatusTone.Success => Brushes.White,
            ShellStatusTone.Warning => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDE68A")),
            ShellStatusTone.Error => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCA5A5")),
            _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D7E6FF"))
        };

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value ?? string.Empty))
                {
                    ResetGuaranteePagination();
                    Refresh();
                }
            }
        }

        public FilterOption SelectedTimeStatus
        {
            get => _selectedTimeStatus;
            set
            {
                if (SetProperty(ref _selectedTimeStatus, value ?? FilterOption.AllTimeStatuses))
                {
                    ResetGuaranteePagination();
                    Refresh();
                }
            }
        }

        public string SelectedBank
        {
            get => _selectedBank;
            set
            {
                if (SetProperty(ref _selectedBank, string.IsNullOrWhiteSpace(value) ? AllBanksLabel : value))
                {
                    ResetGuaranteePagination();
                    Refresh();
                }
            }
        }

        public string SelectedGuaranteeType
        {
            get => _selectedGuaranteeType;
            set
            {
                if (SetProperty(ref _selectedGuaranteeType, string.IsNullOrWhiteSpace(value) ? AllTypesLabel : value))
                {
                    ResetGuaranteePagination();
                    Refresh();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action<GuaranteeFocusArea, int?>? GuaranteeFocusRequested;

        public string GlobalSearchText
        {
            get => _globalSearchText;
            set => SetProperty(ref _globalSearchText, value);
        }

        private static GuaranteeFocusArea ResolveLatestInquirySuggestedArea(OperationalInquiryResult? result)
        {
            if (result == null)
            {
                return GuaranteeFocusArea.None;
            }

            return InquiryContextRoutingResolver.TryResolve(result, out GuaranteeFocusArea area, out _)
                ? area
                : GuaranteeFocusArea.None;
        }

        private static string ResolveLatestInquirySuggestedLabel(OperationalInquiryResult? result)
        {
            if (result == null ||
                !InquiryContextRoutingResolver.TryResolve(result, out GuaranteeFocusArea area, out int? requestIdToFocus))
            {
                return "لا يوجد قسم مقترح";
            }

            return area switch
            {
                GuaranteeFocusArea.Requests when requestIdToFocus.HasValue => "انتقل إلى حدث الطلب",
                GuaranteeFocusArea.Requests => "انتقل إلى السجل الزمني",
                GuaranteeFocusArea.Series => "انتقل إلى الخط الزمني",
                GuaranteeFocusArea.Attachments => "انتقل إلى السجل الزمني",
                GuaranteeFocusArea.Outputs => "انتقل إلى السجل الزمني",
                GuaranteeFocusArea.Actions => "انتقل إلى الإجراءات",
                _ => "لا يوجد قسم مقترح"
            };
        }
    }
}
