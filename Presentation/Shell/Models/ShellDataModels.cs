using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Microsoft.Win32;

namespace GuaranteeManager
{
    public sealed class GuaranteeRow : INotifyPropertyChanged
    {
        private static readonly Dictionary<string, ImageSource> BankLogoCache = new(StringComparer.OrdinalIgnoreCase);

        private GuaranteeRow(
            Guarantee guarantee,
            string beneficiary,
            string amount,
            string amountDescription,
            string issueDate,
            string expiryDate,
            string expiryRemainingLabel,
            string timeStatus,
            Tone timeTone,
            string workStatus,
            Tone workTone,
            GuaranteeActionProfile actionProfile)
        {
            Id = guarantee.Id;
            RootId = guarantee.RootId ?? guarantee.Id;
            IsCurrentVersion = guarantee.IsCurrent;
            Beneficiary = beneficiary;
            Bank = guarantee.Bank;
            GuaranteeNo = guarantee.GuaranteeNo;
            Amount = amount;
            AmountValue = guarantee.Amount;
            AmountDescription = amountDescription;
            VersionLabel = guarantee.VersionLabel;
            IssueDate = issueDate;
            ExpiryDate = expiryDate;
            ExpiryDateValue = guarantee.ExpiryDate;
            ExpiryRemainingLabel = expiryRemainingLabel;
            GuaranteeType = string.IsNullOrWhiteSpace(guarantee.GuaranteeType) ? "---" : guarantee.GuaranteeType;
            ReferenceFieldLabel = GetReferenceFieldLabel(guarantee.ReferenceType);
            ReferenceNumber = string.IsNullOrWhiteSpace(guarantee.ReferenceNumber) ? "---" : guarantee.ReferenceNumber;
            Attachments = guarantee.Attachments ?? new List<AttachmentRecord>();
            TimeStatus = timeStatus;
            WorkStatus = workStatus;
            TimeBrush = TonePalette.Foreground(timeTone);
            TimeBackground = TonePalette.Background(timeTone);
            TimeBorder = TonePalette.Border(timeTone);
            WorkBrush = TonePalette.Foreground(workTone);
            WorkBackground = TonePalette.Background(workTone);
            WorkBorder = TonePalette.Border(workTone);
            ActionProfile = actionProfile;
        }

        public int Id { get; }
        public int RootId { get; }
        public bool IsCurrentVersion { get; }
        public string GuaranteeNo { get; }
        public string AutomationKey => BuildAutomationKey(GuaranteeNo, Id);
        public string RowAutomationName => $"{GuaranteeNo} | {Beneficiary}";
        public string RowMoreAutomationId => BuildRowActionAutomationId("More");
        public string RowMoreAutomationName => $"المزيد | {GuaranteeNo}";
        public string RowRequestsAutomationId => BuildRowActionAutomationId("Requests");
        public string RowRequestsAutomationName => $"الطلبات | {GuaranteeNo}";
        public string Beneficiary { get; }
        public string Bank { get; }
        public ImageSource BankLogo => GetBankLogo(Bank);
        public string Amount { get; }
        public decimal AmountValue { get; }
        public string AmountDescription { get; }
        public string VersionLabel { get; }
        public string IssueDate { get; }
        public string ExpiryDate { get; }
        public DateTime ExpiryDateValue { get; }
        public string ExpiryRemainingLabel { get; }
        public string GuaranteeType { get; }
        public string ReferenceFieldLabel { get; }
        public string ReferenceNumber { get; }
        public IReadOnlyList<AttachmentRecord> Attachments { get; private set; }
        public string TimeStatus { get; }
        public string WorkStatus { get; }
        public Brush TimeBrush { get; }
        public Brush TimeBackground { get; }
        public Brush TimeBorder { get; }
        public Brush WorkBrush { get; }
        public Brush WorkBackground { get; }
        public Brush WorkBorder { get; }
        public GuaranteeActionProfile ActionProfile { get; }
        public bool HasPendingRequests => ActionProfile.PendingRequestCount > 0;
        public bool HasWorkflowOutputs => ActionProfile.OutputCount > 0;
        public bool HasResponseOutputs => ActionProfile.ResponseOutputCount > 0;
        public bool HasLetterOutputs => ActionProfile.LetterOutputCount > 0;
        public bool HasOfficialAttachments => Attachments.Count > 0;
        public ActionEligibility OpenFileAction => ActionProfile.OpenFileAction;
        public ActionEligibility RegisterResponseAction => ActionProfile.RegisterResponseAction;
        public ActionEligibility OpenAttachmentsAction => Attachments.Count > 0
            ? ActionEligibility.Enabled($"يوجد {Attachments.Count.ToString("N0", CultureInfo.InvariantCulture)} مرفق رسمي متاح للفتح من هذه اللوحة.")
            : ActionProfile.OpenAttachmentsAction;
        public ActionEligibility OpenRequestsAction => ActionProfile.OpenRequestsAction;
        public ActionEligibility EditAction => ActionProfile.EditAction;
        public ActionEligibility ReleaseAction => ActionProfile.ReleaseAction;
        public ActionEligibility ExtensionAction => ActionProfile.ExtensionAction;
        public ActionEligibility ReductionAction => ActionProfile.ReductionAction;
        public ActionEligibility LiquidationAction => ActionProfile.LiquidationAction;
        public ActionEligibility VerificationAction => ActionProfile.VerificationAction;
        public ActionEligibility ReplacementAction => ActionProfile.ReplacementAction;
        public string ActionSummaryTitle => ActionProfile.SummaryTitle;
        public string ActionSummaryDetail => ActionProfile.SummaryDetail;
        public string SuggestedFocusLabel => ActionProfile.SuggestedFocusLabel;
        public GuaranteeFileFocusArea SuggestedFocusArea => ActionProfile.SuggestedFocusArea;
        public bool HasSuggestedFocus => ActionProfile.SuggestedFocusArea != GuaranteeFileFocusArea.None;

        public event PropertyChangedEventHandler? PropertyChanged;

        public static ImageSource ResolveBankLogo(string bankName) => GetBankLogo(bankName);

        public void SetAttachments(IReadOnlyList<AttachmentRecord> attachments)
        {
            Attachments = attachments;
            OnPropertyChanged(nameof(Attachments));
            OnPropertyChanged(nameof(HasOfficialAttachments));
            OnPropertyChanged(nameof(OpenAttachmentsAction));
        }

        private string BuildRowActionAutomationId(string action)
            => $"Guarantees.RowAction.{action}.{AutomationKey}";

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static string BuildAutomationKey(string value, int fallbackId)
        {
            string key = string.Concat((value ?? string.Empty).Where(char.IsLetterOrDigit));
            return string.IsNullOrWhiteSpace(key)
                ? fallbackId.ToString(CultureInfo.InvariantCulture)
                : key;
        }

