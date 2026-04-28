using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed class ShellViewModel : INotifyPropertyChanged
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
        private int? _focusedGuaranteeRequestId;
        private int _guaranteeFocusRequestVersion;
        private GuaranteeFileFocusArea _currentGuaranteeFocusArea = GuaranteeFileFocusArea.None;
        private int _pendingGuaranteeFileFocusRootId;
        private int? _pendingGuaranteeFileFocusRequestId;
        private GuaranteeFileFocusArea _pendingGuaranteeFileFocusArea = GuaranteeFileFocusArea.None;
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
        private FilterOption _selectedTimeStatus = FilterOption.AllTimeStatuses;
        private string _selectedBank = AllBanksLabel;
        private string _selectedGuaranteeType = AllTypesLabel;
        private string _currentWorkspaceKey = ShellWorkspaceKeys.Dashboard;
        private FrameworkElement? _activeWorkspaceContent;
        private ShellLastFileState _lastFileState = ShellLastFileState.Empty;
        private bool _trackSelectedGuaranteeAsLastFile = true;

        private ShellViewModel(
            IDatabaseService database,
            IWorkflowService workflow,
            IExcelService excel,
            IGuaranteeHistoryDocumentService historyDocuments,
            IOperationalInquiryService inquiry,
            IContextActionService contextActionService,
            INavigationGuard navigationGuard,
            IShellStatusService shellStatus,
            IUiDiagnosticsService diagnostics)
        {
            _database = database;
            _navigationGuard = navigationGuard;
            _shellStatus = shellStatus;
            _diagnostics = diagnostics;
            _guaranteeData = new GuaranteeWorkspaceDataService(_database, contextActionService);
            _guaranteeWorkspace = new GuaranteeWorkspaceCoordinator(
                _database,
                workflow,
                excel,
                historyDocuments,
                inquiry,
                shellStatus,
                LoadFilterOptions,
                RefreshAfterWorkflowChange);
            _sessionCoordinator = new ShellSessionCoordinator();
            _workspaceFactory = new ShellWorkspaceFactory(_database, workflow, _guaranteeData, new ReportsWorkspaceCoordinator(_database, excel));
            _shellStatus.PropertyChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(ShellStatusPrimaryText));
                OnPropertyChanged(nameof(ShellStatusSecondaryText));
                OnPropertyChanged(nameof(ShellStatusPrimaryBrush));
            };
            CreateNewGuaranteeCommand = new RelayCommand(_ => CreateNewGuarantee());
            EditGuaranteeCommand = new RelayCommand(parameter => EditGuarantee(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            ApplySmartFilterCommand = new RelayCommand(_ => ApplySmartFilter());
            SelectGuaranteeCommand = new RelayCommand(parameter => SelectGuarantee(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            OpenHistoryCommand = new RelayCommand(parameter => OpenHistory(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            RunSelectedInquiryCommand = new RelayCommand(_ => RunSelectedInquiry(), _ => SelectedGuarantee != null && SelectedOperationalInquiryOption != null);
            CreateExtensionRequestCommand = new RelayCommand(parameter => CreateExtensionRequest(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            CreateReleaseRequestCommand = new RelayCommand(parameter => CreateReleaseRequest(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            CreateReductionRequestCommand = new RelayCommand(parameter => CreateReductionRequest(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            CreateLiquidationRequestCommand = new RelayCommand(parameter => CreateLiquidationRequest(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            CreateVerificationRequestCommand = new RelayCommand(parameter => CreateVerificationRequest(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            CreateReplacementRequestCommand = new RelayCommand(parameter => CreateReplacementRequest(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            CreateAnnulmentRequestCommand = new RelayCommand(parameter => CreateAnnulmentRequest(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            RegisterBankResponseCommand = new RelayCommand(parameter => RegisterBankResponse(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            ShowRowAttachmentsCommand = new RelayCommand(parameter => ShowRowAttachments(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            ShowRowRequestsCommand = new RelayCommand(parameter => ShowRowRequests(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            CopyGuaranteeNoCommand = new RelayCommand(parameter => CopyGuaranteeNo(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            CopyGuaranteeSupplierCommand = new RelayCommand(parameter => CopyGuaranteeSupplier(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            CopyGuaranteeReferenceTypeCommand = new RelayCommand(parameter => CopyGuaranteeReferenceType(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            CopyGuaranteeReferenceNumberCommand = new RelayCommand(parameter => CopyGuaranteeReferenceNumber(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            ExportVisibleGuaranteesCommand = new RelayCommand(_ => ExportVisibleGuarantees(), _ => Guarantees.Count > 0);
            ExportGuaranteeReportCommand = new RelayCommand(parameter => ExportGuaranteeReport(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            ExportGuaranteeHistoryCommand = new RelayCommand(parameter => ExportGuaranteeHistory(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            ExportGuaranteesByBankCommand = new RelayCommand(parameter => ExportGuaranteesByBank(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            ExportGuaranteesBySupplierCommand = new RelayCommand(parameter => ExportGuaranteesBySupplier(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            ExportGuaranteesByTemporalStatusCommand = new RelayCommand(parameter => ExportGuaranteesByTemporalStatus(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow);
            OpenGuaranteeFileCommand = new RelayCommand(parameter => OpenGuaranteeFile(parameter as GuaranteeRow), parameter => parameter is GuaranteeRow || SelectedGuarantee != null);
            FocusExecutiveSummaryCommand = new RelayCommand(_ => FocusGuaranteeSection(GuaranteeFileFocusArea.ExecutiveSummary), _ => SelectedGuarantee != null);
            FocusRequestsSectionCommand = new RelayCommand(_ => FocusGuaranteeSection(GuaranteeFileFocusArea.Requests), _ => SelectedGuarantee != null);
            FocusTimelineSectionCommand = new RelayCommand(_ => FocusGuaranteeSection(GuaranteeFileFocusArea.Series), _ => SelectedGuarantee != null);
            FocusAttachmentsSectionCommand = new RelayCommand(_ => FocusGuaranteeSection(GuaranteeFileFocusArea.Attachments), _ => SelectedGuarantee != null);
            FocusActionsSectionCommand = new RelayCommand(_ => FocusGuaranteeSection(GuaranteeFileFocusArea.Actions), _ => SelectedGuarantee != null);
            FocusOutputsSectionCommand = new RelayCommand(_ => FocusGuaranteeSection(GuaranteeFileFocusArea.Outputs), _ => SelectedGuarantee != null);
            FocusSuggestedGuaranteeActionCommand = new RelayCommand(_ => FocusSuggestedGuaranteeArea(), _ => SelectedGuarantee?.HasSuggestedFocus == true);
            OpenAttachmentCommand = new RelayCommand(parameter => OpenAttachment(parameter as AttachmentItem), parameter => parameter is AttachmentItem);
            OpenRequestPreviewCommand = new RelayCommand(parameter => OpenRequestPreview(parameter as GuaranteeRequestPreviewItem), parameter => parameter is GuaranteeRequestPreviewItem);
            RegisterRequestPreviewResponseCommand = new RelayCommand(parameter => RegisterRequestPreviewResponse(parameter as GuaranteeRequestPreviewItem), parameter => parameter is GuaranteeRequestPreviewItem item && item.CanRegisterResponse);
            OpenRequestPreviewLetterCommand = new RelayCommand(parameter => OpenRequestPreviewLetter(parameter as GuaranteeRequestPreviewItem), parameter => parameter is GuaranteeRequestPreviewItem item && item.CanOpenLetter);
            OpenRequestPreviewResponseCommand = new RelayCommand(parameter => OpenRequestPreviewResponse(parameter as GuaranteeRequestPreviewItem), parameter => parameter is GuaranteeRequestPreviewItem item && item.CanOpenResponse);
            OpenOutputLetterCommand = new RelayCommand(parameter => OpenOutputLetter(parameter as GuaranteeOutputPreviewItem), parameter => parameter is GuaranteeOutputPreviewItem item && item.CanOpenLetter);
            OpenOutputResponseCommand = new RelayCommand(parameter => OpenOutputResponse(parameter as GuaranteeOutputPreviewItem), parameter => parameter is GuaranteeOutputPreviewItem item && item.CanOpenResponse);
            OpenLatestInquiryDialogCommand = new RelayCommand(_ => OpenLatestInquiryDialog(), _ => LatestInquiryResult != null);
            OpenLatestInquiryHistoryCommand = new RelayCommand(_ => OpenLatestInquiryHistory(), _ => LatestInquiryResult?.CanOpenHistory == true);
            OpenLatestInquiryLetterCommand = new RelayCommand(_ => OpenLatestInquiryLetter(), _ => LatestInquiryResult?.CanOpenRequestLetter == true);
            OpenLatestInquiryResponseCommand = new RelayCommand(_ => OpenLatestInquiryResponse(), _ => LatestInquiryResult?.CanOpenResponseDocument == true);
            FocusLatestInquirySectionCommand = new RelayCommand(_ => FocusLatestInquirySection(), _ => ResolveLatestInquirySuggestedArea(LatestInquiryResult) != GuaranteeFileFocusArea.None);
            ShowAllAttachmentsCommand = new RelayCommand(_ => ShowAllAttachments(), _ => SelectedGuarantee != null);
            ShowGuaranteeRequestsCommand = new RelayCommand(_ => ShowGuaranteeRequests(), _ => SelectedGuarantee != null);
            ResumeLastFileCommand = new RelayCommand(_ => ResumeLastFile(), _ => HasLastFile);
            ShowDashboardCommand = new RelayCommand(_ => ShowDashboardWorkspace());
            ShowGuaranteesCommand = new RelayCommand(_ => ShowGuaranteesWorkspace());
            ShowRequestsCommand = new RelayCommand(_ => ShowRequestsWorkspace());
            ShowBanksCommand = new RelayCommand(_ => ShowBanksWorkspace());
            ShowReportsCommand = new RelayCommand(_ => ShowReportsWorkspace());
            ShowNotificationsCommand = new RelayCommand(_ => ShowNotificationsWorkspace());
            ShowSettingsCommand = new RelayCommand(_ => ShowSettingsWorkspace());
            ExecuteGlobalSearchCommand = new RelayCommand(_ => ExecuteGlobalSearch());
            ExitCommand = new RelayCommand(_ => RequestExit());
            LoadOperationalInquiryOptions();
        }

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
            ? "الطلب الذي فتح الملف"
            : GuaranteeRequestsPreview.Count == 0
                ? "لا توجد حركة حالية"
                : "الأقرب للتنفيذ الآن";
        public string TimelineSummaryText => Timeline.Count == 0
            ? "سجل المراحل الرئيسية لهذا الضمان سيظهر هنا عند توفر طلبات أو أحداث موثقة."
            : "راجع هذا التسلسل لفهم آخر ما تغيّر قبل فتح طلب أو مخرج أو مرفق.";
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
            ? "لا توجد مرفقات رسمية محفوظة على هذا الملف حاليًا. ستبقى خطابات الطلب وردود البنك داخل قسم المخرجات عند توفرها."
            : $"هذه هي الأدلة الرسمية المحفوظة على الملف. يوجد {Attachments.Count.ToString("N0", CultureInfo.InvariantCulture)} مرفقات رسمية، بينما تبقى خطابات الطلب وردود البنك ضمن المخرجات.";
        public string OfficialAttachmentsHeading => Attachments.Count == 0
            ? "المرفقات الرسمية"
            : $"المرفقات الرسمية ({Attachments.Count.ToString("N0", CultureInfo.InvariantCulture)})";
        public bool HasOutputShortcuts => HasLatestLetterOutput || HasLatestResponseOutput;
        public string OutputShortcutsSummaryText => HasOutputShortcuts
            ? "اختصارات آخر المستندات الناتجة"
            : "لا توجد اختصارات لمستندات ناتجة الآن";
        public int GuaranteeFocusRequestVersion => _guaranteeFocusRequestVersion;
        public GuaranteeFileFocusArea CurrentGuaranteeFocusArea => _currentGuaranteeFocusArea;
        public int? CurrentGuaranteeFocusRequestId => _focusedGuaranteeRequestId;
        public ObservableCollection<FilterOption> TimeStatusOptions { get; } = new();
        public ObservableCollection<string> BankOptions { get; } = new();
        public ObservableCollection<string> GuaranteeTypeOptions { get; } = new();
        public ObservableCollection<OperationalInquiryOption> OperationalInquiryOptions { get; } = new();
        public ICommand CreateNewGuaranteeCommand { get; }
        public ICommand EditGuaranteeCommand { get; }
        public ICommand ApplySmartFilterCommand { get; }
        public ICommand SelectGuaranteeCommand { get; }
        public ICommand OpenHistoryCommand { get; }
        public ICommand RunSelectedInquiryCommand { get; }
        public ICommand CreateExtensionRequestCommand { get; }
        public ICommand CreateReleaseRequestCommand { get; }
        public ICommand CreateReductionRequestCommand { get; }
        public ICommand CreateLiquidationRequestCommand { get; }
        public ICommand CreateVerificationRequestCommand { get; }
        public ICommand CreateReplacementRequestCommand { get; }
        public ICommand CreateAnnulmentRequestCommand { get; }
        public ICommand RegisterBankResponseCommand { get; }
        public ICommand ShowRowAttachmentsCommand { get; }
        public ICommand ShowRowRequestsCommand { get; }
        public ICommand CopyGuaranteeNoCommand { get; }
        public ICommand CopyGuaranteeSupplierCommand { get; }
        public ICommand CopyGuaranteeReferenceTypeCommand { get; }
        public ICommand CopyGuaranteeReferenceNumberCommand { get; }
        public ICommand ExportVisibleGuaranteesCommand { get; }
        public ICommand ExportGuaranteeReportCommand { get; }
        public ICommand ExportGuaranteeHistoryCommand { get; }
        public ICommand ExportGuaranteesByBankCommand { get; }
        public ICommand ExportGuaranteesBySupplierCommand { get; }
        public ICommand ExportGuaranteesByTemporalStatusCommand { get; }
        public ICommand OpenGuaranteeFileCommand { get; }
        public ICommand FocusExecutiveSummaryCommand { get; }
        public ICommand FocusRequestsSectionCommand { get; }
        public ICommand FocusTimelineSectionCommand { get; }
        public ICommand FocusAttachmentsSectionCommand { get; }
        public ICommand FocusActionsSectionCommand { get; }
        public ICommand FocusOutputsSectionCommand { get; }
        public ICommand FocusSuggestedGuaranteeActionCommand { get; }
        public ICommand OpenAttachmentCommand { get; }
        public ICommand OpenRequestPreviewCommand { get; }
        public ICommand RegisterRequestPreviewResponseCommand { get; }
        public ICommand OpenRequestPreviewLetterCommand { get; }
        public ICommand OpenRequestPreviewResponseCommand { get; }
        public ICommand OpenOutputLetterCommand { get; }
        public ICommand OpenOutputResponseCommand { get; }
        public ICommand OpenLatestInquiryDialogCommand { get; }
        public ICommand OpenLatestInquiryHistoryCommand { get; }
        public ICommand OpenLatestInquiryLetterCommand { get; }
        public ICommand OpenLatestInquiryResponseCommand { get; }
        public ICommand FocusLatestInquirySectionCommand { get; }
        public ICommand ShowAllAttachmentsCommand { get; }
        public ICommand ShowGuaranteeRequestsCommand { get; }
        public ICommand ResumeLastFileCommand { get; }
        public ICommand ShowDashboardCommand { get; }
        public ICommand ShowGuaranteesCommand { get; }
        public ICommand ShowRequestsCommand { get; }
        public ICommand ShowBanksCommand { get; }
        public ICommand ShowReportsCommand { get; }
        public ICommand ShowNotificationsCommand { get; }
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
                if (_focusedGuaranteeRequestId.HasValue)
                {
                    _focusedGuaranteeRequestId = null;
                    RaiseGuaranteeFileSectionTextProperties();
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
                OnPropertyChanged(nameof(LatestInquirySuggestedSectionLabel));
                RaiseInquiryCommandStates();
            }
        }

        public bool HasLatestInquiryResult => LatestInquiryResult != null;
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
            ShellWorkspaceKeys.Requests => "الطلبات",
            ShellWorkspaceKeys.Banks => "البنوك",
            ShellWorkspaceKeys.Reports => "التحليلات والمخرجات",
            ShellWorkspaceKeys.Notifications => "التنبيهات",
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
                if (SetProperty(ref _searchText, value))
                {
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
                    Refresh();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action<GuaranteeFileFocusArea, int?>? GuaranteeFocusRequested;

        public static ShellViewModel Create(
            IDatabaseService database,
            IWorkflowService workflow,
            IExcelService excel,
            IGuaranteeHistoryDocumentService historyDocuments,
            IOperationalInquiryService inquiry,
            IContextActionService contextActionService,
            INavigationGuard navigationGuard,
            IShellStatusService shellStatus,
            IUiDiagnosticsService diagnostics)
        {
            var viewModel = new ShellViewModel(database, workflow, excel, historyDocuments, inquiry, contextActionService, navigationGuard, shellStatus, diagnostics);
            viewModel.LoadFilterOptions();
            viewModel.Refresh();
            viewModel.ShowDashboardWorkspace();
            viewModel.WriteDiagnosticsState("startup");
            return viewModel;
        }

        private void LoadOperationalInquiryOptions()
        {
            string? selectedOptionId = SelectedOperationalInquiryOption?.Id;
            IReadOnlyList<OperationalInquiryOption> options = _guaranteeData.BuildOperationalInquiryOptions();
            OperationalInquiryOptions.Clear();
            foreach (OperationalInquiryOption option in options)
            {
                OperationalInquiryOptions.Add(option);
            }

            SelectedOperationalInquiryOption = OperationalInquiryOptions.FirstOrDefault(option => option.Id == selectedOptionId)
                                               ?? OperationalInquiryOptions.FirstOrDefault();
        }

        private void UpdateSelectedOperationalInquiryDescription()
        {
            SelectedOperationalInquiryDescription = _guaranteeData.BuildOperationalInquiryDescription(
                SelectedOperationalInquiryOption,
                SelectedGuarantee);
        }

        public void Refresh()
        {
            GuaranteeRow? previousSelection = SelectedGuarantee;
            GuaranteeWorkspaceSnapshot snapshot = _guaranteeData.BuildSnapshot(
                SearchText,
                SelectedBank,
                AllBanksLabel,
                SelectedGuaranteeType,
                AllTypesLabel,
                SelectedTimeStatus.Value,
                PageSize);

            Guarantees.Clear();
            foreach (GuaranteeRow row in snapshot.Rows)
            {
                Guarantees.Add(row);
            }

            PendingRequestCount = snapshot.PendingRequestCount;
            PendingRequestMeta = snapshot.PendingRequestMeta;
            ExpiredCount = snapshot.ExpiredCount;
            ExpiredMeta = snapshot.ExpiredMeta;
            ExpiredFollowUpCount = snapshot.ExpiredFollowUpCount;
            ExpiredFollowUpMeta = snapshot.ExpiredFollowUpMeta;
            ExpiringSoonCount = snapshot.ExpiringSoonCount;
            ExpiringSoonMeta = snapshot.ExpiringSoonMeta;
            ActiveCount = snapshot.ActiveCount;
            ActiveMeta = snapshot.ActiveMeta;
            FooterSummary = snapshot.FooterSummary;

            _trackSelectedGuaranteeAsLastFile = false;
            SelectedGuarantee = ResolvePreferredVisibleGuarantee(previousSelection);
            _trackSelectedGuaranteeAsLastFile = true;
            if (ExportVisibleGuaranteesCommand is RelayCommand exportCommand)
            {
                exportCommand.RaiseCanExecuteChanged();
            }

            WriteDiagnosticsState("refresh");
        }

        private GuaranteeRow? ResolvePreferredVisibleGuarantee(GuaranteeRow? previousSelection)
        {
            if (previousSelection != null)
            {
                int previousRootId = previousSelection.RootId > 0
                    ? previousSelection.RootId
                    : previousSelection.Id;

                GuaranteeRow? matchingRow = Guarantees.FirstOrDefault(row =>
                    row.RootId == previousRootId
                    || row.Id == previousSelection.Id);

                if (matchingRow != null)
                {
                    return matchingRow;
                }
            }

            return Guarantees.FirstOrDefault();
        }

        private void SelectGuarantee(GuaranteeRow? row)
        {
            if (row != null)
            {
                LatestInquiryResult = null;
                SelectedGuarantee = row;
                _diagnostics.RecordEvent(
                    "shell.selection",
                    "guarantee.select",
                    new
                    {
                        row.Id,
                        row.RootId,
                        row.GuaranteeNo,
                        row.Beneficiary,
                        row.Bank
                    });
                WriteDiagnosticsState("select-guarantee");
            }
        }

        private void CreateNewGuarantee()
        {
            _guaranteeWorkspace.CreateNewGuarantee();
        }

        private void EditGuarantee(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.EditGuarantee);
        }

        private void OpenGuaranteeFile(GuaranteeRow? row)
        {
            GuaranteeRow? target = ResolveTarget(row);
            if (target == null)
            {
                return;
            }

            SelectedGuarantee = target;
            GuaranteeFileFocusArea focusArea = target.SuggestedFocusArea == GuaranteeFileFocusArea.None
                ? GuaranteeFileFocusArea.ExecutiveSummary
                : target.SuggestedFocusArea;
            QueueGuaranteeFileOpenFocus(focusArea, ResolveInitialFileFocusRequestId(target, focusArea), target.RootId);
            GuaranteeFileDialog.ShowFor(this, target);
        }

        private int? ResolveInitialFileFocusRequestId(GuaranteeRow target, GuaranteeFileFocusArea focusArea)
        {
            if (focusArea != GuaranteeFileFocusArea.Requests)
            {
                return null;
            }

            return _database.GetWorkflowRequestsByRootId(target.RootId)
                .OrderBy(request => request.Status == RequestStatus.Pending ? 0 : 1)
                .ThenByDescending(request => request.RequestDate)
                .ThenByDescending(request => request.SequenceNumber)
                .FirstOrDefault()
                ?.Id;
        }

        private void OpenGuaranteeContextFromDashboard(int rootId, GuaranteeFileFocusArea area, int? requestIdToFocus)
        {
            OpenGuaranteeContext("dashboard", rootId, area, requestIdToFocus);
        }

        private void OpenGuaranteeContextFromNotifications(int rootId, GuaranteeFileFocusArea area, int? requestIdToFocus)
        {
            OpenGuaranteeContext("notifications", rootId, area, requestIdToFocus);
        }

        private void OpenGuaranteeContext(string sourceKey, int rootId, GuaranteeFileFocusArea area, int? requestIdToFocus)
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Guarantees))
            {
                return;
            }

            ResetGuaranteeFilters();

            Guarantee? guarantee = _database.GetCurrentGuaranteeByRootId(rootId);
            if (guarantee == null)
            {
                ShowGuaranteesWorkspace();
                return;
            }

            GuaranteeRow? row = Guarantees.FirstOrDefault(item => item.RootId == rootId)
                                ?? Guarantees.FirstOrDefault(item => item.Id == guarantee.Id);

            if (row == null)
            {
                RefreshAfterWorkflowChange(rootId);
                row = Guarantees.FirstOrDefault(item => item.RootId == rootId)
                      ?? Guarantees.FirstOrDefault(item => item.Id == guarantee.Id);
            }

            if (row == null)
            {
                ShowGuaranteesWorkspace();
                return;
            }

            SelectedGuarantee = row;
            QueueGuaranteeFileOpenFocus(area, requestIdToFocus, row.RootId);
            OpenGuaranteeFile(row);
            _diagnostics.RecordEvent(
                $"{sourceKey}.action",
                "open-guarantee-context",
                new
                {
                    rootId,
                    FocusArea = area.ToString(),
                    requestIdToFocus
                });
            WriteDiagnosticsState($"{sourceKey}-open-guarantee-context");
        }

        private void ApplySmartFilter()
        {
            SetGuaranteeFilters(
                string.Empty,
                AllBanksLabel,
                AllTypesLabel,
                TimeStatusOptions.FirstOrDefault(option => option.Value == GuaranteeTimeStatus.ExpiringSoon)
                ?? FilterOption.AllTimeStatuses);
        }

        private void OpenHistory(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.OpenHistory);
        }

        private void RunSelectedInquiry()
        {
            GuaranteeRow? target = ResolveTarget(SelectedGuarantee);
            OperationalInquiryOption? option = SelectedOperationalInquiryOption;
            if (target == null || option == null)
            {
                return;
            }

            LatestInquiryResult = _guaranteeWorkspace.RunInquiry(target, option);
            _diagnostics.RecordEvent(
                "guarantee.inquiry",
                "run",
                new
                {
                    target.Id,
                    target.RootId,
                    target.GuaranteeNo,
                    InquiryId = option.Id
                });
            WriteDiagnosticsState("run-inquiry");
        }

        private void CreateExtensionRequest(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CreateExtensionRequest);
        }

        private void CreateReleaseRequest(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CreateReleaseRequest);
        }

        private void CreateReductionRequest(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CreateReductionRequest);
        }

        private void CreateLiquidationRequest(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CreateLiquidationRequest);
        }

        private void CreateVerificationRequest(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CreateVerificationRequest);
        }

        private void CreateReplacementRequest(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CreateReplacementRequest);
        }

        private void CreateAnnulmentRequest(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CreateAnnulmentRequest);
        }

        private void RegisterBankResponse(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.RegisterBankResponse);
        }

        private void OpenAttachment(AttachmentItem? item)
        {
            _sessionCoordinator.OpenAttachment(item);
        }

        private void ShowAllAttachments()
        {
            ExecuteGuaranteeAction(SelectedGuarantee, target => _guaranteeWorkspace.ShowAttachments(target, showEmptyMessage: true));
        }

        private void ShowGuaranteeRequests()
        {
            ExecuteGuaranteeAction(SelectedGuarantee, target => _guaranteeWorkspace.ShowRequests(target, requireExistingRequests: false));
        }

        private void OpenRequestPreview(GuaranteeRequestPreviewItem? item)
        {
            if (item == null)
            {
                return;
            }

            ExecuteGuaranteeAction(
                SelectedGuarantee,
                target => _guaranteeWorkspace.ShowRequests(target, requireExistingRequests: false, item.Request.Id),
                syncSelection: true);
        }

        private void RegisterRequestPreviewResponse(GuaranteeRequestPreviewItem? item)
        {
            if (item?.CanRegisterResponse != true || SelectedGuarantee == null)
            {
                return;
            }

            _guaranteeWorkspace.RegisterBankResponse(item.Request, SelectedGuarantee.GuaranteeNo);
        }

        private void OpenRequestPreviewLetter(GuaranteeRequestPreviewItem? item)
        {
            if (item != null)
            {
                _guaranteeWorkspace.OpenRequestLetter(item.Request);
            }
        }

        private void OpenRequestPreviewResponse(GuaranteeRequestPreviewItem? item)
        {
            if (item != null)
            {
                _guaranteeWorkspace.OpenResponseDocument(item.Request);
            }
        }

        private void FocusSuggestedGuaranteeArea()
        {
            if (SelectedGuarantee?.HasSuggestedFocus == true)
            {
                FocusGuaranteeSection(SelectedGuarantee.SuggestedFocusArea);
            }
        }

        private void FocusLatestInquirySection()
        {
            GuaranteeFileFocusArea area = ResolveLatestInquirySuggestedArea(LatestInquiryResult);
            if (area != GuaranteeFileFocusArea.None)
            {
                FocusGuaranteeSection(area);
            }
        }

        private void ShowRowAttachments(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(
                row,
                target => _guaranteeWorkspace.ShowAttachments(target, showEmptyMessage: true),
                syncSelection: true);
        }

        private void ShowRowRequests(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(
                row,
                target => _guaranteeWorkspace.ShowRequests(target, requireExistingRequests: true),
                syncSelection: true);
        }

        private void CopyGuaranteeNo(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CopyGuaranteeNo, syncSelection: true);
        }

        private void CopyGuaranteeSupplier(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CopySupplier, syncSelection: true);
        }

        private void CopyGuaranteeReferenceType(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CopyReferenceType, syncSelection: true);
        }

        private void CopyGuaranteeReferenceNumber(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CopyReferenceNumber, syncSelection: true);
        }

        private void ExportVisibleGuarantees()
        {
            _guaranteeWorkspace.ExportVisibleGuarantees(Guarantees.ToList());
        }

        private void ExportGuaranteeReport(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.ExportGuaranteeReport, syncSelection: true);
        }

        private void ExportGuaranteeHistory(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.ExportGuaranteeHistory, syncSelection: true);
        }

        private void ExportGuaranteesByBank(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.ExportGuaranteesByBank, syncSelection: true);
        }

        private void ExportGuaranteesBySupplier(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.ExportGuaranteesBySupplier, syncSelection: true);
        }

        private void ExportGuaranteesByTemporalStatus(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.ExportGuaranteesByTemporalStatus, syncSelection: true);
        }

        public void FocusGuaranteeSection(GuaranteeFileFocusArea area, int? requestIdToFocus = null)
        {
            if (SelectedGuarantee == null)
            {
                return;
            }

            _currentGuaranteeFocusArea = area;
            int? nextFocusedRequestId = area == GuaranteeFileFocusArea.Requests ? requestIdToFocus : null;
            _guaranteeFocusRequestVersion++;
            if (_focusedGuaranteeRequestId != nextFocusedRequestId)
            {
                _focusedGuaranteeRequestId = nextFocusedRequestId;
                RaiseGuaranteeFileSectionTextProperties();
                RefreshSelectedGuaranteeArtifacts();
            }

            GuaranteeFocusRequested?.Invoke(area, requestIdToFocus);
        }

        public void QueueGuaranteeFileOpenFocus(GuaranteeFileFocusArea area, int? requestIdToFocus, int rootId)
        {
            _pendingGuaranteeFileFocusArea = area;
            _pendingGuaranteeFileFocusRequestId = area == GuaranteeFileFocusArea.Requests ? requestIdToFocus : null;
            _pendingGuaranteeFileFocusRootId = rootId;
            FocusGuaranteeSection(area, requestIdToFocus);
        }

        public bool TryConsumePendingGuaranteeFileOpenFocus(int rootId, out GuaranteeFileFocusArea area, out int? requestIdToFocus)
        {
            if (_pendingGuaranteeFileFocusRootId == rootId && _pendingGuaranteeFileFocusArea != GuaranteeFileFocusArea.None)
            {
                area = _pendingGuaranteeFileFocusArea;
                requestIdToFocus = _pendingGuaranteeFileFocusRequestId;
                _pendingGuaranteeFileFocusArea = GuaranteeFileFocusArea.None;
                _pendingGuaranteeFileFocusRequestId = null;
                _pendingGuaranteeFileFocusRootId = 0;
                return true;
            }

            area = GuaranteeFileFocusArea.None;
            requestIdToFocus = null;
            return false;
        }

        private void OpenOutputLetter(GuaranteeOutputPreviewItem? item)
        {
            if (item != null)
            {
                _guaranteeWorkspace.OpenRequestLetter(item.Request);
            }
        }

        private void OpenOutputResponse(GuaranteeOutputPreviewItem? item)
        {
            if (item != null)
            {
                _guaranteeWorkspace.OpenResponseDocument(item.Request);
            }
        }

        private void OpenLatestInquiryDialog()
        {
            if (LatestInquiryResult != null)
            {
                _guaranteeWorkspace.ShowInquiryResult(LatestInquiryResult);
            }
        }

        private void OpenLatestInquiryHistory()
        {
            if (LatestInquiryResult != null)
            {
                _guaranteeWorkspace.OpenInquiryHistory(LatestInquiryResult);
            }
        }

        private void OpenLatestInquiryLetter()
        {
            if (LatestInquiryResult?.RelatedRequest != null)
            {
                _guaranteeWorkspace.OpenRequestLetter(LatestInquiryResult.RelatedRequest);
            }
        }

        private void OpenLatestInquiryResponse()
        {
            if (LatestInquiryResult?.RelatedRequest != null)
            {
                _guaranteeWorkspace.OpenResponseDocument(LatestInquiryResult.RelatedRequest);
            }
        }

        public void RunInquiryAction(string actionId, GuaranteeRow? row)
        {
            GuaranteeRow? target = ResolveTarget(row);
            if (target == null || string.IsNullOrWhiteSpace(actionId))
            {
                return;
            }

            SelectedGuarantee = target;
            SelectedOperationalInquiryOption = OperationalInquiryOptions.FirstOrDefault(option => option.Id == actionId)
                                               ?? SelectedOperationalInquiryOption;

            Guarantee? guarantee = _database.GetGuaranteeById(target.Id);
            if (guarantee == null)
            {
                MessageBox.Show("تعذر تحميل الضمان المحدد لتنفيذ الاستعلام.", "الاستعلامات التشغيلية", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ContextActionAvailability availability = GuaranteeInquiryActionSupport.GetAvailability(actionId, guarantee);
            if (!availability.IsEnabled)
            {
                MessageBox.Show(
                    availability.DisabledReason ?? "هذا الاستعلام غير متاح لهذا السجل حاليًا.",
                    "الاستعلامات التشغيلية",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            LatestInquiryResult = _guaranteeWorkspace.RunInquiry(target, new OperationalInquiryOption(actionId, "استعلام مباشر", actionId, string.Empty));
        }

        public ContextActionAvailability GetInquiryAvailability(GuaranteeRow row, string actionId)
        {
            Guarantee? guarantee = _database.GetGuaranteeById(row.Id);
            return guarantee == null
                ? ContextActionAvailability.Disabled("تعذر تحميل السجل الحالي لتقييم إتاحة هذا الاستعلام.")
                : GuaranteeInquiryActionSupport.GetAvailability(actionId, guarantee);
        }

        public void ExecuteGlobalSearch()
        {
            ShellWorkspaceSearchPlan plan = ShellWorkspaceAliasResolver.Resolve(GlobalSearchText, CurrentWorkspaceKey);
            if (!plan.MatchedAlias && !plan.HasSearchText)
            {
                return;
            }

            _diagnostics.RecordEvent(
                "shell.search",
                "execute",
                new
                {
                    Query = GlobalSearchText,
                    plan.TargetWorkspaceKey,
                    plan.SearchText,
                    plan.MatchedAlias
                });

            switch (plan.TargetWorkspaceKey)
            {
                case ShellWorkspaceKeys.Dashboard:
                    ShowDashboardWorkspace(plan.HasSearchText ? plan.SearchText : null);
                    break;
                case ShellWorkspaceKeys.Guarantees:
                    if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Guarantees))
                    {
                        return;
                    }

                    if (plan.HasSearchText)
                    {
                        SearchText = plan.SearchText;
                    }

                    ShowGuaranteesWorkspace();
                    break;
                case ShellWorkspaceKeys.Requests:
                    ShowRequestsWorkspace(plan.SearchText);
                    break;
                case ShellWorkspaceKeys.Banks:
                    ShowBanksWorkspace(plan.SearchText);
                    break;
                case ShellWorkspaceKeys.Reports:
                    ShowReportsWorkspace(plan.SearchText);
                    break;
                case ShellWorkspaceKeys.Notifications:
                    ShowNotificationsWorkspace(plan.SearchText);
                    break;
                case ShellWorkspaceKeys.Settings:
                    ShowSettingsWorkspace(plan.SearchText);
                    break;
                default:
                    if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Guarantees))
                    {
                        return;
                    }

                    if (plan.HasSearchText)
                    {
                        SearchText = plan.SearchText;
                    }

                    ShowGuaranteesWorkspace();
                    break;
            }
        }

        private void ShowDashboardWorkspace()
        {
            ShowDashboardWorkspace(null, null);
        }

        private void ShowDashboardWorkspace(string? initialSearchText)
        {
            ShowDashboardWorkspace(initialSearchText, null);
        }

        private void ShowDashboardWorkspace(string? initialSearchText, string? initialScopeFilter)
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Dashboard))
            {
                return;
            }

            ActivateWorkspace(
                ShellWorkspaceKeys.Dashboard,
                _workspaceFactory.CreateDashboardWorkspace(
                    SearchText,
                    SelectedBank,
                    AllBanksLabel,
                    SelectedGuaranteeType,
                    AllTypesLabel,
                    SelectedTimeStatus.Value,
                    HasLastFile,
                    LastFileGuaranteeNo,
                    LastFileSummary,
                    ResumeLastFile,
                    OpenGuaranteeContextFromDashboard,
                    ShowGuaranteesWorkspace,
                    ShowDashboardWorkspace,
                    ShowRequestsWorkspace,
                    ShowReportsWorkspace,
                    initialSearchText,
                    initialScopeFilter));
        }

        private void ShowGuaranteesWorkspace()
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Guarantees))
            {
                return;
            }

            ActivateWorkspace(ShellWorkspaceKeys.Guarantees, null);
        }

        private void ResumeLastFile()
        {
            if (!HasLastFile)
            {
                return;
            }

            Guarantee? guarantee = _sessionCoordinator.ResolveLastFileGuarantee(_lastFileState, _database);
            if (guarantee == null)
            {
                SetLastFileState(ShellLastFileState.Empty);
                return;
            }

            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Guarantees))
            {
                return;
            }

            CloseActiveWorkspace();

            SearchText = string.Empty;
            SelectedBank = AllBanksLabel;
            SelectedGuaranteeType = AllTypesLabel;
            SelectedTimeStatus = FilterOption.AllTimeStatuses;

            GuaranteeRow? row = Guarantees.FirstOrDefault(item => item.RootId == _lastFileState.RootId)
                ?? Guarantees.FirstOrDefault(item => item.Id == guarantee.Id);
            if (row != null)
            {
                SelectedGuarantee = row;
                return;
            }

            RefreshAfterWorkflowChange(_lastFileState.RootId);
            _diagnostics.RecordEvent(
                "shell.session",
                "resume-last-file",
                new
                {
                    _lastFileState.RootId,
                    _lastFileState.GuaranteeNo
                });
        }

        private void ShowRequestsWorkspace()
        {
            ShowRequestsWorkspace(null);
        }

        private void ShowRequestsWorkspace(string? initialSearchText)
        {
            ShowRequestsWorkspace(initialSearchText, null);
        }

        private void ShowRequestsWorkspace(string? initialSearchText, int? initialRequestId)
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Requests))
            {
                return;
            }

            ActivateWorkspace(
                ShellWorkspaceKeys.Requests,
                _workspaceFactory.CreateRequestsWorkspace(
                    RefreshAfterWorkflowChange,
                    CloseActiveWorkspace,
                    () =>
                    {
                        if (string.Equals(CurrentWorkspaceKey, ShellWorkspaceKeys.Requests, StringComparison.Ordinal)
                            && ActiveWorkspaceContent is RequestsWorkspaceSurface)
                        {
                            WriteDiagnosticsState("requests-selection");
                        }
                    },
                    initialSearchText,
                    initialRequestId));
        }

        private void CloseActiveWorkspace()
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Guarantees))
            {
                return;
            }

            ActivateWorkspace(ShellWorkspaceKeys.Guarantees, null);
        }

        private void ShowBanksWorkspace()
        {
            ShowBanksWorkspace(null);
        }

        private void ShowBanksWorkspace(string? initialSearchText)
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Banks))
            {
                return;
            }

            ActivateWorkspace(
                ShellWorkspaceKeys.Banks,
                _workspaceFactory.CreateBanksWorkspace(CloseActiveWorkspace, initialSearchText));
        }

        private void ShowReportsWorkspace()
        {
            ShowReportsWorkspace(null);
        }

        private void ShowReportsWorkspace(string? initialSearchText)
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Reports))
            {
                return;
            }

            ActivateWorkspace(
                ShellWorkspaceKeys.Reports,
                _workspaceFactory.CreateReportsWorkspace(ShowBanksWorkspace, CloseActiveWorkspace, initialSearchText));
        }

        private void ShowNotificationsWorkspace()
        {
            ShowNotificationsWorkspace(null);
        }

        private void ShowNotificationsWorkspace(string? initialSearchText)
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Notifications))
            {
                return;
            }

            ActivateWorkspace(
                ShellWorkspaceKeys.Notifications,
                _workspaceFactory.CreateNotificationsWorkspace(
                    OpenGuaranteeContextFromNotifications,
                    ShowGuaranteesWorkspace,
                    CloseActiveWorkspace,
                    initialSearchText));
        }

        private void ShowSettingsWorkspace()
        {
            ShowSettingsWorkspace(null);
        }

        private void ShowSettingsWorkspace(string? initialSearchText)
        {
            if (!CanNavigateToWorkspace(ShellWorkspaceKeys.Settings))
            {
                return;
            }

            ActivateWorkspace(
                ShellWorkspaceKeys.Settings,
                _workspaceFactory.CreateSettingsWorkspace(CloseActiveWorkspace, RefreshAfterDataReset, initialSearchText));
        }

        private void RequestExit()
        {
            if (!CanNavigateAway("إغلاق البرنامج"))
            {
                return;
            }

            Application.Current.MainWindow?.Close();
        }

        private bool CanNavigateToWorkspace(string targetWorkspaceKey)
        {
            if (string.Equals(CurrentWorkspaceKey, targetWorkspaceKey, StringComparison.Ordinal))
            {
                return true;
            }

            return CanNavigateAway("التنقل بين المساحات");
        }

        private bool CanNavigateAway(string actionLabel)
        {
            if (_navigationGuard.CanNavigateAway(out string blockingReason))
            {
                return true;
            }

            string message = string.IsNullOrWhiteSpace(blockingReason)
                ? $"تعذر {actionLabel} قبل إكمال العملية الحالية."
                : blockingReason;
            MessageBox.Show(message, "حراسة التنقل", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        private void ExecuteGuaranteeAction(GuaranteeRow? row, Action<GuaranteeRow> action, bool syncSelection = false)
        {
            GuaranteeRow? target = ResolveTarget(row);
            if (target == null)
            {
                return;
            }

            if (syncSelection)
            {
                SelectedGuarantee = target;
            }

            action(target);
        }

        private GuaranteeRow? ResolveTarget(GuaranteeRow? row)
        {
            GuaranteeRow? target = row ?? SelectedGuarantee;
            if (target == null)
            {
                MessageBox.Show("اختر ضماناً أولاً.", "إجراء الضمان", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return target;
        }

        private void RefreshAfterWorkflowChange(int rootIdToRestore)
        {
            Refresh();
            SelectedGuarantee = rootIdToRestore > 0
                ? ResolveOrSurfaceGuaranteeRow(rootIdToRestore) ?? Guarantees.FirstOrDefault()
                : Guarantees.FirstOrDefault();
            WriteDiagnosticsState("workflow-change");
        }

        private GuaranteeRow? ResolveOrSurfaceGuaranteeRow(int rootId)
        {
            GuaranteeRow? visibleRow = Guarantees.FirstOrDefault(row => row.RootId == rootId);
            if (visibleRow != null)
            {
                return visibleRow;
            }

            Guarantee? currentGuarantee = _database.GetCurrentGuaranteeByRootId(rootId);
            if (currentGuarantee == null)
            {
                return null;
            }

            List<WorkflowRequest> requests = _database.GetWorkflowRequestsByRootId(rootId);
            GuaranteeRow surfacedRow = GuaranteeRow.FromGuarantee(currentGuarantee, requests);
            Guarantees.Insert(0, surfacedRow);

            while (Guarantees.Count > PageSize)
            {
                Guarantees.RemoveAt(Guarantees.Count - 1);
            }

            return surfacedRow;
        }

        private void RefreshAfterDataReset()
        {
            LoadFilterOptions();
            Refresh();

            if (_lastFileState.HasLastFile && _database.GetCurrentGuaranteeByRootId(_lastFileState.RootId) == null)
            {
                SetLastFileState(ShellLastFileState.Empty);
            }

            LatestInquiryResult = null;
            WriteDiagnosticsState("data-reset");
        }

        private void LoadFilterOptions()
        {
            GuaranteeWorkspaceFilterData filters = _guaranteeData.BuildFilters(AllBanksLabel, AllTypesLabel);

            TimeStatusOptions.Clear();
            foreach (FilterOption option in filters.TimeStatusOptions)
            {
                TimeStatusOptions.Add(option);
            }

            BankOptions.Clear();
            foreach (string bank in filters.BankOptions)
            {
                BankOptions.Add(bank);
            }

            GuaranteeTypeOptions.Clear();
            foreach (string type in filters.GuaranteeTypeOptions)
            {
                GuaranteeTypeOptions.Add(type);
            }
        }

        private void RefreshSelectedGuaranteeArtifacts()
        {
            GuaranteeSelectionArtifacts artifacts = _guaranteeData.BuildSelectionArtifacts(SelectedGuarantee, _focusedGuaranteeRequestId);
            Timeline.Clear();
            Attachments.Clear();
            GuaranteeRequestsPreview.Clear();
            GuaranteeOutputsPreview.Clear();
            foreach (TimelineItem item in artifacts.Timeline)
            {
                Timeline.Add(item);
            }

            foreach (AttachmentItem item in artifacts.Attachments)
            {
                Attachments.Add(item);
            }

            foreach (GuaranteeRequestPreviewItem item in artifacts.Requests)
            {
                GuaranteeRequestsPreview.Add(item);
            }

            foreach (GuaranteeOutputPreviewItem item in artifacts.Outputs)
            {
                GuaranteeOutputsPreview.Add(item);
            }

            LatestLetterOutput = GuaranteeOutputsPreview.FirstOrDefault(item => item.CanOpenLetter);
            LatestResponseOutput = GuaranteeOutputsPreview.FirstOrDefault(item => item.CanOpenResponse);
            RaiseGuaranteeFileSectionTextProperties();
        }

        private void RaiseGuaranteeFileSectionTextProperties()
        {
            OnPropertyChanged(nameof(GuaranteeRequestsSummaryText));
            OnPropertyChanged(nameof(GuaranteeRequestsContextLabel));
            OnPropertyChanged(nameof(TimelineSummaryText));
            OnPropertyChanged(nameof(TimelineStationsLabel));
            OnPropertyChanged(nameof(OutputsSummaryText));
            OnPropertyChanged(nameof(OutputsAvailabilityLabel));
            OnPropertyChanged(nameof(AttachmentsSummaryText));
            OnPropertyChanged(nameof(OfficialAttachmentsHeading));
            OnPropertyChanged(nameof(HasOutputShortcuts));
            OnPropertyChanged(nameof(OutputShortcutsSummaryText));
        }

        private void RaiseSelectionCommandStates()
        {
            foreach (ICommand command in new[]
                     {
                         OpenHistoryCommand,
                         RunSelectedInquiryCommand,
                         EditGuaranteeCommand,
                         CreateExtensionRequestCommand,
                         CreateReleaseRequestCommand,
                         CreateReductionRequestCommand,
                         CreateLiquidationRequestCommand,
                         CreateVerificationRequestCommand,
                         CreateReplacementRequestCommand,
                         CreateAnnulmentRequestCommand,
                         RegisterBankResponseCommand,
                         ShowAllAttachmentsCommand,
                         ShowGuaranteeRequestsCommand,
                         OpenGuaranteeFileCommand,
                         FocusSuggestedGuaranteeActionCommand
                        , FocusExecutiveSummaryCommand
                        , FocusRequestsSectionCommand
                        , FocusTimelineSectionCommand
                        , FocusAttachmentsSectionCommand
                        , FocusActionsSectionCommand
                        , FocusOutputsSectionCommand
                        , OpenRequestPreviewCommand
                        , RegisterRequestPreviewResponseCommand
                        , OpenRequestPreviewLetterCommand
                        , OpenRequestPreviewResponseCommand
                        , OpenLatestInquiryDialogCommand
                        , OpenLatestInquiryHistoryCommand
                        , OpenLatestInquiryLetterCommand
                        , OpenLatestInquiryResponseCommand
                        , FocusLatestInquirySectionCommand
                     })
            {
                if (command is RelayCommand relayCommand)
                {
                    relayCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private void RaiseInquiryCommandStates()
        {
            foreach (ICommand command in new[]
                     {
                         OpenLatestInquiryDialogCommand,
                         OpenLatestInquiryHistoryCommand,
                         OpenLatestInquiryLetterCommand,
                         OpenLatestInquiryResponseCommand,
                         FocusLatestInquirySectionCommand
                     })
            {
                if (command is RelayCommand relayCommand)
                {
                    relayCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string GlobalSearchText
        {
            get => _globalSearchText;
            set => SetProperty(ref _globalSearchText, value);
        }

        private static GuaranteeFileFocusArea ResolveLatestInquirySuggestedArea(OperationalInquiryResult? result)
        {
            return result?.InquiryKey switch
            {
                "guarantee.response-link-status" => GuaranteeFileFocusArea.Outputs,
                "guarantee.extension-timing" or "guarantee.outstanding-extension" or "guarantee.outstanding-release" or "guarantee.outstanding-liquidation" or "guarantee.reduction-source" => GuaranteeFileFocusArea.Requests,
                "guarantee.expired-without-extension" => GuaranteeFileFocusArea.Requests,
                "guarantee.release-evidence" or "guarantee.liquidation-evidence" => GuaranteeFileFocusArea.Outputs,
                "guarantee.last-event" => GuaranteeFileFocusArea.Series,
                "bank.pending-requests" or "supplier.latest-activity" => GuaranteeFileFocusArea.Requests,
                _ => GuaranteeFileFocusArea.None
            };
        }

        private static string ResolveLatestInquirySuggestedLabel(OperationalInquiryResult? result)
        {
            return ResolveLatestInquirySuggestedArea(result) switch
            {
                GuaranteeFileFocusArea.Requests => "انتقل إلى الطلبات المرتبطة",
                GuaranteeFileFocusArea.Series => "انتقل إلى الخط الزمني",
                GuaranteeFileFocusArea.Attachments => "انتقل إلى المرفقات",
                GuaranteeFileFocusArea.Outputs => "انتقل إلى المخرجات المرتبطة",
                GuaranteeFileFocusArea.Actions => "انتقل إلى الإجراءات",
                _ => "لا يوجد قسم مقترح"
            };
        }

        private void RememberLastFile(GuaranteeRow row)
        {
            SetLastFileState(_sessionCoordinator.RememberLastFile(row));
        }

        private void ResetGuaranteeFilters()
        {
            SetGuaranteeFilters(
                string.Empty,
                AllBanksLabel,
                AllTypesLabel,
                FilterOption.AllTimeStatuses);
        }

        private void SetGuaranteeFilters(
            string searchText,
            string selectedBank,
            string selectedGuaranteeType,
            FilterOption selectedTimeStatus)
        {
            bool changed = false;
            changed |= SetProperty(ref _searchText, searchText ?? string.Empty, nameof(SearchText));
            changed |= SetProperty(ref _selectedBank, string.IsNullOrWhiteSpace(selectedBank) ? AllBanksLabel : selectedBank, nameof(SelectedBank));
            changed |= SetProperty(ref _selectedGuaranteeType, string.IsNullOrWhiteSpace(selectedGuaranteeType) ? AllTypesLabel : selectedGuaranteeType, nameof(SelectedGuaranteeType));
            changed |= SetProperty(ref _selectedTimeStatus, selectedTimeStatus, nameof(SelectedTimeStatus));

            if (changed)
            {
                Refresh();
            }
        }

        private void SetLastFileState(ShellLastFileState state)
        {
            if (EqualityComparer<ShellLastFileState>.Default.Equals(_lastFileState, state))
            {
                return;
            }

            _lastFileState = state;
            OnPropertyChanged(nameof(HasLastFile));
            OnPropertyChanged(nameof(LastFileGuaranteeNo));
            OnPropertyChanged(nameof(LastFileSummary));
            if (ResumeLastFileCommand is RelayCommand relay)
            {
                relay.RaiseCanExecuteChanged();
            }

            _diagnostics.RecordEvent(
                "shell.session",
                "last-file-updated",
                new
                {
                    state.HasLastFile,
                    state.RootId,
                    state.GuaranteeNo,
                    state.Summary
                });
            WriteDiagnosticsState("last-file");
        }

        private void ActivateWorkspace(string key, FrameworkElement? content)
        {
            CurrentWorkspaceKey = key;
            OnPropertyChanged(nameof(CurrentWorkspaceDisplayTitle));
            ActiveWorkspaceContent = content;
            _diagnostics.RecordEvent(
                "shell.navigation",
                "workspace-activated",
                new
                {
                    WorkspaceKey = key,
                    ContentType = content?.GetType().Name ?? nameof(GuaranteesDashboardView)
                });
            WriteDiagnosticsState("workspace-activated");
        }

        private void WriteDiagnosticsState(string reason)
        {
            ShellDiagnosticsSelection diagnosticsSelection = ResolveDiagnosticsSelection();
            _diagnostics.UpdateShellState(new UiShellDiagnosticsState(
                DateTimeOffset.Now,
                reason,
                CurrentWorkspaceKey,
                ActiveWorkspaceContent?.GetType().Name ?? nameof(GuaranteesDashboardView),
                GlobalSearchText,
                SearchText,
                SelectedBank,
                SelectedGuaranteeType,
                SelectedTimeStatus?.Value.ToString() ?? string.Empty,
                Guarantees.Count,
                FooterSummary,
                PendingRequestCount,
                ExpiredCount,
                ExpiredFollowUpCount,
                ExpiringSoonCount,
                ActiveCount,
                HasLastFile,
                LastFileGuaranteeNo,
                LastFileSummary,
                diagnosticsSelection.GuaranteeId,
                diagnosticsSelection.RootGuaranteeId,
                diagnosticsSelection.GuaranteeNo,
                diagnosticsSelection.Supplier,
                diagnosticsSelection.Bank,
                SelectedOperationalInquiryOption?.Id ?? string.Empty,
                HasLatestInquiryResult));
        }

        private ShellDiagnosticsSelection ResolveDiagnosticsSelection()
        {
            if (string.Equals(CurrentWorkspaceKey, ShellWorkspaceKeys.Requests, StringComparison.Ordinal)
                && ActiveWorkspaceContent is RequestsWorkspaceSurface requestsWorkspace)
            {
                if (requestsWorkspace.SelectedDiagnosticsItem is RequestListDisplayItem requestItem)
                {
                    return new ShellDiagnosticsSelection(
                        requestItem.Item.CurrentGuaranteeId,
                        requestItem.Item.RootGuaranteeId,
                        requestItem.GuaranteeNo,
                        requestItem.Supplier,
                        requestItem.Bank);
                }

                return new ShellDiagnosticsSelection(null, null, string.Empty, string.Empty, string.Empty);
            }

            return new ShellDiagnosticsSelection(
                SelectedGuarantee?.Id,
                SelectedGuarantee?.RootId,
                SelectedGuarantee?.GuaranteeNo ?? string.Empty,
                SelectedGuarantee?.Beneficiary ?? string.Empty,
                SelectedGuarantee?.Bank ?? string.Empty);
        }

        private sealed record ShellDiagnosticsSelection(
            int? GuaranteeId,
            int? RootGuaranteeId,
            string GuaranteeNo,
            string Supplier,
            string Bank);

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
