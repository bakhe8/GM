using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using GuaranteeManager.Models;

namespace GuaranteeManager
{
    public static class DashboardScopeFilters
    {
        public const string AllWork = "أعمال اليوم";
        public const string PendingRequests = "طلبات معلقة";
        public const string ExpiryFollowUps = "متابعات الانتهاء";
        public const string LegacyExpiredFollowUp = "منتهية تحتاج متابعة";
        public const string LegacyExpiringSoon = "قريبة الانتهاء";

        public static string Normalize(string? rawScope)
        {
            string scope = rawScope?.Trim() ?? string.Empty;
            return scope switch
            {
                LegacyExpiredFollowUp => ExpiryFollowUps,
                LegacyExpiringSoon => ExpiryFollowUps,
                PendingRequests => PendingRequests,
                ExpiryFollowUps => ExpiryFollowUps,
                _ => AllWork
            };
        }
    }

    public sealed class DashboardWorkspaceDataService
    {
        public List<DashboardWorkItem> BuildItems(
            IReadOnlyList<Guarantee> guarantees,
            IReadOnlyList<WorkflowRequestListItem> pendingRequests)
        {
            var items = new List<DashboardWorkItem>();

            items.AddRange(pendingRequests.Select(BuildPendingRequestItem));
            items.AddRange(guarantees
                .Where(item => item.NeedsExpiryFollowUp)
                .OrderBy(item => item.ExpiryDate)
                .Select(BuildExpiredFollowUpItem));
            items.AddRange(guarantees
                .Where(item => item.IsExpiringSoon)
                .OrderBy(item => item.ExpiryDate)
                .Select(BuildExpiringSoonItem));

            return items;
        }