        public static GuaranteeRow FromGuarantee(Guarantee guarantee, IReadOnlyList<WorkflowRequest> relatedRequests)
        {
            bool hasPendingRequest = relatedRequests.Any(request => request.Status == RequestStatus.Pending);
            (string timeStatus, Tone timeTone) = GetTimeStatus(guarantee);
            (string workStatus, Tone workTone) = GetWorkStatus(guarantee, hasPendingRequest);
            string beneficiary = string.IsNullOrWhiteSpace(guarantee.Beneficiary)
                ? guarantee.Supplier
                : guarantee.Beneficiary;
            GuaranteeActionProfile actionProfile = GuaranteeActionProfile.Build(guarantee, relatedRequests);

            return new GuaranteeRow(
                guarantee,
                string.IsNullOrWhiteSpace(beneficiary) ? "---" : beneficiary,
                FormatAmount(guarantee.Amount),
                NumberToArabicWords(guarantee.Amount) + " ريال سعودي",
                FormatDate(guarantee.CreatedAt),
                FormatDate(guarantee.ExpiryDate),
                GetExpiryRemainingLabel(guarantee.ExpiryDate),
                timeStatus,
                timeTone,
                workStatus,
                workTone,
                actionProfile);
        }

        private static (string Label, Tone Tone) GetTimeStatus(Guarantee guarantee)
        {
            if (guarantee.IsExpired)
            {
                return ("منتهي", Tone.Danger);
            }

            if (guarantee.IsExpiringSoon)
            {
                return ("قريب الانتهاء", Tone.Warning);
            }

            return ("نشط", Tone.Success);
        }

        private static (string Label, Tone Tone) GetWorkStatus(Guarantee guarantee, bool hasPendingRequest)
        {
            if (hasPendingRequest)
            {
                return ("قيد التنفيذ", Tone.Info);
            }

            return guarantee.LifecycleStatus switch
            {
                GuaranteeLifecycleStatus.Active => ("نشط", Tone.Success),
                GuaranteeLifecycleStatus.Expired => ("منتهي الصلاحية", Tone.Danger),
                GuaranteeLifecycleStatus.Released => ("مفرج", Tone.Info),
                GuaranteeLifecycleStatus.Liquidated => ("مسيّل", Tone.Danger),
                GuaranteeLifecycleStatus.Replaced => ("مستبدل", Tone.Info),
                _ => (GuaranteeLifecycleStatusDisplay.GetLabel(guarantee.LifecycleStatus), Tone.Info)
            };
        }

        private static string GetExpiryRemainingLabel(DateTime expiryDate)
        {
            int days = (expiryDate.Date - DateTime.Today).Days;
            if (days > 0)
            {
                return $"({days.ToString("N0", CultureInfo.InvariantCulture)} يوم متبقية)";
            }

            if (days == 0)
            {
                return "(ينتهي اليوم)";
            }

            return $"({Math.Abs(days).ToString("N0", CultureInfo.InvariantCulture)} يوم متأخرة)";
        }

        private static string GetReferenceFieldLabel(GuaranteeReferenceType referenceType) => referenceType switch
        {
            GuaranteeReferenceType.Contract => "رقم العقد",
            GuaranteeReferenceType.PurchaseOrder => "رقم أمر الشراء",
            _ => "رقم المرجع"
        };

        private static string FormatDate(DateTime date) => date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);

        private static string FormatAmount(decimal amount) => amount.ToString("N0", CultureInfo.InvariantCulture);

        private static string NumberToArabicWords(decimal amount)
        {
            long n = (long)Math.Round(amount);
            if (n == 0) return "صفر";
            var parts = new System.Collections.Generic.List<string>();
            if (n >= 1_000_000_000) { long g = n / 1_000_000_000; n %= 1_000_000_000; parts.Add(g == 1 ? "مليار" : g == 2 ? "ملياران" : ArabicGroupWord(g, "مليارات", "مليار")); }
            if (n >= 1_000_000)     { long g = n / 1_000_000;     n %= 1_000_000;     parts.Add(g == 1 ? "مليون" : g == 2 ? "مليونان" : ArabicGroupWord(g, "ملايين", "مليون")); }
            if (n >= 1_000)         { long g = n / 1_000;         n %= 1_000;         parts.Add(g == 1 ? "ألف"   : g == 2 ? "ألفان"   : ArabicGroupWord(g, "آلاف", "ألف")); }
            if (n >= 100) { parts.Add(ArabicHundreds((int)(n / 100))); n %= 100; }
            if (n > 0)    { parts.Add(ArabicUnderHundred((int)n)); }
            return string.Join(" و", parts);
        }

        private static string ArabicGroupWord(long count, string fewForm, string manyForm)
        {
            string c = ArabicSmallCount(count);
            return count >= 3 && count <= 10 ? $"{c} {fewForm}" : $"{c} {manyForm}";
        }

        private static string ArabicSmallCount(long n)
        {
            if (n <= 19) return ArabicUnderHundred((int)n);
            if (n < 100) return ArabicUnderHundred((int)(n % 10)) + " و" + ArabicTens((int)(n / 10));
            return ArabicHundreds((int)(n / 100)) + (n % 100 > 0 ? " و" + ArabicSmallCount(n % 100) : "");
        }

        private static string ArabicUnderHundred(int n) => n switch
        {
            1  => "واحد",       2  => "اثنان",      3  => "ثلاثة",
            4  => "أربعة",      5  => "خمسة",       6  => "ستة",
            7  => "سبعة",       8  => "ثمانية",     9  => "تسعة",
            10 => "عشرة",       11 => "أحد عشر",    12 => "اثنا عشر",
            13 => "ثلاثة عشر",  14 => "أربعة عشر",  15 => "خمسة عشر",
            16 => "ستة عشر",    17 => "سبعة عشر",   18 => "ثمانية عشر",
            19 => "تسعة عشر",
            _ when n % 10 == 0 => ArabicTens(n / 10),
            _ => ArabicUnderHundred(n % 10) + " و" + ArabicTens(n / 10)
        };

        private static string ArabicTens(int tens) => tens switch
        {
            2 => "عشرون",   3 => "ثلاثون",  4 => "أربعون",
            5 => "خمسون",   6 => "ستون",    7 => "سبعون",
            8 => "ثمانون",  9 => "تسعون",   _ => ""
        };

        private static string ArabicHundreds(int h) => h switch
        {
            1 => "مئة",      2 => "مئتان",    3 => "ثلاثمئة",
            4 => "أربعمئة", 5 => "خمسمئة",  6 => "ستمئة",
            7 => "سبعمئة",  8 => "ثمانمئة", 9 => "تسعمئة",
            _ => ""
        };

        private static ImageSource GetBankLogo(string bankName)
        {
            string fileName = bankName switch
            {
                "البنك الأهلي" or "البنك الأهلي السعودي" => "snb_logo.png",
                "مصرف الراجحي" => "alrajhi_logo.png",
                "بنك الرياض" => "riyad_logo.png",
                "بنك ساب" or "البنك السعودي الأول" => "sab_logo.png",
                "البنك العربي الوطني" => "anb_logo.png",
                "البنك السعودي للاستثمار" => "saib_logo.png",
                "مصرف الإنماء" => "alinma_logo.png",
                "البنك السعودي الفرنسي" => "bsf_logo.png",
                "بنك البلاد" => "albilad_logo.png",
                "بنك الجزيرة" => "aljazira_logo.png",
                "بنك الخليج الدولي" or "بنك الخليج الدولي – السعودية" => "gib_logo.png",
                "بنك الإمارات دبي الوطني" => "emirates_nbd_logo.png",
                "بنك أبوظبي الأول" => "fab_logo.png",
                "بنك أبوظبي التجاري" => "adcb_logo.png",
                "بنك الكويت الوطني" => "nbk_logo.png",
                "بنك البحرين الوطني" => "nbb_logo.png",
                "بنك مسقط" => "muscat_logo.png",
                "بنك قطر الوطني" => "qnb_logo.png",
                _ => "snb_logo.png"
            };

            return LoadBankLogo(fileName);
        }

