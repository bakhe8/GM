using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager
{
    public sealed class GuaranteeRow : INotifyPropertyChanged
    {
        private static readonly Dictionary<string, ImageSource> BankLogoCache = new(StringComparer.OrdinalIgnoreCase);

        private GuaranteeRow(
            Guarantee guarantee,
            string supplier,
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
            Supplier = supplier;
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
        public string RowAutomationName => $"{GuaranteeNo} | {Supplier}";
        public string Supplier { get; }
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
        public ActionEligibility RegisterResponseAction => ActionProfile.RegisterResponseAction;
        public ActionEligibility OpenAttachmentsAction => Attachments.Count > 0
            ? ActionEligibility.Enabled($"يوجد {Attachments.Count.ToString("N0", CultureInfo.InvariantCulture)} مرفق رسمي متاح للفتح من هذه اللوحة.")
            : ActionProfile.OpenAttachmentsAction;
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
        public GuaranteeFocusArea SuggestedFocusArea => ActionProfile.SuggestedFocusArea;
        public bool HasSuggestedFocus => ActionProfile.SuggestedFocusArea != GuaranteeFocusArea.None;

        public event PropertyChangedEventHandler? PropertyChanged;

        public static ImageSource ResolveBankLogo(string bankName) => GetBankLogo(bankName);

        public void SetAttachments(IReadOnlyList<AttachmentRecord> attachments)
        {
            Attachments = attachments;
            OnPropertyChanged(nameof(Attachments));
            OnPropertyChanged(nameof(HasOfficialAttachments));
            OnPropertyChanged(nameof(OpenAttachmentsAction));
        }

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
            string supplier = string.IsNullOrWhiteSpace(guarantee.Supplier)
                ? "---"
                : guarantee.Supplier.Trim();
            string beneficiary = BusinessPartyDefaults.NormalizeBeneficiary(guarantee.Beneficiary);
            GuaranteeActionProfile actionProfile = GuaranteeActionProfile.Build(guarantee, relatedRequests);

            return new GuaranteeRow(
                guarantee,
                supplier,
                beneficiary,
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
            ActionEligibility registerResponseAction,
            ActionEligibility openAttachmentsAction,
            ActionEligibility editAction,
            ActionEligibility releaseAction,
            ActionEligibility extensionAction,
            ActionEligibility reductionAction,
            ActionEligibility liquidationAction,
            ActionEligibility verificationAction,
            ActionEligibility replacementAction,
            string summaryTitle,
            string summaryDetail,
            GuaranteeFocusArea suggestedFocusArea,
            string suggestedFocusLabel)
        {
            PendingRequestCount = pendingRequestCount;
            TotalRequestCount = totalRequestCount;
            OutputCount = outputCount;
            LetterOutputCount = letterOutputCount;
            ResponseOutputCount = responseOutputCount;
            RegisterResponseAction = registerResponseAction;
            OpenAttachmentsAction = openAttachmentsAction;
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
        public ActionEligibility RegisterResponseAction { get; }
        public ActionEligibility OpenAttachmentsAction { get; }
        public ActionEligibility EditAction { get; }
        public ActionEligibility ReleaseAction { get; }
        public ActionEligibility ExtensionAction { get; }
        public ActionEligibility ReductionAction { get; }
        public ActionEligibility LiquidationAction { get; }
        public ActionEligibility VerificationAction { get; }
        public ActionEligibility ReplacementAction { get; }
        public string SummaryTitle { get; }
        public string SummaryDetail { get; }
        public GuaranteeFocusArea SuggestedFocusArea { get; }
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

            bool isRequestBlocked(RequestType type) => archivedVersion || !WorkflowLifecyclePolicy.CanCreateRequest(guarantee, type);
            string blockedRequestHint(RequestType type) => archivedVersion
                ? archivedVersionHint
                : WorkflowLifecyclePolicy.GetCreateBlockedMessage(type, guarantee);

            ActionEligibility enableWhen(bool condition, string enabledHint, string disabledHint)
                => condition ? ActionEligibility.Enabled(enabledHint) : ActionEligibility.Disabled(disabledHint);

            ActionEligibility registerResponse = archivedVersion
                ? ActionEligibility.Disabled(archivedVersionHint)
                : enableWhen(
                    pendingCount > 0,
                    $"يوجد {pendingCount.ToString("N0", CultureInfo.InvariantCulture)} طلب معلق يمكن تسجيل رد البنك عليه من هذا الملف.",
                    "لا يوجد طلب معلق لهذا الضمان حاليًا، لذلك لا يتوفر تسجيل رد مباشر.");

            ActionEligibility openAttachments = enableWhen(
                guarantee.Attachments.Count > 0,
                $"يوجد {guarantee.Attachments.Count.ToString("N0", CultureInfo.InvariantCulture)} مرفق رسمي على هذا الضمان.",
                "لا توجد مرفقات رسمية على هذا الضمان حاليًا.");

            ActionEligibility edit = archivedVersion
                ? ActionEligibility.Disabled(archivedVersionHint)
                : lifecycleClosed
                ? ActionEligibility.Enabled($"السجل في حالة {guarantee.LifecycleStatusLabel}. التعديل هنا مخصص للتصحيح الإداري الوصفي فقط، مثل الاسم أو الملاحظات.")
                : ActionEligibility.Enabled("التعديل متاح للتصحيح الإداري الوصفي فقط، ولا يغير مبلغ الضمان أو تاريخه أو بنكه أو رقمه.");

            ActionEligibility release = BuildLifecycleAction(
                isRequestBlocked(RequestType.Release),
                hasPendingType(RequestType.Release),
                blockedRequestHint(RequestType.Release),
                "يوجد طلب إفراج معلق بالفعل لهذا الضمان.",
                guarantee.IsExpired || guarantee.LifecycleStatus == GuaranteeLifecycleStatus.Expired
                    ? "الضمان منتهي الصلاحية؛ المتاح هو الإفراج/إعادة الضمان للبنك وتوثيق الرد."
                    : "طلب الإفراج متاح عند اكتمال مستندات إنهاء الالتزام.");

            ActionEligibility extension = BuildLifecycleAction(
                isRequestBlocked(RequestType.Extension),
                hasPendingType(RequestType.Extension),
                blockedRequestHint(RequestType.Extension),
                "يوجد طلب تمديد معلق بالفعل لهذا الضمان.",
                guarantee.IsExpiringSoon
                        ? "الضمان قريب الانتهاء، وطلب التمديد مناسب في هذا التوقيت."
                        : "طلب التمديد متاح لتمديد استباقي عند الحاجة.");

            ActionEligibility reduction = BuildLifecycleAction(
                isRequestBlocked(RequestType.Reduction),
                hasPendingType(RequestType.Reduction),
                blockedRequestHint(RequestType.Reduction),
                "يوجد طلب تخفيض معلق بالفعل لهذا الضمان.",
                "طلب التخفيض متاح عند الحاجة لتعديل قيمة الضمان.");

            ActionEligibility liquidation = BuildLifecycleAction(
                isRequestBlocked(RequestType.Liquidation),
                hasPendingType(RequestType.Liquidation),
                blockedRequestHint(RequestType.Liquidation),
                "يوجد طلب تسييل معلق بالفعل لهذا الضمان.",
                "طلب التسييل متاح عند الحاجة لإثبات المطالبة وتنفيذ أثرها قبل انتهاء صلاحية الضمان.");

            ActionEligibility verification = BuildLifecycleAction(
                isRequestBlocked(RequestType.Verification),
                hasPendingType(RequestType.Verification),
                blockedRequestHint(RequestType.Verification),
                "يوجد طلب تحقق معلق بالفعل لهذا الضمان.",
                "طلب التحقق متاح لمراجعة صحة المستندات أو إثبات الحالة.");

            ActionEligibility replacement = BuildLifecycleAction(
                isRequestBlocked(RequestType.Replacement),
                hasPendingType(RequestType.Replacement),
                blockedRequestHint(RequestType.Replacement),
                "يوجد طلب استبدال معلق بالفعل لهذا الضمان.",
                "طلب الاستبدال متاح عند الحاجة لإصدار ضمان بديل.");

            string summaryTitle;
            string summaryDetail;
            GuaranteeFocusArea suggestedArea;
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
                    ? GuaranteeFocusArea.Attachments
                    : GuaranteeFocusArea.Series;
                suggestedLabel = guarantee.Attachments.Count > 0 ? "راجع المرفقات" : "راجع السجل";
            }
            else if (pendingCount > 0)
            {
                summaryTitle = $"يوجد {pendingCount.ToString("N0", CultureInfo.InvariantCulture)} طلب قيد التنفيذ على هذا الملف";
                summaryDetail = latestPending == null
                    ? "المتابعة الأقرب الآن هي مراجعة السجل الزمني وتسجيل رد البنك عند وصوله."
                    : $"أقرب نقطة متابعة الآن: {latestPending.TypeLabel} بتاريخ {latestPending.RequestDate:yyyy/MM/dd}.";
                suggestedArea = GuaranteeFocusArea.Requests;
                suggestedLabel = "راجع السجل";
            }
            else if (guarantee.NeedsExpiryFollowUp)
            {
                summaryTitle = "الضمان منتهٍ ويحتاج متابعة مباشرة";
                summaryDetail = "لا توجد طلبات معلقة حاليًا، لذا الأفضل البدء من السجل الزمني أو الاستعلامات لمعرفة سبب بقاء الضمان دون إغلاق.";
                suggestedArea = GuaranteeFocusArea.Requests;
                suggestedLabel = "راجع السجل";
            }
            else if (outputCount > 0)
            {
                summaryTitle = "توجد مخرجات جاهزة للفتح من داخل الملف";
                summaryDetail = $"المتوفر الآن: {letterCount.ToString("N0", CultureInfo.InvariantCulture)} خطاب طلب و{responseCount.ToString("N0", CultureInfo.InvariantCulture)} رد بنك مرتبط.";
                suggestedArea = GuaranteeFocusArea.Outputs;
                suggestedLabel = "افتح المخرجات";
            }
            else if (guarantee.IsExpiringSoon)
            {
                summaryTitle = "الضمان قريب الانتهاء";
                summaryDetail = "راجع التمديد أو المستندات الداعمة قبل الوصول إلى حالة منتهٍ تحتاج متابعة.";
                suggestedArea = GuaranteeFocusArea.ExecutiveSummary;
                suggestedLabel = "ارجع إلى الملخص";
            }
            else if (guarantee.Attachments.Count > 0)
            {
                summaryTitle = "المرفقات الرسمية متاحة داخل الملف";
                summaryDetail = "يمكنك البدء من المرفقات إذا كان المطلوب مراجعة المستند الرسمي قبل أي إجراء.";
                suggestedArea = GuaranteeFocusArea.Attachments;
                suggestedLabel = "افتح المرفقات";
            }
            else
            {
                summaryTitle = "الملف مستقر تشغيليًا في الوقت الحالي";
                summaryDetail = "لا توجد طلبات معلقة أو مخرجات ملحّة الآن، ويمكن استخدام الملف كنقطة مراجعة سريعة.";
                suggestedArea = GuaranteeFocusArea.ExecutiveSummary;
                suggestedLabel = "افتح الملخص";
            }

            return new GuaranteeActionProfile(
                pendingCount,
                requests.Count,
                outputCount,
                letterCount,
                responseCount,
                registerResponse,
                openAttachments,
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
}