        public DashboardWorkspaceFilterResult BuildFilteredItems(
            IReadOnlyList<DashboardWorkItem> allItems,
            string searchText,
            string scopeFilter,
            bool hasLastFile,
            string lastFileGuaranteeNo,
            IReadOnlyList<Guarantee> guarantees,
            IReadOnlyList<WorkflowRequestListItem> pendingRequests)
        {
            IEnumerable<DashboardWorkItem> query = allItems;
            string normalizedScope = DashboardScopeFilters.Normalize(scopeFilter);
            query = normalizedScope switch
            {
                DashboardScopeFilters.PendingRequests => query.Where(item => item.Scope == DashboardScope.PendingRequests),
                DashboardScopeFilters.ExpiryFollowUps => query.Where(item =>
                    item.Scope == DashboardScope.ExpiredFollowUp ||
                    item.Scope == DashboardScope.ExpiringSoon),
                _ => query
            };

            string normalizedSearch = searchText.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(item =>
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

            string summary = BuildSummary(filtered.Count, allItems.Count, normalizedSearch, normalizedScope);

            return new DashboardWorkspaceFilterResult(
                filtered,
                BuildMetrics(
                    normalizedScope,
                    filtered,
                    hasLastFile,
                    lastFileGuaranteeNo,
                    allItems,
                    guarantees,
                    pendingRequests),
                summary);
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
                string subtitle = "ابدأ باختيار عنصر من قائمة الأولويات اليومية.";
                string note = hasLastFile
                    ? $"آخر ملف تم العمل عليه: {lastFileGuaranteeNo} | {lastFileSummary}"
                    : "لا يوجد ملف حديث بعد. ابدأ بفتح الضمانات أو اختيار عنصر من أعمال اليوم.";

                if (string.Equals(normalizedScope, DashboardScopeFilters.ExpiryFollowUps, StringComparison.Ordinal))
                {
                    title = "اختر متابعة انتهاء";
                    subtitle = "ستظهر هنا تفاصيل الضمانات القريبة من الانتهاء أو المنتهية التي تحتاج متابعة.";
                    note = "هذه العدسة تجمع المتابعة الوقائية والمتابعة المتأخرة في بيت يومي واحد.";
                }
                else if (string.Equals(normalizedScope, DashboardScopeFilters.PendingRequests, StringComparison.Ordinal))
                {
                    title = "اختر طلبًا معلقًا";
                    subtitle = "ستظهر هنا تفاصيل الطلب المحدد والخطوة التالية المناسبة له.";
                    note = "ابدأ بطلب معلق لترى المرجع والقيمة المطلوبة وما الإجراء الأنسب الآن.";
                }

                return new DashboardWorkspaceDetailState(
                    title,
                    subtitle,
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
                    "---",
                    "---",
                    note,
                    "فتح الملف عند اختيار عنصر",
                    "فتح المساحة",
                    false,
                    false);
            }

            return new DashboardWorkspaceDetailState(
                selected.Title,
                selected.Subtitle,
                selected.PriorityLabel,
                selected.PriorityBrush,
                selected.PriorityBackground,
                selected.PriorityBorder,
                selected.BankLogo,
                selected.Bank,
                selected.AmountDisplay,
                selected.AmountCaption,
                selected.CategoryLabel,
                selected.PriorityLabel,
                selected.Reference,
                selected.DueDetail,
                selected.WorkspaceLabel,
                selected.NextAction,
                selected.Note,
                selected.PrimaryActionLabel,
                selected.WorkspaceButtonLabel,
                selected.RootGuaranteeId > 0,
                true);
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
                DashboardTarget.Requests,
                item.RootGuaranteeId,
                item.Request.Id,
                GuaranteeFileFocusArea.Requests,
                "طلبات معلقة",
                label,
                rank,
                $"{item.Request.TypeLabel} - {item.Supplier}",
                item.Bank,
                GuaranteeRow.ResolveBankLogo(item.Bank),
                item.GuaranteeNo,
                item.CurrentAmount,
                $"{item.CurrentAmount.ToString("N0", CultureInfo.InvariantCulture)} ريال",
                "القيمة الحالية للضمان",
                item.Request.RequestDate.Date,
                item.Request.RequestDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                $"أُنشئ بتاريخ {item.Request.RequestDate:yyyy/MM/dd}",
                $"{item.Request.TypeLabel} | {item.Supplier}",
                "متابعة الطلب الآن",
                "الطلبات",
                "فتح الطلبات",
                $"مراجعة {item.Request.TypeLabel} وتسجيل رد البنك",
                $"الطلب ما زال معلقاً. الحقل الحالي: {item.CurrentValueFieldLabel} = {item.CurrentValueDisplay}. المطلوب: {item.RequestedValueFieldLabel} = {item.RequestedValueDisplay}.",
                TonePalette.Foreground(tone),
                TonePalette.Background(tone),
                TonePalette.Border(tone),
                WorkspaceSurfaceChrome.BrushFrom("#2563EB"));
        }

        private static DashboardWorkItem BuildExpiredFollowUpItem(Guarantee item)
        {
            int daysLate = Math.Abs((item.ExpiryDate.Date - DateTime.Today).Days);
            (string label, Tone tone, int rank) = daysLate >= 30
                ? ("حرج", Tone.Danger, 0)
                : ("عاجل", Tone.Warning, 1);

            return new DashboardWorkItem(
                DashboardScope.ExpiredFollowUp,
                DashboardTarget.Today,
                item.RootId ?? item.Id,
                null,
                GuaranteeFileFocusArea.Actions,
                "منتهية تحتاج متابعة",
                label,
                rank,
                item.Beneficiary,
                item.Bank,
                GuaranteeRow.ResolveBankLogo(item.Bank),
                item.GuaranteeNo,
                item.Amount,
                $"{item.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال",
                "قيمة الضمان المتأثرة",
                item.ExpiryDate.Date,
                item.ExpiryDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                $"متأخر {daysLate.ToString("N0", CultureInfo.InvariantCulture)} يوماً",
                item.GuaranteeType,
                "راجع الإجراء الآن",
                "اليوم • متابعات الانتهاء",
                "فتح اليوم",
                "مراجعة ملف الضمان واتخاذ قرار تشغيل",
                $"انتهى الضمان وما زالت حالته التشغيلية {item.LifecycleStatusLabel}. يحتاج متابعة للتأكد من الإفراج أو التمديد أو الإقفال التشغيلي.",
                TonePalette.Foreground(tone),
                TonePalette.Background(tone),
                TonePalette.Border(tone),
                WorkspaceSurfaceChrome.BrushFrom("#EF4444"));
        }