        private static ImageSource LoadBankLogo(string fileName)
        {
            string logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Logos", fileName);
            if (!File.Exists(logoPath))
            {
                logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Logos", "snb_logo.png");
            }

            if (BankLogoCache.TryGetValue(logoPath, out ImageSource? cachedLogo))
            {
                return cachedLogo;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(logoPath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            BankLogoCache[logoPath] = image;
            return image;
        }
    }

    public sealed record ActionEligibility(bool IsEnabled, string Hint)
    {
        public static ActionEligibility Enabled(string hint) => new(true, hint);
        public static ActionEligibility Disabled(string hint) => new(false, hint);
    }

    public sealed class GuaranteeActionProfile
    {
        private GuaranteeActionProfile(
            int pendingRequestCount,
            int totalRequestCount,
            int outputCount,
            int letterOutputCount,
            int responseOutputCount,
            ActionEligibility openFileAction,
            ActionEligibility registerResponseAction,
            ActionEligibility openAttachmentsAction,
            ActionEligibility openRequestsAction,
            ActionEligibility editAction,
            ActionEligibility releaseAction,
            ActionEligibility extensionAction,
            ActionEligibility reductionAction,
            ActionEligibility liquidationAction,
            ActionEligibility verificationAction,
            ActionEligibility replacementAction,
            string summaryTitle,
            string summaryDetail,
            GuaranteeFileFocusArea suggestedFocusArea,
            string suggestedFocusLabel)
        {
            PendingRequestCount = pendingRequestCount;
            TotalRequestCount = totalRequestCount;
            OutputCount = outputCount;
            LetterOutputCount = letterOutputCount;
            ResponseOutputCount = responseOutputCount;
            OpenFileAction = openFileAction;
            RegisterResponseAction = registerResponseAction;
            OpenAttachmentsAction = openAttachmentsAction;
            OpenRequestsAction = openRequestsAction;
            EditAction = editAction;
            ReleaseAction = releaseAction;
            ExtensionAction = extensionAction;
            ReductionAction = reductionAction;
            LiquidationAction = liquidationAction;
            VerificationAction = verificationAction;
            ReplacementAction = replacementAction;
            SummaryTitle = summaryTitle;
            SummaryDetail = summaryDetail;
            SuggestedFocusArea = suggestedFocusArea;
            SuggestedFocusLabel = suggestedFocusLabel;
        }

        public int PendingRequestCount { get; }
        public int TotalRequestCount { get; }
        public int OutputCount { get; }
        public int LetterOutputCount { get; }
        public int ResponseOutputCount { get; }
        public ActionEligibility OpenFileAction { get; }
        public ActionEligibility RegisterResponseAction { get; }
        public ActionEligibility OpenAttachmentsAction { get; }
        public ActionEligibility OpenRequestsAction { get; }
        public ActionEligibility EditAction { get; }
        public ActionEligibility ReleaseAction { get; }
        public ActionEligibility ExtensionAction { get; }
        public ActionEligibility ReductionAction { get; }
        public ActionEligibility LiquidationAction { get; }
        public ActionEligibility VerificationAction { get; }
        public ActionEligibility ReplacementAction { get; }
        public string SummaryTitle { get; }
        public string SummaryDetail { get; }
        public GuaranteeFileFocusArea SuggestedFocusArea { get; }
        public string SuggestedFocusLabel { get; }

        public static GuaranteeActionProfile Build(Guarantee guarantee, IReadOnlyList<WorkflowRequest> requests)
        {
            int pendingCount = requests.Count(request => request.Status == RequestStatus.Pending);
            int outputCount = requests.Count(request => request.HasLetter || request.HasResponseDocument);
            int letterCount = requests.Count(request => request.HasLetter);
            int responseCount = requests.Count(request => request.HasResponseDocument);
            bool archivedVersion = !guarantee.IsCurrent;
            string archivedVersionHint = $"هذا إصدار محفوظ ({guarantee.VersionLabel}) للعرض والمراجعة فقط. الإجراءات التشغيلية تبدأ من الإصدار الحالي.";

            bool hasPendingType(RequestType type) => requests.Any(request => request.Status == RequestStatus.Pending && request.Type == type);
            bool lifecycleClosed = guarantee.LifecycleStatus is GuaranteeLifecycleStatus.Released or GuaranteeLifecycleStatus.Liquidated or GuaranteeLifecycleStatus.Replaced or GuaranteeLifecycleStatus.Closed;
            bool lifecycleActionBlocked = archivedVersion || guarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active;

            string blockedLifecycleHint = archivedVersion
                ? archivedVersionHint
                : guarantee.LifecycleStatus switch
            {
                GuaranteeLifecycleStatus.Expired => "حالة الضمان التشغيلية منتهية الصلاحية، لذلك لا يمكن إنشاء طلبات تشغيلية جديدة عليه قبل معالجة حالته.",
                GuaranteeLifecycleStatus.Released => "تم الإفراج عن هذا الضمان، لذلك لا تظهر عليه إجراءات متابعة إنشائية جديدة عادةً.",
                GuaranteeLifecycleStatus.Liquidated => "هذا الضمان مُسيّل بالفعل، لذلك أغلب إجراءات التغيير الجديدة غير منطقية عليه.",
                GuaranteeLifecycleStatus.Replaced => "هذا الضمان استُبدل بالفعل، لذلك إجراءات التعديل الإنشائي تنتقل غالبًا إلى الضمان البديل.",
                GuaranteeLifecycleStatus.Closed => "هذا السجل من حالة قديمة مغلقة، لذا لا يوصى ببدء إجراءات تشغيلية جديدة عليه.",
                _ => string.Empty
            };

            ActionEligibility enableWhen(bool condition, string enabledHint, string disabledHint)
                => condition ? ActionEligibility.Enabled(enabledHint) : ActionEligibility.Disabled(disabledHint);

            ActionEligibility registerResponse = archivedVersion
                ? ActionEligibility.Disabled(archivedVersionHint)
                : enableWhen(
                    pendingCount > 0,
                    $"يوجد {pendingCount.ToString("N0", CultureInfo.InvariantCulture)} طلب معلق يمكن تسجيل رد البنك عليه من هذا الملف.",
                    "لا يوجد طلب معلق لهذا الضمان حاليًا، لذلك لا يتوفر تسجيل رد مباشر.");

            ActionEligibility openRequests = enableWhen(
                requests.Count > 0,
                $"يوجد {requests.Count.ToString("N0", CultureInfo.InvariantCulture)} طلب مرتبط بهذا الضمان، ويمكن فتحها من هنا.",
                "لا توجد طلبات مرتبطة بهذه السلسلة حتى الآن.");

            ActionEligibility openAttachments = enableWhen(
                guarantee.Attachments.Count > 0,
                $"يوجد {guarantee.Attachments.Count.ToString("N0", CultureInfo.InvariantCulture)} مرفق رسمي على هذا الضمان.",
                "لا توجد مرفقات رسمية على هذا الضمان حاليًا.");

            ActionEligibility edit = archivedVersion
                ? ActionEligibility.Disabled(archivedVersionHint)
                : lifecycleClosed
                ? ActionEligibility.Enabled($"السجل في حالة {guarantee.LifecycleStatusLabel}. ما زال التعديل متاحًا، لكنه سينشئ إصدارًا جديدًا ينبغي مراجعته بعناية.")
                : ActionEligibility.Enabled("التعديل متاح وسيُحفظ كسجل إصدار جديد مع الإبقاء على التاريخ الكامل.");

            ActionEligibility release = BuildLifecycleAction(
                lifecycleActionBlocked,
                hasPendingType(RequestType.Release),
                blockedLifecycleHint,
                "يوجد طلب إفراج معلق بالفعل لهذا الضمان.",
                "طلب الإفراج متاح عند اكتمال مستندات إنهاء الالتزام.");

            ActionEligibility extension = BuildLifecycleAction(
                lifecycleActionBlocked,
                hasPendingType(RequestType.Extension),
                blockedLifecycleHint,
                "يوجد طلب تمديد معلق بالفعل لهذا الضمان.",
                guarantee.IsExpired
                    ? "الضمان منتهٍ حاليًا، وطلب التمديد مناسب لإغلاق فجوة المتابعة."
                    : guarantee.IsExpiringSoon
                        ? "الضمان قريب الانتهاء، وطلب التمديد مناسب في هذا التوقيت."
                        : "طلب التمديد متاح لتمديد استباقي عند الحاجة.");

            ActionEligibility reduction = BuildLifecycleAction(
                lifecycleActionBlocked,
                hasPendingType(RequestType.Reduction),
                blockedLifecycleHint,
                "يوجد طلب تخفيض معلق بالفعل لهذا الضمان.",
                "طلب التخفيض متاح عند الحاجة لتعديل قيمة الضمان.");

            ActionEligibility liquidation = BuildLifecycleAction(
                lifecycleActionBlocked,
                hasPendingType(RequestType.Liquidation),
                blockedLifecycleHint,
                "يوجد طلب تسييل معلق بالفعل لهذا الضمان.",
                guarantee.IsExpired
                    ? "الضمان منتهٍ، وطلب التسييل متاح إذا كانت مبررات المطالبة مكتملة."
                    : "طلب التسييل متاح عند الحاجة لإثبات المطالبة وتنفيذ أثرها.");

            ActionEligibility verification = BuildLifecycleAction(
                lifecycleActionBlocked,
                hasPendingType(RequestType.Verification),
                blockedLifecycleHint,
                "يوجد طلب تحقق معلق بالفعل لهذا الضمان.",
                "طلب التحقق متاح لمراجعة صحة المستندات أو إثبات الحالة.");

            ActionEligibility replacement = BuildLifecycleAction(
                lifecycleActionBlocked,
                hasPendingType(RequestType.Replacement),
                blockedLifecycleHint,
                "يوجد طلب استبدال معلق بالفعل لهذا الضمان.",
                "طلب الاستبدال متاح عند الحاجة لإصدار ضمان بديل.");

            string summaryTitle;
            string summaryDetail;
            GuaranteeFileFocusArea suggestedArea;
            string suggestedLabel;

            WorkflowRequest? latestPending = requests
                .Where(request => request.Status == RequestStatus.Pending)
                .OrderByDescending(request => request.RequestDate)
                .ThenByDescending(request => request.SequenceNumber)
                .FirstOrDefault();

            if (archivedVersion)
            {
                summaryTitle = $"الإصدار {guarantee.VersionLabel} محفوظ للمراجعة";
                summaryDetail = requests.Count == 0
                    ? "لا توجد طلبات مرتبطة مباشرة بهذا الإصدار. راجع بياناته ومرفقاته الرسمية من اللوحة الجانبية."
                    : $"يوجد {requests.Count.ToString("N0", CultureInfo.InvariantCulture)} طلب مرتبط بهذا الإصدار في السلسلة.";
                suggestedArea = guarantee.Attachments.Count > 0
                    ? GuaranteeFileFocusArea.Attachments
                    : GuaranteeFileFocusArea.Series;
                suggestedLabel = guarantee.Attachments.Count > 0 ? "راجع المرفقات" : "راجع السجل";
            }
            else if (pendingCount > 0)
            {
                summaryTitle = $"يوجد {pendingCount.ToString("N0", CultureInfo.InvariantCulture)} طلب قيد التنفيذ على هذا الملف";
                summaryDetail = latestPending == null
                    ? "المتابعة الأقرب الآن هي مراجعة الطلبات المعلقة وتسجيل رد البنك عند وصوله."
                    : $"أقرب نقطة متابعة الآن: {latestPending.TypeLabel} بتاريخ {latestPending.RequestDate:yyyy/MM/dd}.";
                suggestedArea = GuaranteeFileFocusArea.Requests;
                suggestedLabel = "اذهب إلى الطلبات";
            }
            else if (guarantee.NeedsExpiryFollowUp)
            {
                summaryTitle = "الضمان منتهٍ ويحتاج متابعة مباشرة";
                summaryDetail = "لا توجد طلبات معلقة حاليًا، لذا الأفضل البدء من الطلبات أو الاستعلامات لمعرفة سبب بقاء الضمان دون إغلاق.";
                suggestedArea = GuaranteeFileFocusArea.Requests;
                suggestedLabel = "راجع الطلبات";
            }
            else if (outputCount > 0)
            {
                summaryTitle = "توجد مخرجات جاهزة للفتح من داخل الملف";
                summaryDetail = $"المتوفر الآن: {letterCount.ToString("N0", CultureInfo.InvariantCulture)} خطاب طلب و{responseCount.ToString("N0", CultureInfo.InvariantCulture)} رد بنك مرتبط.";
                suggestedArea = GuaranteeFileFocusArea.Outputs;
                suggestedLabel = "افتح المخرجات";
            }
            else if (guarantee.IsExpiringSoon)
            {
                summaryTitle = "الضمان قريب الانتهاء";
                summaryDetail = "راجع التمديد أو المستندات الداعمة قبل الوصول إلى حالة منتهٍ تحتاج متابعة.";
                suggestedArea = GuaranteeFileFocusArea.ExecutiveSummary;
                suggestedLabel = "ارجع إلى الملخص";
            }
            else if (guarantee.Attachments.Count > 0)
            {
                summaryTitle = "المرفقات الرسمية متاحة داخل الملف";
                summaryDetail = "يمكنك البدء من المرفقات إذا كان المطلوب مراجعة المستند الرسمي قبل أي إجراء.";
                suggestedArea = GuaranteeFileFocusArea.Attachments;
                suggestedLabel = "افتح المرفقات";
            }
            else
            {
                summaryTitle = "الملف مستقر تشغيليًا في الوقت الحالي";
                summaryDetail = "لا توجد طلبات معلقة أو مخرجات ملحّة الآن، ويمكن استخدام الملف كنقطة مراجعة سريعة.";
                suggestedArea = GuaranteeFileFocusArea.ExecutiveSummary;
                suggestedLabel = "افتح الملخص";
            }

            return new GuaranteeActionProfile(
                pendingCount,
                requests.Count,
                outputCount,
                letterCount,
                responseCount,
                ActionEligibility.Enabled(BuildOpenFileHint(suggestedArea)),
                registerResponse,
                openAttachments,
                openRequests,
                edit,
                release,
                extension,
                reduction,
                liquidation,
                verification,
                replacement,
                summaryTitle,
                summaryDetail,
                suggestedArea,
                suggestedLabel);
        }

        private static string BuildOpenFileHint(GuaranteeFileFocusArea suggestedArea)
        {
            return suggestedArea switch
            {
                GuaranteeFileFocusArea.Requests => "يفتح شاشة الطلبات مفلترة على هذا الضمان حتى تبدأ من نقطة المتابعة الحالية.",
                GuaranteeFileFocusArea.Outputs => "يفتح شاشة الطلبات للوصول إلى المخرجات والأثر المرتبط بهذا الضمان.",
                GuaranteeFileFocusArea.Attachments => "ينقلك إلى لوحة الضمان الجانبية عند الأدلة والمرفقات الرسمية.",
                GuaranteeFileFocusArea.Series => "ينقلك إلى لوحة الضمان الجانبية عند الخط الزمني لهذا الضمان.",
                GuaranteeFileFocusArea.Actions => "ينقلك إلى الإجراءات السريعة المناسبة للحالة الحالية داخل لوحة الضمان.",
                GuaranteeFileFocusArea.ExecutiveSummary => "ينقلك إلى الضمان المحدد داخل المحفظة.",
                _ => "ينقلك إلى الوجهة الأنسب لحالة الضمان الحالية."
            };
        }

        private static ActionEligibility BuildLifecycleAction(
            bool lifecycleClosed,
            bool hasPendingSameType,
            string closedReason,
            string duplicatePendingReason,
            string enabledHint)
        {
            if (lifecycleClosed)
            {
                return ActionEligibility.Disabled(closedReason);
            }

            if (hasPendingSameType)
            {
                return ActionEligibility.Disabled(duplicatePendingReason);
            }

            return ActionEligibility.Enabled(enabledHint);
        }
    }

    public enum TimelineEvidenceActionKind
    {
        None,
        Attachment,
        RequestLetter,
        ResponseDocument,
        OfficialAttachment
    }

    public sealed class TimelineItem
    {
        public TimelineItem(
            DateTime timestamp,
            string title,
            string detail,
            string status,
            Tone tone,
            TimelineEvidenceActionKind evidenceActionKind = TimelineEvidenceActionKind.None,
            AttachmentRecord? evidenceAttachment = null,
            WorkflowRequest? evidenceRequest = null,
            int? evidenceGuaranteeId = null,
            string evidenceKey = "")
            : this(
                timestamp.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                title,
                detail,
                status,
                tone,
                evidenceActionKind,
                evidenceAttachment,
                evidenceRequest,
                evidenceGuaranteeId,
                evidenceKey)
        {
        }

        public TimelineItem(string date, string title, string detail, string status, Tone tone)
            : this(date, string.Empty, title, detail, status, tone)
        {
        }

        private TimelineItem(
            string date,
            string time,
            string title,
            string detail,
            string status,
            Tone tone,
            TimelineEvidenceActionKind evidenceActionKind = TimelineEvidenceActionKind.None,
            AttachmentRecord? evidenceAttachment = null,
            WorkflowRequest? evidenceRequest = null,
            int? evidenceGuaranteeId = null,
            string evidenceKey = "")
        {
            Date = date;
            Time = time;
            Title = title;
            Detail = detail;
            Status = status;
            Brush = TonePalette.Foreground(tone);
            StatusBackground = TonePalette.Background(tone);
            StatusBorder = TonePalette.Border(tone);
            EvidenceKey = evidenceKey ?? string.Empty;
            EvidenceActionKind = NormalizeEvidenceActionKind(evidenceActionKind, evidenceAttachment, evidenceRequest, evidenceGuaranteeId);
            EvidenceAttachment = EvidenceActionKind == TimelineEvidenceActionKind.Attachment ? evidenceAttachment : null;
            EvidenceRequest = EvidenceActionKind is TimelineEvidenceActionKind.RequestLetter or TimelineEvidenceActionKind.ResponseDocument
                ? evidenceRequest
                : null;
            EvidenceGuaranteeId = EvidenceActionKind == TimelineEvidenceActionKind.OfficialAttachment
                ? evidenceGuaranteeId
                : evidenceGuaranteeId ?? EvidenceAttachment?.GuaranteeId ?? EvidenceRequest?.BaseVersionId;
            EvidenceActionLabel = BuildEvidenceActionLabel(EvidenceActionKind, EvidenceAttachment, EvidenceRequest);
            EvidenceActionHint = BuildEvidenceActionHint(EvidenceActionKind, EvidenceAttachment, EvidenceRequest);
            EvidenceActionAutomationId = BuildEvidenceActionAutomationId(
                EvidenceActionKind,
                EvidenceAttachment,
                EvidenceRequest,
                EvidenceGuaranteeId,
                EvidenceKey);
        }

        public string Date { get; }
        public string Time { get; }
        public string Title { get; }
        public string Detail { get; }
        public string Status { get; }
        public Brush Brush { get; }
        public Brush StatusBackground { get; }
        public Brush StatusBorder { get; }
        public TimelineEvidenceActionKind EvidenceActionKind { get; }
        public string EvidenceKey { get; }
        public AttachmentRecord? EvidenceAttachment { get; }
        public WorkflowRequest? EvidenceRequest { get; }
        public int? EvidenceGuaranteeId { get; }
        public bool HasEvidenceAction => EvidenceActionKind != TimelineEvidenceActionKind.None;
        public bool IsAttachEvidenceAction => EvidenceActionKind == TimelineEvidenceActionKind.OfficialAttachment
            || EvidenceActionKind == TimelineEvidenceActionKind.ResponseDocument && EvidenceRequest?.HasResponseDocument != true;
        public bool IsOpenEvidenceAction => HasEvidenceAction && !IsAttachEvidenceAction;
        public double EvidenceActionWidth => 20d;
        public double EvidenceActionHeight => 20d;
        public string EvidenceActionLabel { get; }
        public string EvidenceActionHint { get; }
        public string EvidenceActionAutomationId { get; }

        public static TimelineItem FromRequest(WorkflowRequest request)
        {
            Tone tone = request.Status switch
            {
                RequestStatus.Executed => Tone.Success,
                RequestStatus.Pending => Tone.Warning,
                RequestStatus.Rejected or RequestStatus.Cancelled => Tone.Danger,
                _ => Tone.Info
            };

            return new TimelineItem(
                request.RequestDate,
                request.TypeLabel,
                WorkflowRequestDisplayText.BuildDetail(request),
                request.StatusLabel,
                tone);
        }

        public static TimelineItem FromEvent(
            GuaranteeTimelineEvent timelineEvent,
            IReadOnlyDictionary<int, WorkflowRequest>? requestsById = null,
            IReadOnlyDictionary<int, AttachmentRecord>? attachmentsById = null,
            IReadOnlyDictionary<string, AttachmentRecord>? attachmentsByEventKey = null)
        {
            ResolveEvidence(
                timelineEvent,
                requestsById,
                attachmentsById,
                attachmentsByEventKey,
                out TimelineEvidenceActionKind evidenceActionKind,
                out AttachmentRecord? evidenceAttachment,
                out WorkflowRequest? evidenceRequest);

            return new TimelineItem(
                timelineEvent.OccurredAt,
                timelineEvent.Title,
                timelineEvent.Details,
                timelineEvent.Status,
                ParseTone(timelineEvent.ToneKey),
                evidenceActionKind,
                evidenceAttachment,
                evidenceRequest,
                timelineEvent.GuaranteeId,
                timelineEvent.EventKey);
        }

        public static TimelineItem RequestCreated(WorkflowRequest request)
        {
            string detail = $"القيمة المطلوبة: {request.RequestedValueLabel}";
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                detail += $" | {request.Notes.Trim()}";
            }

            return new TimelineItem(
                request.RequestDate,
                request.TypeLabel,
                detail,
                request.Status == RequestStatus.Pending ? request.StatusLabel : "مسجل",
                request.Status == RequestStatus.Pending ? Tone.Warning : Tone.Info,
                TimelineEvidenceActionKind.RequestLetter,
                evidenceRequest: request,
                evidenceKey: $"workflow-request-created:{request.Id.ToString(CultureInfo.InvariantCulture)}");
        }

        public static TimelineItem BankResponse(WorkflowRequest request, string resultVersionLabel)
        {
            string detail = WorkflowRequestDisplayText.BuildDetail(request);
            string effectDetail = BuildBankResponseEffectDetail(request, resultVersionLabel);
            if (!string.IsNullOrWhiteSpace(effectDetail))
            {
                detail += $" | {effectDetail}";
            }

            if (request.HasResponseDocument)
            {
                detail += " | رد البنك مرفق";
            }

            return new TimelineItem(
                request.ResponseRecordedAt!.Value,
                $"تسجيل رد {request.TypeLabel}",
                detail,
                request.StatusLabel,
                GetRequestTone(request.Status),
                TimelineEvidenceActionKind.ResponseDocument,
                evidenceRequest: request,
                evidenceKey: $"workflow-response:{request.Id.ToString(CultureInfo.InvariantCulture)}");
        }

        public static TimelineItem FromVersion(Guarantee version)
        {
            if (version.VersionNumber <= 1)
            {
                return new TimelineItem(
                    version.CreatedAt,
                    "إنشاء الضمان",
                    $"تم إنشاء الضمان بقيمة {version.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال وانتهاء {version.ExpiryDate:yyyy/MM/dd}.",
                    "مكتمل",
                    Tone.Success,
                    TimelineEvidenceActionKind.OfficialAttachment,
                    evidenceGuaranteeId: version.Id,
                    evidenceKey: $"guarantee-created:{version.Id.ToString(CultureInfo.InvariantCulture)}");
            }

            return new TimelineItem(
                version.CreatedAt,
                $"إصدار جديد {version.VersionLabel}",
                $"تم حفظ شروط هذا الإصدار: المبلغ {version.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال | الانتهاء {version.ExpiryDate:yyyy/MM/dd}.",
                "موثق",
                Tone.Info,
                TimelineEvidenceActionKind.OfficialAttachment,
                evidenceGuaranteeId: version.Id,
                evidenceKey: $"guarantee-version:{version.Id.ToString(CultureInfo.InvariantCulture)}");
        }

        public static TimelineItem AttachmentAdded(AttachmentRecord attachment)
        {
            string documentType = attachment.DocumentTypeLabel;
            string name = string.IsNullOrWhiteSpace(attachment.OriginalFileName)
                ? "مرفق رسمي"
                : attachment.OriginalFileName.Trim();

            return new TimelineItem(
                attachment.UploadedAt,
                $"إضافة مرفق {documentType}",
                name,
                "مضاف",
                Tone.Info,
                TimelineEvidenceActionKind.Attachment,
                evidenceAttachment: attachment,
                evidenceKey: string.IsNullOrWhiteSpace(attachment.SavedFileName)
                    ? $"attachment-added:{attachment.Id.ToString(CultureInfo.InvariantCulture)}"
                    : $"attachment-added:{attachment.GuaranteeId.ToString(CultureInfo.InvariantCulture)}:{attachment.SavedFileName}");
        }

        public static TimelineItem StatusChanged(Guarantee version)
        {
            return new TimelineItem(
                version.CreatedAt,
                IsTerminalLifecycle(version.LifecycleStatus)
                    ? GetTerminalLifecycleTitle(version.LifecycleStatus)
                    : $"تغيير الحالة {version.LifecycleStatusLabel}",
                IsTerminalLifecycle(version.LifecycleStatus)
                    ? GetTerminalLifecycleDetail(version)
                    : $"أصبحت حالة الضمان: {version.LifecycleStatusLabel} ضمن الإصدار {version.VersionLabel}.",
                version.LifecycleStatusLabel,
                GetLifecycleTone(version.LifecycleStatus),
                TimelineEvidenceActionKind.OfficialAttachment,
                evidenceGuaranteeId: version.Id,
                evidenceKey: $"guarantee-status:{version.Id.ToString(CultureInfo.InvariantCulture)}:{version.LifecycleStatus}");
        }

        public static TimelineItem Created(string date)
            => new(date, "إنشاء الضمان", "تم إنشاء الضمان في النظام", "مكتمل", Tone.Success);

        public static TimelineItem VersionCreated(string date, string versionLabel, string status)
            => new(date, $"إنشاء الإصدار {versionLabel}", "تم حفظ هذا الإصدار ضمن سجل الضمان الرسمي", status, Tone.Info);

        private static Tone GetRequestTone(RequestStatus status) => status switch
        {
            RequestStatus.Executed => Tone.Success,
            RequestStatus.Pending => Tone.Warning,
            RequestStatus.Rejected or RequestStatus.Cancelled => Tone.Danger,
            _ => Tone.Info
        };

        private static Tone ParseTone(string? toneKey)
        {
            return Enum.TryParse(toneKey, ignoreCase: true, out Tone tone)
                ? tone
                : Tone.Info;
        }

        private static void ResolveEvidence(
            GuaranteeTimelineEvent timelineEvent,
            IReadOnlyDictionary<int, WorkflowRequest>? requestsById,
            IReadOnlyDictionary<int, AttachmentRecord>? attachmentsById,
            IReadOnlyDictionary<string, AttachmentRecord>? attachmentsByEventKey,
            out TimelineEvidenceActionKind evidenceActionKind,
            out AttachmentRecord? evidenceAttachment,
            out WorkflowRequest? evidenceRequest)
        {
            evidenceActionKind = TimelineEvidenceActionKind.None;
            evidenceAttachment = null;
            evidenceRequest = null;

            if (!string.IsNullOrWhiteSpace(timelineEvent.EventKey)
                && attachmentsByEventKey?.TryGetValue(timelineEvent.EventKey, out evidenceAttachment) == true)
            {
                evidenceActionKind = TimelineEvidenceActionKind.Attachment;
                return;
            }

            if (timelineEvent.AttachmentId.HasValue
                && attachmentsById?.TryGetValue(timelineEvent.AttachmentId.Value, out evidenceAttachment) == true)
            {
                evidenceActionKind = TimelineEvidenceActionKind.Attachment;
                return;
            }

            if (!timelineEvent.WorkflowRequestId.HasValue
                || requestsById?.TryGetValue(timelineEvent.WorkflowRequestId.Value, out evidenceRequest) != true)
            {
                evidenceRequest = null;
                evidenceActionKind = CanAttachOfficialEvidenceToEvent(timelineEvent.EventType)
                    ? TimelineEvidenceActionKind.OfficialAttachment
                    : TimelineEvidenceActionKind.None;
                return;
            }

            evidenceActionKind = timelineEvent.EventType switch
            {
                "WorkflowRequestCreated" when evidenceRequest?.HasLetter == true => TimelineEvidenceActionKind.RequestLetter,
                "WorkflowRequestCreated" => TimelineEvidenceActionKind.OfficialAttachment,
                "WorkflowResponseRecorded" => TimelineEvidenceActionKind.ResponseDocument,
                "WorkflowResponseDocumentAttached" => TimelineEvidenceActionKind.ResponseDocument,
                _ when CanAttachOfficialEvidenceToEvent(timelineEvent.EventType) => TimelineEvidenceActionKind.OfficialAttachment,
                _ => TimelineEvidenceActionKind.None
            };
        }

        private static TimelineEvidenceActionKind NormalizeEvidenceActionKind(
            TimelineEvidenceActionKind evidenceActionKind,
            AttachmentRecord? evidenceAttachment,
            WorkflowRequest? evidenceRequest,
            int? evidenceGuaranteeId)
        {
            return evidenceActionKind switch
            {
                TimelineEvidenceActionKind.Attachment when evidenceAttachment != null =>
                    TimelineEvidenceActionKind.Attachment,
                TimelineEvidenceActionKind.RequestLetter when evidenceRequest?.HasLetter == true =>
                    TimelineEvidenceActionKind.RequestLetter,
                TimelineEvidenceActionKind.ResponseDocument
                    when evidenceRequest != null
                         && (evidenceRequest.HasResponseDocument || evidenceRequest.Status != RequestStatus.Pending) =>
                    TimelineEvidenceActionKind.ResponseDocument,
                TimelineEvidenceActionKind.OfficialAttachment =>
                    TimelineEvidenceActionKind.OfficialAttachment,
                _ => TimelineEvidenceActionKind.None
            };
        }

        private static bool CanAttachOfficialEvidenceToEvent(string eventType)
        {
            return !string.Equals(eventType, "AttachmentAdded", StringComparison.Ordinal)
                   && !string.Equals(eventType, "WorkflowResponseDocumentAttached", StringComparison.Ordinal);
        }

        private static string BuildEvidenceActionLabel(
            TimelineEvidenceActionKind evidenceActionKind,
            AttachmentRecord? evidenceAttachment,
            WorkflowRequest? evidenceRequest)
        {
            return evidenceActionKind switch
            {
                TimelineEvidenceActionKind.Attachment => "فتح المرفق",
                TimelineEvidenceActionKind.RequestLetter => "فتح خطاب الطلب",
                TimelineEvidenceActionKind.ResponseDocument when evidenceRequest?.HasResponseDocument == true => "فتح رد البنك",
                TimelineEvidenceActionKind.ResponseDocument => "إرفاق",
                TimelineEvidenceActionKind.OfficialAttachment => "إرفاق",
                _ => string.Empty
            };
        }

        private static string BuildEvidenceActionHint(
            TimelineEvidenceActionKind evidenceActionKind,
            AttachmentRecord? evidenceAttachment,
            WorkflowRequest? evidenceRequest)
        {
            return evidenceActionKind switch
            {
                TimelineEvidenceActionKind.Attachment =>
                    $"فتح {evidenceAttachment?.DocumentTypeLabel ?? "المرفق"} المرتبط بهذا الحدث.",
                TimelineEvidenceActionKind.RequestLetter =>
                    "فتح خطاب الطلب المرتبط بهذا الحدث.",
                TimelineEvidenceActionKind.ResponseDocument when evidenceRequest?.HasResponseDocument == true =>
                    "فتح مستند رد البنك المرتبط بهذا الحدث.",
                TimelineEvidenceActionKind.ResponseDocument =>
                    "إرفاق مستند رد البنك بهذا الحدث المغلق.",
                TimelineEvidenceActionKind.OfficialAttachment =>
                    "إرفاق مستند رسمي بهذا الحدث.",
                _ => string.Empty
            };
        }

        private static string BuildEvidenceActionAutomationId(
            TimelineEvidenceActionKind evidenceActionKind,
            AttachmentRecord? evidenceAttachment,
            WorkflowRequest? evidenceRequest,
            int? evidenceGuaranteeId,
            string evidenceKey)
        {
            if (evidenceActionKind == TimelineEvidenceActionKind.None)
            {
                return string.Empty;
            }

            string key = string.IsNullOrWhiteSpace(evidenceKey)
                ? evidenceActionKind switch
                {
                    TimelineEvidenceActionKind.Attachment =>
                        $"attachment:{evidenceAttachment?.Id.ToString(CultureInfo.InvariantCulture) ?? "0"}",
                    TimelineEvidenceActionKind.RequestLetter =>
                        $"request-letter:{evidenceRequest?.Id.ToString(CultureInfo.InvariantCulture) ?? "0"}",
                    TimelineEvidenceActionKind.ResponseDocument =>
                        $"response-document:{evidenceRequest?.Id.ToString(CultureInfo.InvariantCulture) ?? "0"}",
                    TimelineEvidenceActionKind.OfficialAttachment =>
                        $"official-attachment:{evidenceGuaranteeId?.ToString(CultureInfo.InvariantCulture) ?? "0"}",
                    _ => "none"
                }
                : evidenceKey;
            string normalized = new string(key
                .Where(character => char.IsAsciiLetterOrDigit(character) || character == '-' || character == ':')
                .ToArray())
                .Replace(':', '.')
                .Replace('-', '.');

            return string.IsNullOrWhiteSpace(normalized)
                ? "GuaranteeTimeline.Evidence"
                : $"GuaranteeTimeline.Evidence.{normalized}";
        }

        private static Tone GetLifecycleTone(GuaranteeLifecycleStatus status) => status switch
        {
            GuaranteeLifecycleStatus.Active => Tone.Success,
            GuaranteeLifecycleStatus.Expired or GuaranteeLifecycleStatus.Liquidated => Tone.Danger,
            GuaranteeLifecycleStatus.Released or GuaranteeLifecycleStatus.Replaced => Tone.Info,
            _ => Tone.Info
        };

        private static string BuildBankResponseEffectDetail(WorkflowRequest request, string resultVersionLabel)
        {
            if (request.Status != RequestStatus.Executed)
            {
                return string.Empty;
            }

            return request.Type switch
            {
                RequestType.Extension when !string.IsNullOrWhiteSpace(resultVersionLabel) =>
                    $"الإصدار الناتج: {resultVersionLabel}",
                RequestType.Reduction when !string.IsNullOrWhiteSpace(resultVersionLabel) =>
                    $"الإصدار الناتج: {resultVersionLabel}",
                RequestType.Verification when !string.IsNullOrWhiteSpace(resultVersionLabel) =>
                    $"اعتماد مستند رسمي على {resultVersionLabel}",
                RequestType.Release =>
                    "تم إنهاء دورة حياة الضمان بالإفراج",
                RequestType.Liquidation =>
                    "تم إنهاء دورة حياة الضمان بالتسييل",
                RequestType.Replacement =>
                    string.IsNullOrWhiteSpace(request.ReplacementGuaranteeNo)
                        ? "تم إنشاء ضمان بديل"
                        : $"الضمان البديل: {request.ReplacementGuaranteeNo}",
                RequestType.Annulment =>
                    "مسار قديم ملغى",
                _ => string.Empty
            };
        }

        private static bool IsTerminalLifecycle(GuaranteeLifecycleStatus status)
            => status is GuaranteeLifecycleStatus.Released
                or GuaranteeLifecycleStatus.Liquidated
                or GuaranteeLifecycleStatus.Replaced;

        private static string GetTerminalLifecycleTitle(GuaranteeLifecycleStatus status) => status switch
        {
            GuaranteeLifecycleStatus.Released => "إنهاء دورة الحياة بالإفراج",
            GuaranteeLifecycleStatus.Liquidated => "إنهاء دورة الحياة بالتسييل",
            GuaranteeLifecycleStatus.Replaced => "استبدال الضمان",
            _ => "تغيير الحالة"
        };

        private static string GetTerminalLifecycleDetail(Guarantee version) => version.LifecycleStatus switch
        {
            GuaranteeLifecycleStatus.Released =>
                $"تم تسجيل الإفراج عن الضمان بقيمة {version.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال.",
            GuaranteeLifecycleStatus.Liquidated =>
                $"تم تسجيل تسييل الضمان بقيمة {version.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال.",
            GuaranteeLifecycleStatus.Replaced =>
                "تم إنهاء السلسلة القديمة لصالح ضمان بديل.",
            _ =>
                $"أصبحت حالة الضمان: {version.LifecycleStatusLabel} ضمن الإصدار {version.VersionLabel}."
        };

    }

