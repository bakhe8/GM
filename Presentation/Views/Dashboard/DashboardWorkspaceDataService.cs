using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using GuaranteeManager.Models;

namespace GuaranteeManager
{
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
            query = scopeFilter switch
            {
                "طلبات معلقة" => query.Where(item => item.Scope == DashboardScope.PendingRequests),
                "منتهية تحتاج متابعة" => query.Where(item => item.Scope == DashboardScope.ExpiredFollowUp),
                "قريبة الانتهاء" => query.Where(item => item.Scope == DashboardScope.ExpiringSoon),
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
                .Take(8)
                .ToList();

            string summary = filtered.Count == 0
                ? "لا توجد أعمال يومية مطابقة."
                : $"عرض أعلى {filtered.Count.ToString("N0", CultureInfo.InvariantCulture)} أولويات من أصل {allItems.Count.ToString("N0", CultureInfo.InvariantCulture)}";

            return new DashboardWorkspaceFilterResult(
                filtered,
                new DashboardWorkspaceMetrics(
                    hasLastFile ? lastFileGuaranteeNo : "لا يوجد",
                    allItems.Count(item => item.PriorityRank <= 1).ToString("N0", CultureInfo.InvariantCulture),
                    pendingRequests.Count.ToString("N0", CultureInfo.InvariantCulture),
                    guarantees.Count(item => item.IsExpiringSoon).ToString("N0", CultureInfo.InvariantCulture)),
                summary);
        }

    public DashboardWorkspaceDetailState BuildDetailState(
        DashboardWorkItem? selected,
        bool hasLastFile,
        string lastFileGuaranteeNo,
        string lastFileSummary)
        {
            if (selected == null)
            {
                return new DashboardWorkspaceDetailState(
                    "لا توجد أولوية محددة",
                    "ابدأ باختيار عنصر من قائمة الأولويات اليومية.",
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
                    hasLastFile
                    ? $"آخر ملف تم العمل عليه: {lastFileGuaranteeNo} | {lastFileSummary}"
                    : "لا يوجد ملف حديث بعد. ابدأ بفتح الضمانات أو اختيار عنصر من أعمال اليوم.",
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
                "اليوم",
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
                "اليوم",
                "فتح اليوم",
                "التأكد من الحاجة إلى تمديد",
                $"الضمان ضمن نافذة الانتهاء القريبة. يستحسن مراجعة الطلبات المرتبطة والتأكد من الحاجة إلى تمديد أو إقفال مبكر.",
                TonePalette.Foreground(tone),
                TonePalette.Background(tone),
                TonePalette.Border(tone),
                WorkspaceSurfaceChrome.BrushFrom("#E09408"));
        }
    }

    public sealed record DashboardWorkspaceMetrics(
        string LastFile,
        string CriticalWork,
        string PendingRequests,
        string ExpiringSoon);

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