        private static DashboardWorkItem BuildExpiringSoonItem(Guarantee item)
        {
            int daysLeft = Math.Max(0, (item.ExpiryDate.Date - DateTime.Today).Days);
            (string label, Tone tone, int rank) = daysLeft switch
            {
                <= 3 => ("عاجل", Tone.Danger, 1),
                <= 10 => ("مرتفع", Tone.Warning, 2),
                _ => ("متابعة", Tone.Info, 3)
            };

            return new DashboardWorkItem(
                DashboardScope.ExpiringSoon,
                DashboardTarget.Today,
                item.RootId ?? item.Id,
                null,
                GuaranteeFileFocusArea.Actions,
                "قريبة الانتهاء",
                label,
                rank,
                item.Beneficiary,
                item.Bank,
                GuaranteeRow.ResolveBankLogo(item.Bank),
                item.GuaranteeNo,
                item.Amount,
                $"{item.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال",
                "قيمة الضمان الحالية",
                item.ExpiryDate.Date,
                item.ExpiryDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                $"خلال {daysLeft.ToString("N0", CultureInfo.InvariantCulture)} يوم",
                item.GuaranteeType,
                "راجع التمديد الآن",
                "اليوم • متابعات الانتهاء",
                "فتح اليوم",
                "التأكد من الحاجة إلى تمديد",
                $"الضمان ضمن نافذة الانتهاء القريبة. يستحسن مراجعة الطلبات المرتبطة والتأكد من الحاجة إلى تمديد أو إقفال مبكر.",
                TonePalette.Foreground(tone),
                TonePalette.Background(tone),
                TonePalette.Border(tone),
                WorkspaceSurfaceChrome.BrushFrom("#E09408"));
        }

        private static string BuildSummary(int filteredCount, int totalCount, string normalizedSearch, string normalizedScope)
        {
            if (filteredCount == 0)
            {
                return "لا توجد أعمال يومية مطابقة.";
            }

            bool hasSearch = !string.IsNullOrWhiteSpace(normalizedSearch);
            bool hasScopedLens = !string.Equals(normalizedScope, DashboardScopeFilters.AllWork, StringComparison.Ordinal);

            if (!hasSearch && !hasScopedLens)
            {
                return $"عرض {filteredCount.ToString("N0", CultureInfo.InvariantCulture)} عنصر عمل يومي مرتب حسب الأولوية.";
            }

            string scopeLabel = normalizedScope switch
            {
                DashboardScopeFilters.PendingRequests => "الطلبات المعلقة",
                DashboardScopeFilters.ExpiryFollowUps => "متابعات الانتهاء",
                _ => "أعمال اليوم"
            };

            return $"عرض {filteredCount.ToString("N0", CultureInfo.InvariantCulture)} نتيجة ضمن {scopeLabel} من أصل {totalCount.ToString("N0", CultureInfo.InvariantCulture)}.";
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
                int expiringCount = filteredItems.Count(item => item.Scope == DashboardScope.ExpiringSoon);
                int expiredCount = filteredItems.Count(item => item.Scope == DashboardScope.ExpiredFollowUp);
                decimal totalAmount = filteredItems.Sum(item => item.Amount);
                string closestDate = filteredItems
                    .OrderBy(item => item.SortDate)
                    .FirstOrDefault()?
                    .DueLabel ?? "---";

                return new DashboardWorkspaceMetrics(
                    new DashboardMetricCard("قريب الانتهاء", expiringCount.ToString("N0", CultureInfo.InvariantCulture), "#E09408"),
                    new DashboardMetricCard("منتهي", expiredCount.ToString("N0", CultureInfo.InvariantCulture), "#EF4444"),
                    new DashboardMetricCard("إجمالي القيمة", $"{totalAmount.ToString("N0", CultureInfo.InvariantCulture)} ريال", "#2563EB"),
                    new DashboardMetricCard("أقرب تاريخ", closestDate, "#0F172A"));
            }