    internal static class WorkflowRequestDisplayText
    {
        public static string BuildDetail(WorkflowRequest request)
        {
            string actor = string.IsNullOrWhiteSpace(request.CreatedBy) ? "النظام" : request.CreatedBy;
            if (request.Status == RequestStatus.Pending)
            {
                return request.Type == RequestType.Annulment
                    ? $"طلب قديم ملغى من قبل {actor}"
                    : $"تم رفع الطلب من قبل {actor}";
            }

            string responseNotes = request.ResponseNotes?.Trim() ?? string.Empty;
            if (request.Type == RequestType.Annulment)
            {
                return string.IsNullOrWhiteSpace(responseNotes)
                    ? "مسار قديم ملغى ولا يُستخدم في العمل الحالي."
                    : responseNotes;
            }

            return string.IsNullOrWhiteSpace(responseNotes)
                ? "تم تحديث حالة الطلب"
                : responseNotes;
        }

    }

    public sealed class GuaranteeRequestPreviewItem
    {
        public GuaranteeRequestPreviewItem(
            WorkflowRequest request,
            string requestNo,
            string requestType,
            string date,
            string detail,
            string status,
            string requestedValue,
            Tone tone,
            bool isContextTarget)
        {
            Request = request;
            RequestNo = requestNo;
            RequestType = requestType;
            Date = date;
            Detail = detail;
            Status = status;
            RequestedValue = requestedValue;
            Brush = TonePalette.Foreground(tone);
            StatusBackground = TonePalette.Background(tone);
            StatusBorder = TonePalette.Border(tone);
            IsContextTarget = isContextTarget;
        }

