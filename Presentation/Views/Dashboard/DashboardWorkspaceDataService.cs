using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager
{
    public static class DashboardScopeFilters
    {
        public const string AllWork = "أعمال اليوم";
        public const string PendingRequests = "طلبات بانتظار الرد";
        public const string ExpiryFollowUps = "متابعات الانتهاء";
        public const string LegacyPendingRequests = "طلبات معلقة";
        public const string LegacyExpiredFollowUp = "منتهية تحتاج متابعة";
        public const string LegacyExpiringSoon = "قريبة الانتهاء";

        public static string Normalize(string? rawScope)
        {
            string scope = rawScope?.Trim() ?? string.Empty;
            return scope switch
            {
                LegacyExpiredFollowUp => ExpiryFollowUps,
                LegacyExpiringSoon => ExpiryFollowUps,
                LegacyPendingRequests => PendingRequests,
                PendingRequests => PendingRequests,
                ExpiryFollowUps => ExpiryFollowUps,
                _ => AllWork
            };
        }
    }

    public static class DashboardExpiryFollowUpFilters
    {
        public const string All = "كل المتابعات";
        public const string Expired = "منتهية";
        public const string ExpiringSoon = "قريبة الانتهاء";

        public static string Normalize(string? rawFilter)
        {
            string filter = rawFilter?.Trim() ?? string.Empty;
            return filter switch
            {
                Expired or DashboardScopeFilters.LegacyExpiredFollowUp => Expired,
                ExpiringSoon or DashboardScopeFilters.LegacyExpiringSoon => ExpiringSoon,
                _ => All
            };
        }

        public static string FromScope(string? rawScope)
        {
            string scope = rawScope?.Trim() ?? string.Empty;
            return scope switch
            {
                DashboardScopeFilters.LegacyExpiredFollowUp => Expired,
                DashboardScopeFilters.LegacyExpiringSoon => ExpiringSoon,
                _ => All
            };
        }
    }

    public sealed class DashboardWorkspaceDataService
    {
        private const string ExpiredFollowUpMetricLabel = "منتهيه تحتاج اغلاق";

        public List<DashboardWorkItem> BuildItems(
            IReadOnlyList<Guarantee> guarantees,
            IReadOnlyList<WorkflowRequestListItem> pendingRequests)
        {
            var items = new List<DashboardWorkItem>();
            Dictionary<int, WorkflowRequestListItem> latestPendingByRoot = pendingRequests
                .Where(item => item.Request.Status == RequestStatus.Pending)
                .GroupBy(item => item.RootGuaranteeId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(item => item.Request.RequestDate)
                        .ThenByDescending(item => item.Request.SequenceNumber)
                        .First());

            items.AddRange(pendingRequests.Select(BuildPendingRequestItem));
            items.AddRange(guarantees
                .Where(item => item.NeedsExpiryFollowUp)
                .OrderBy(item => item.ExpiryDate)
                .Select(item => BuildExpiredFollowUpItem(item, ResolvePending(latestPendingByRoot, item))));
            items.AddRange(guarantees
                .Where(item => item.IsExpiringSoon && HasPendingRequest(latestPendingByRoot, item))
                .OrderBy(item => item.ExpiryDate)
                .Select(item => BuildExpiringSoonItem(item, ResolvePending(latestPendingByRoot, item))));

            return items;
        }

        public DashboardWorkspaceFilterResult BuildFilteredItems(
            IReadOnlyList<DashboardWorkItem> allItems,
            string searchText,
            string scopeFilter,
            bool hasLastFile,
            string lastFileGuaranteeNo,
            IReadOnlyList<Guarantee> guarantees,
            IReadOnlyList<WorkflowRequestListItem> pendingRequests,
            string expiryFollowUpFilter = DashboardExpiryFollowUpFilters.All)
        {
            IEnumerable<DashboardWorkItem> query = allItems;
            string normalizedScope = DashboardScopeFilters.Normalize(scopeFilter);
            string normalizedExpiryFilter = DashboardExpiryFollowUpFilters.Normalize(expiryFollowUpFilter);
            query = normalizedScope switch
            {
                DashboardScopeFilters.PendingRequests => query.Where(item => item.Scope == DashboardScope.PendingRequests),
                DashboardScopeFilters.ExpiryFollowUps => ApplyExpiryFollowUpFilter(query, normalizedExpiryFilter),
                _ => query
            };

            string normalizedSearch = searchText.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(item =>
                    item.RequiredLabel.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    item.Supplier.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    item.Title.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    item.Subtitle.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    item.Reference.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    item.Bank.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
            }

            List<DashboardWorkItem> filtered = query
                .OrderBy(item => item.PriorityRank)
                .ThenBy(item => item.SortDate)
                .ThenByDescending(item => item.Amount)
                .ToList();

            return new DashboardWorkspaceFilterResult(
                filtered,
                BuildMetrics(
                    normalizedScope,
                    filtered,
                    hasLastFile,
                    lastFileGuaranteeNo,
                    allItems,
                    guarantees,
                    pendingRequests));
        }

        private static IEnumerable<DashboardWorkItem> ApplyExpiryFollowUpFilter(
            IEnumerable<DashboardWorkItem> query,
            string normalizedExpiryFilter)
        {
            return normalizedExpiryFilter switch
            {
                DashboardExpiryFollowUpFilters.Expired => query.Where(item => item.Scope == DashboardScope.ExpiredFollowUp),
                DashboardExpiryFollowUpFilters.ExpiringSoon => query.Where(item => item.Scope == DashboardScope.ExpiringSoon),
                _ => query.Where(item =>
                    item.Scope == DashboardScope.ExpiredFollowUp ||
                    item.Scope == DashboardScope.ExpiringSoon)
            };
        }

        public DashboardWorkspaceDetailState BuildDetailState(
            DashboardWorkItem? selected,
            string scopeFilter,
            bool hasLastFile,
            string lastFileGuaranteeNo,
            string lastFileSummary)
        {
            string normalizedScope = DashboardScopeFilters.Normalize(scopeFilter);

            if (selected == null)
            {
                string title = "لا توجد أولوية محددة";
                string note = hasLastFile
                    ? $"آخر ضمان تم العمل عليه: {lastFileGuaranteeNo} | {lastFileSummary}"
                    : "لا يوجد آخر ضمان محفوظ بعد. اختر عنصرًا من أعمال اليوم أو افتح الضمانات من الشريط الجانبي.";

                if (string.Equals(normalizedScope, DashboardScopeFilters.ExpiryFollowUps, StringComparison.Ordinal))
                {
                    title = "اختر متابعة انتهاء";
                    note = "هذا النطاق يجمع المتابعة الوقائية والمتابعة المتأخرة في بيت يومي واحد.";
                }
                else if (string.Equals(normalizedScope, DashboardScopeFilters.PendingRequests, StringComparison.Ordinal))
                {
                    title = "اختر طلبًا بانتظار الرد";
                    note = "ابدأ بطلب ينتظر رد البنك لترى رقم الضمان والقيمة المطلوبة وما الإجراء الأنسب الآن.";
                }

                return new DashboardWorkspaceDetailState(
                    title,
                    "جاهز",
                    WorkspaceSurfaceChrome.BrushFrom("#16A34A"),
                    WorkspaceSurfaceChrome.BrushFrom("#F2FBF4"),
                    WorkspaceSurfaceChrome.BrushFrom("#C9EFCF"),
                    null,
                    "---",
                    "---",
                    "لا توجد قيمة مرتبطة",
                    "---",
                    "---",
                    "---",
                    "---",
                    note,
                    "اختر عنصرًا لفتح وجهته",
                    ResolveEmptyDetailProfile(normalizedScope),
                    false);
            }

            DashboardDetailProfile detailProfile = selected.Scope switch
            {
                DashboardScope.PendingRequests => DashboardDetailProfile.PendingRequest,
                DashboardScope.ExpiredFollowUp or DashboardScope.ExpiringSoon => DashboardDetailProfile.FollowUp,
                _ => DashboardDetailProfile.Default
            };

            return new DashboardWorkspaceDetailState(
                selected.Title,
                selected.PriorityLabel,
                selected.PriorityBrush,
                selected.PriorityBackground,
                selected.PriorityBorder,
                selected.BankLogo,
                selected.Bank,
                ArabicAmountFormatter.FormatNumber(selected.Amount),
                selected.AmountCaption,
                selected.Reference,
                selected.DueDetail,
                detailProfile == DashboardDetailProfile.FollowUp ? selected.DueLabel : "---",
                selected.NextAction,
                selected.Note,
                selected.PrimaryActionLabel,
                detailProfile,
                true);
        }

        public DashboardGuidanceState BuildGuidanceState(
            IReadOnlyList<DashboardWorkItem> allItems,
            IReadOnlyList<Guarantee> guarantees,
            IReadOnlyList<WorkflowRequestListItem> pendingRequests)
        {
            DashboardWorkItem? topPriority = allItems
                .OrderBy(item => item.PriorityRank)
                .ThenBy(item => item.SortDate)
                .ThenByDescending(item => item.Amount)
                .FirstOrDefault();

            DashboardGuidanceCard guide = topPriority == null
                ? new DashboardGuidanceCard(
                    "دليل اليوم الذكي",
                    "لا توجد أعمال يومية معلقة الآن.",
                    "المحفظة هادئة، ويمكنك فتح الضمانات لمراجعة السجل الكامل أو إضافة ضمان جديد.",
                    "فتح الضمانات",
                    DashboardGuidanceActionKind.OpenGuarantees,
                    null)
                : new DashboardGuidanceCard(
                    "دليل اليوم الذكي",
                    $"ابدأ من {topPriority.Reference} لأنه أعلى أولوية ظاهرة اليوم.",
                    $"{topPriority.CategoryLabel} | {topPriority.NextAction}",
                    topPriority.PrimaryActionLabel,
                    DashboardGuidanceActionKind.OpenTopPriority,
                    topPriority);

            int pendingCount = pendingRequests.Count;
            int expiredCount = allItems.Count(item => item.Scope == DashboardScope.ExpiredFollowUp);
            int expiringSoonCount = allItems.Count(item => item.Scope == DashboardScope.ExpiringSoon);
            HashSet<int> pendingRootIds = BuildPendingRootIds(pendingRequests);
            int followUpCount = guarantees.Count(item =>
                item.NeedsExpiryFollowUp ||
                (item.IsExpiringSoon && pendingRootIds.Contains(GetRootId(item))));

            DashboardGuidanceCard recommendation;
            if (pendingCount > 0)
            {
                recommendation = new DashboardGuidanceCard(
                    "توصيات تشغيلية",
                    $"يوجد {pendingCount.ToString("N0", CultureInfo.InvariantCulture)} طلب بانتظار رد البنك.",
                    "ابدأ بها لأنها تمثل مسارات مفتوحة ولا ينبغي إنشاء إجراء جديد قبل فهم الطلب القائم.",
                    "عرض الطلبات المنتظرة",
                    DashboardGuidanceActionKind.FilterPendingRequests,
                    null);
            }
            else if (expiredCount > 0)
            {
                recommendation = new DashboardGuidanceCard(
                    "توصيات تشغيلية",
                    $"يوجد {expiredCount.ToString("N0", CultureInfo.InvariantCulture)} ضمان منتهي يحتاج إغلاقًا.",
                    "المنتهية لا تقبل تمديدًا أو تسييلًا؛ الإجراء المتاح هو الإفراج/إعادة الضمان وتوثيق الرد.",
                    "عرض التي تحتاج إغلاق",
                    DashboardGuidanceActionKind.FilterExpiredFollowUps,
                    null);
            }
            else if (expiringSoonCount > 0)
            {
                recommendation = new DashboardGuidanceCard(
                    "توصيات تشغيلية",
                    $"يوجد {expiringSoonCount.ToString("N0", CultureInfo.InvariantCulture)} ضمان قريب الانتهاء.",
                    "راجعها قبل تاريخ الانتهاء لتحديد هل المطلوب تمديد أو إجراء آخر.",
                    "عرض القريبة من الانتهاء",
                    DashboardGuidanceActionKind.FilterExpiringSoon,
                    null);
            }
            else
            {
                recommendation = new DashboardGuidanceCard(
                    "توصيات تشغيلية",
                    "لا توجد توصيات حرجة الآن.",
                    $"إجمالي عناصر المتابعة الحالية {followUpCount.ToString("N0", CultureInfo.InvariantCulture)}، ويمكنك العودة لأعمال اليوم عند ظهور مستجدات.",
                    "عرض كل أعمال اليوم",
                    DashboardGuidanceActionKind.FilterAllWork,
                    null);
            }

            return new DashboardGuidanceState(guide, recommendation);
        }

        private static DashboardDetailProfile ResolveEmptyDetailProfile(string normalizedScope)
        {
            return normalizedScope switch
            {
                DashboardScopeFilters.PendingRequests => DashboardDetailProfile.PendingRequest,
                DashboardScopeFilters.ExpiryFollowUps => DashboardDetailProfile.FollowUp,
                _ => DashboardDetailProfile.Default
            };
        }

        private static DashboardWorkItem BuildPendingRequestItem(WorkflowRequestListItem item)
        {
            int ageDays = Math.Max(0, (DateTime.Today - item.Request.RequestDate.Date).Days);
            (string label, Tone tone, int rank) = ageDays switch
            {
                >= 21 => ("حرج", Tone.Danger, 0),
                >= 10 => ("مرتفع", Tone.Warning, 1),
                _ => ("متابعة", Tone.Info, 2)
            };

            return new DashboardWorkItem(
                DashboardScope.PendingRequests,
                item.RootGuaranteeId,
                item.Request.Id,
                GuaranteeFocusArea.Requests,
                "بانتظار الرد",
                label,
                rank,
                item.Supplier,
                item.Request.TypeLabel,
                item.Supplier,
                item.Bank,
                GuaranteeRow.ResolveBankLogo(item.Bank),
                item.GuaranteeNo,
                item.CurrentAmount,
                ArabicAmountFormatter.FormatSaudiRiyals(item.CurrentAmount),
                ArabicAmountFormatter.FormatSaudiRiyalsInWords(item.CurrentAmount),
                item.Request.RequestDate.Date,
                item.Request.RequestDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                $"ينتظر منذ {ageDays.ToString("N0", CultureInfo.InvariantCulture)} يوم",
                $"{item.Request.TypeLabel} | {item.Supplier}",
                "راجع الطلب",
                $"مراجعة {item.Request.TypeLabel} ثم تسجيل رد البنك عند وصوله",
                $"ظهر اليوم لأنه ما زال بانتظار رد البنك. الحالي: {item.CurrentValueFieldLabel} = {item.CurrentValueDisplay}. المطلوب: {item.RequestedValueFieldLabel} = {item.RequestedValueDisplay}.",
                TonePalette.Foreground(tone),
                TonePalette.Background(tone),
                TonePalette.Border(tone),
                WorkspaceSurfaceChrome.BrushFrom("#2563EB"));
        }

        private static WorkflowRequestListItem? ResolvePending(
            IReadOnlyDictionary<int, WorkflowRequestListItem> latestPendingByRoot,
            Guarantee guarantee)
        {
            int rootId = GetRootId(guarantee);
            return latestPendingByRoot.TryGetValue(rootId, out WorkflowRequestListItem? pending)
                ? pending
                : null;
        }

        private static bool HasPendingRequest(
            IReadOnlyDictionary<int, WorkflowRequestListItem> latestPendingByRoot,
            Guarantee guarantee)
            => latestPendingByRoot.ContainsKey(GetRootId(guarantee));

        private static HashSet<int> BuildPendingRootIds(IReadOnlyList<WorkflowRequestListItem> pendingRequests)
            => pendingRequests
                .Where(item => item.Request.Status == RequestStatus.Pending)
                .Select(item => item.RootGuaranteeId)
                .ToHashSet();

        private static int GetRootId(Guarantee guarantee) => guarantee.RootId ?? guarantee.Id;

        private static DashboardWorkItem BuildExpiredFollowUpItem(Guarantee item, WorkflowRequestListItem? pendingRequest)
        {
            int daysLate = Math.Abs((item.ExpiryDate.Date - DateTime.Today).Days);
            (string label, Tone tone, int rank) = daysLate >= 30
                ? ("حرج", Tone.Danger, 0)
                : ("عاجل", Tone.Warning, 1);
            bool hasPendingRequest = pendingRequest != null;
            int? requestId = pendingRequest?.Request.Id;
            GuaranteeFocusArea focusArea = hasPendingRequest
                ? GuaranteeFocusArea.Requests
                : GuaranteeFocusArea.Actions;
            string primaryAction = hasPendingRequest ? "راجع الطلب" : "راجع الضمان";
            string nextAction = hasPendingRequest
                ? $"افتح {pendingRequest!.Request.TypeLabel} المعلق وسجل رد البنك عند وصوله"
                : "افتح الضمان في المحفظة وحدد هل يحتاج تمديدًا أو إفراجًا أو إقفالًا تشغيليًا";
            string note = hasPendingRequest
                ? $"ظهر اليوم لأن تاريخ الانتهاء مضى ويوجد {pendingRequest!.Request.TypeLabel} معلق منذ {pendingRequest.Request.RequestDate:yyyy/MM/dd}. ابدأ من الطلب المرتبط قبل إنشاء إجراء جديد."
                : $"ظهر اليوم لأن تاريخ الانتهاء مضى وما زالت الحالة التشغيلية {item.LifecycleStatusLabel}. المتابعة هنا تمنع بقاء ضمان منتهي بلا قرار.";

            return new DashboardWorkItem(
                DashboardScope.ExpiredFollowUp,
                item.RootId ?? item.Id,
                requestId,
                focusArea,
                "منتهية تحتاج إفراج",
                label,
                rank,
                item.Supplier,
                hasPendingRequest ? pendingRequest!.Request.TypeLabel : "متابعة انتهاء",
                item.Supplier,
                item.Bank,
                GuaranteeRow.ResolveBankLogo(item.Bank),
                item.GuaranteeNo,
                item.Amount,
                ArabicAmountFormatter.FormatSaudiRiyals(item.Amount),
                ArabicAmountFormatter.FormatSaudiRiyalsInWords(item.Amount),
                item.ExpiryDate.Date,
                item.ExpiryDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                $"متأخر {daysLate.ToString("N0", CultureInfo.InvariantCulture)} يوماً",
                item.GuaranteeType,
                primaryAction,
                nextAction,
                note,
                TonePalette.Foreground(tone),
                TonePalette.Background(tone),
                TonePalette.Border(tone),
                WorkspaceSurfaceChrome.BrushFrom("#EF4444"));
        }

        private static DashboardWorkItem BuildExpiringSoonItem(Guarantee item, WorkflowRequestListItem? pendingRequest)
        {
            int daysLeft = Math.Max(0, (item.ExpiryDate.Date - DateTime.Today).Days);
            (string label, Tone tone, int rank) = daysLeft switch
            {
                <= 3 => ("عاجل", Tone.Danger, 1),
                <= 10 => ("مرتفع", Tone.Warning, 2),
                _ => ("متابعة", Tone.Info, 3)
            };
            bool hasPendingRequest = pendingRequest != null;
            int? requestId = pendingRequest?.Request.Id;
            GuaranteeFocusArea focusArea = hasPendingRequest
                ? GuaranteeFocusArea.Requests
                : GuaranteeFocusArea.Actions;
            string primaryAction = hasPendingRequest ? "راجع الطلب" : "راجع التمديد";
            string nextAction = hasPendingRequest
                ? $"راجع {pendingRequest!.Request.TypeLabel} المعلق قبل إنشاء متابعة جديدة"
                : "افتح الضمان في المحفظة وراجع قرار التمديد قبل الوصول إلى تاريخ الانتهاء";
            string note = hasPendingRequest
                ? $"ظهر اليوم لأنه داخل نافذة الانتهاء القريبة ويوجد {pendingRequest!.Request.TypeLabel} معلق منذ {pendingRequest.Request.RequestDate:yyyy/MM/dd}. ابدأ من الطلب المرتبط."
                : "ظهر اليوم لأنه داخل نافذة الانتهاء القريبة. راجع الطلبات المرتبطة قبل إنشاء تمديد أو إقفال مبكر.";

            return new DashboardWorkItem(
                DashboardScope.ExpiringSoon,
                item.RootId ?? item.Id,
                requestId,
                focusArea,
                "قريبة الانتهاء",
                label,
                rank,
                item.Supplier,
                hasPendingRequest ? pendingRequest!.Request.TypeLabel : "مراجعة تمديد",
                item.Supplier,
                item.Bank,
                GuaranteeRow.ResolveBankLogo(item.Bank),
                item.GuaranteeNo,
                item.Amount,
                ArabicAmountFormatter.FormatSaudiRiyals(item.Amount),
                ArabicAmountFormatter.FormatSaudiRiyalsInWords(item.Amount),
                item.ExpiryDate.Date,
                item.ExpiryDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                $"خلال {daysLeft.ToString("N0", CultureInfo.InvariantCulture)} يوم",
                item.GuaranteeType,
                primaryAction,
                nextAction,
                note,
                TonePalette.Foreground(tone),
                TonePalette.Background(tone),
                TonePalette.Border(tone),
                WorkspaceSurfaceChrome.BrushFrom("#E09408"));
        }

        private static DashboardWorkspaceMetrics BuildMetrics(
            string normalizedScope,
            IReadOnlyList<DashboardWorkItem> filteredItems,
            bool hasLastFile,
            string lastFileGuaranteeNo,
            IReadOnlyList<DashboardWorkItem> allItems,
            IReadOnlyList<Guarantee> guarantees,
            IReadOnlyList<WorkflowRequestListItem> pendingRequests)
        {
            if (string.Equals(normalizedScope, DashboardScopeFilters.ExpiryFollowUps, StringComparison.Ordinal))
            {
                HashSet<int> pendingRootIds = BuildPendingRootIds(pendingRequests);
                int expiringCount = guarantees.Count(item => item.IsExpiringSoon && pendingRootIds.Contains(GetRootId(item)));
                int expiredCount = guarantees.Count(item => item.NeedsExpiryFollowUp);

                return new DashboardWorkspaceMetrics(new[]
                {
                    new DashboardMetricCard(
                        DashboardScopeFilters.AllWork,
                        allItems.Count.ToString("N0", CultureInfo.InvariantCulture),
                        "#2563EB",
                        DashboardScopeFilters.AllWork,
                        DashboardExpiryFollowUpFilters.All),
                    new DashboardMetricCard(
                        DashboardScopeFilters.PendingRequests,
                        pendingRequests.Count.ToString("N0", CultureInfo.InvariantCulture),
                        "#E09408",
                        DashboardScopeFilters.PendingRequests,
                        DashboardExpiryFollowUpFilters.All),
                    new DashboardMetricCard(
                        "قريبة الانتهاء",
                        expiringCount.ToString("N0", CultureInfo.InvariantCulture),
                        "#E09408",
                        DashboardScopeFilters.ExpiryFollowUps,
                        DashboardExpiryFollowUpFilters.ExpiringSoon),
                    new DashboardMetricCard(
                        ExpiredFollowUpMetricLabel,
                        expiredCount.ToString("N0", CultureInfo.InvariantCulture),
                        "#EF4444",
                        DashboardScopeFilters.ExpiryFollowUps,
                        DashboardExpiryFollowUpFilters.Expired)
                });
            }

            HashSet<int> defaultPendingRootIds = BuildPendingRootIds(pendingRequests);
            int expiringSoonCount = guarantees.Count(item => item.IsExpiringSoon && defaultPendingRootIds.Contains(GetRootId(item)));
            int expiredFollowUpCount = guarantees.Count(item => item.NeedsExpiryFollowUp);
            return new DashboardWorkspaceMetrics(new[]
            {
                new DashboardMetricCard(
                    DashboardScopeFilters.AllWork,
                    allItems.Count.ToString("N0", CultureInfo.InvariantCulture),
                    "#2563EB",
                    DashboardScopeFilters.AllWork,
                    DashboardExpiryFollowUpFilters.All),
                new DashboardMetricCard(
                    DashboardScopeFilters.PendingRequests,
                    pendingRequests.Count.ToString("N0", CultureInfo.InvariantCulture),
                    "#E09408",
                    DashboardScopeFilters.PendingRequests,
                    DashboardExpiryFollowUpFilters.All),
                new DashboardMetricCard(
                    "قريبة الانتهاء",
                    expiringSoonCount.ToString("N0", CultureInfo.InvariantCulture),
                    "#E09408",
                    DashboardScopeFilters.ExpiryFollowUps,
                    DashboardExpiryFollowUpFilters.ExpiringSoon),
                new DashboardMetricCard(
                    ExpiredFollowUpMetricLabel,
                    expiredFollowUpCount.ToString("N0", CultureInfo.InvariantCulture),
                    "#EF4444",
                    DashboardScopeFilters.ExpiryFollowUps,
                    DashboardExpiryFollowUpFilters.Expired)
            });
        }
    }

    public sealed record DashboardWorkspaceMetrics(
        IReadOnlyList<DashboardMetricCard> Cards);

    public sealed record DashboardMetricCard(
        string Label,
        string Value,
        string AccentHex,
        string ScopeFilter = "",
        string ExpiryFilter = "");

    public sealed record DashboardWorkspaceFilterResult(
        IReadOnlyList<DashboardWorkItem> Items,
        DashboardWorkspaceMetrics Metrics);

    public sealed record DashboardGuidanceState(
        DashboardGuidanceCard Guide,
        DashboardGuidanceCard Recommendation);

    public sealed record DashboardGuidanceCard(
        string Title,
        string PrimaryText,
        string SecondaryText,
        string ActionLabel,
        DashboardGuidanceActionKind ActionKind,
        DashboardWorkItem? TargetItem);

    public enum DashboardGuidanceActionKind
    {
        OpenTopPriority,
        FilterPendingRequests,
        FilterExpiredFollowUps,
        FilterExpiringSoon,
        FilterAllWork,
        OpenGuarantees
    }

    public sealed record DashboardWorkspaceDetailState(
        string Title,
        string BadgeText,
        Brush BadgeForeground,
        Brush BadgeBackground,
        Brush BadgeBorder,
        ImageSource? BankLogo,
        string BankText,
        string AmountHeadline,
        string AmountCaption,
        string Reference,
        string Due,
        string Expiry,
        string Action,
        string Note,
        string PrimaryActionButtonLabel,
        DashboardDetailProfile DetailProfile,
        bool CanRunPrimaryAction);

    public enum DashboardDetailProfile
    {
        Default,
        PendingRequest,
        FollowUp
    }

    public enum DashboardScope
    {
        PendingRequests,
        ExpiredFollowUp,
        ExpiringSoon
    }

    public sealed record DashboardWorkItem(
        DashboardScope Scope,
        int RootGuaranteeId,
        int? RequestId,
        GuaranteeFocusArea PrimaryFocusArea,
        string CategoryLabel,
        string PriorityLabel,
        int PriorityRank,
        string Title,
        string RequiredLabel,
        string Supplier,
        string Bank,
        ImageSource BankLogo,
        string Reference,
        decimal Amount,
        string AmountDisplay,
        string AmountCaption,
        DateTime SortDate,
        string DueLabel,
        string DueDetail,
        string Subtitle,
        string PrimaryActionLabel,
        string NextAction,
        string Note,
        Brush PriorityBrush,
        Brush PriorityBackground,
        Brush PriorityBorder,
        Brush CategoryBrush);
}