            return new DashboardWorkspaceMetrics(
                new DashboardMetricCard("آخر ملف", hasLastFile ? lastFileGuaranteeNo : "لا يوجد", "#2563EB"),
                new DashboardMetricCard("أعمال حرجة", allItems.Count(item => item.PriorityRank <= 1).ToString("N0", CultureInfo.InvariantCulture), "#EF4444"),
                new DashboardMetricCard("طلبات معلقة", pendingRequests.Count.ToString("N0", CultureInfo.InvariantCulture), "#E09408"),
                new DashboardMetricCard("متابعات الانتهاء", guarantees.Count(item => item.NeedsExpiryFollowUp || item.IsExpiringSoon).ToString("N0", CultureInfo.InvariantCulture), "#0F172A"));
        }
    }

    public sealed record DashboardWorkspaceMetrics(
        DashboardMetricCard First,
        DashboardMetricCard Second,
        DashboardMetricCard Third,
        DashboardMetricCard Fourth);

    public sealed record DashboardMetricCard(
        string Label,
        string Value,
        string AccentHex);

    public sealed record DashboardWorkspaceFilterResult(
        IReadOnlyList<DashboardWorkItem> Items,
        DashboardWorkspaceMetrics Metrics,
        string Summary);

    public sealed record DashboardWorkspaceDetailState(
        string Title,
        string Subtitle,
        string BadgeText,
        Brush BadgeForeground,
        Brush BadgeBackground,
        Brush BadgeBorder,
        ImageSource? BankLogo,
        string BankText,
        string AmountHeadline,
        string AmountCaption,
        string Category,
        string Priority,
        string Reference,
        string Due,
        string Workspace,
        string Action,
        string Note,
        string PrimaryActionButtonLabel,
        string WorkspaceButtonLabel,
        bool CanRunPrimaryAction,
        bool CanOpenWorkspace);

    public enum DashboardScope
    {
        PendingRequests,
        ExpiredFollowUp,
        ExpiringSoon
    }

    public enum DashboardTarget
    {
        Today,
        Guarantees,
        Requests,
        Notifications,
        Reports
    }

    public sealed record DashboardWorkItem(
        DashboardScope Scope,
        DashboardTarget Target,
        int RootGuaranteeId,
        int? RequestId,
        GuaranteeFileFocusArea PrimaryFocusArea,
        string CategoryLabel,
        string PriorityLabel,
        int PriorityRank,
        string Title,
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
        string WorkspaceLabel,
        string WorkspaceButtonLabel,
        string NextAction,
        string Note,
        Brush PriorityBrush,
        Brush PriorityBackground,
        Brush PriorityBorder,
        Brush CategoryBrush)
    {
        public string WorkspaceIconKey => Target switch
        {
            DashboardTarget.Today => "Icon.Notifications",
            DashboardTarget.Requests => "Icon.Requests",
            DashboardTarget.Notifications => "Icon.Notifications",
            DashboardTarget.Reports => "Icon.Reports",
            _ => "Icon.Guarantees"
        };

        public string WorkspaceRowActionLabel => Target switch
        {
            DashboardTarget.Today => "اليوم",
            DashboardTarget.Requests => "الطلبات",
            DashboardTarget.Notifications => "التنبيهات",
            DashboardTarget.Reports => "التقارير",
            _ => "الضمانات"
        };
    }
}