        public WorkflowRequest Request { get; }
        public string RequestNo { get; }
        public string RequestType { get; }
        public string RequestHeading => IsContextTarget ? $"{RequestType} • الطلب المفتوح الآن" : RequestType;
        public string Date { get; }
        public string Detail { get; }
        public string Status { get; }
        public string RequestedValue { get; }
        public Brush Brush { get; }
        public Brush StatusBackground { get; }
        public Brush StatusBorder { get; }
        public bool IsContextTarget { get; }
        public string ContextLabel => IsContextTarget ? "الطلب المفتوح الآن" : string.Empty;
        public string ContextAutomationStatus => IsContextTarget ? "هذا هو الطلب الذي تم فتح شاشة الطلبات منه." : string.Empty;
        public bool CanRegisterResponse => Request.Status == RequestStatus.Pending;
        public bool CanOpenLetter => Request.HasLetter;
        public bool CanOpenResponse => Request.HasResponseDocument;

        public static GuaranteeRequestPreviewItem FromRequest(WorkflowRequest request, bool isContextTarget = false)
        {
            Tone tone = request.Status switch
            {
                RequestStatus.Executed => Tone.Success,
                RequestStatus.Pending => Tone.Warning,
                RequestStatus.Rejected or RequestStatus.Cancelled => Tone.Danger,
                _ => Tone.Info
            };

            string detail = request.Status == RequestStatus.Pending || !string.IsNullOrWhiteSpace(request.ResponseNotes)
                ? WorkflowRequestDisplayText.BuildDetail(request)
                : $"آخر تحديث بواسطة {(string.IsNullOrWhiteSpace(request.CreatedBy) ? "النظام" : request.CreatedBy)}";

            return new GuaranteeRequestPreviewItem(
                request,
                $"REQ-{request.Id:0000}",
                request.TypeLabel,
                request.RequestDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                detail,
                request.StatusLabel,
                request.RequestedValueLabel,
                tone,
                isContextTarget);
        }
    }

    public sealed class GuaranteeOutputPreviewItem
    {
        public GuaranteeOutputPreviewItem(WorkflowRequest request, string requestNo, string title, string date, string detail, string status, Tone tone)
        {
            Request = request;
            RequestNo = requestNo;
            Title = title;
            Date = date;
            Detail = detail;
            Status = status;
            Brush = TonePalette.Foreground(tone);
            StatusBackground = TonePalette.Background(tone);
            StatusBorder = TonePalette.Border(tone);
        }

        public WorkflowRequest Request { get; }
        public string RequestNo { get; }
        public string Title { get; }
        public string Date { get; }
        public string Detail { get; }
        public string Status { get; }
        public Brush Brush { get; }
        public Brush StatusBackground { get; }
        public Brush StatusBorder { get; }
        public bool CanOpenLetter => Request.HasLetter;
        public bool CanOpenResponse => Request.HasResponseDocument;

        public static GuaranteeOutputPreviewItem FromRequest(WorkflowRequest request)
        {
            Tone tone = request.Status switch
            {
                RequestStatus.Executed => Tone.Success,
                RequestStatus.Pending => Tone.Warning,
                RequestStatus.Rejected or RequestStatus.Cancelled => Tone.Danger,
                _ => Tone.Info
            };

            string detail = request.HasLetter && request.HasResponseDocument
                ? "يتوفر خطاب الطلب ورد البنك لهذا الطلب."
                : request.HasLetter
                    ? "يتوفر خطاب الطلب لهذا الطلب."
                    : "يتوفر رد البنك لهذا الطلب.";

            return new GuaranteeOutputPreviewItem(
                request,
                $"REQ-{request.Id:0000}",
                request.TypeLabel,
                request.RequestDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                detail,
                request.StatusLabel,
                tone);
        }
    }

    public sealed class FilterOption
    {
        public static readonly FilterOption AllTimeStatuses = new("كل الحالات", null);

        public FilterOption(string label, GuaranteeTimeStatus? value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }
        public GuaranteeTimeStatus? Value { get; }

        public override string ToString() => Label;
    }

    public sealed class OperationalInquiryOption
    {
        public OperationalInquiryOption(string id, string section, string label, string description)
        {
            Id = id;
            Section = section;
            Label = label;
            Description = description;
        }

        public string Id { get; }
        public string Section { get; }
        public string Label { get; }
        public string Description { get; }
        public string Display => $"{Section} | {Label}";

        public override string ToString() => Display;
    }
}
