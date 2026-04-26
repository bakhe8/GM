using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using GuaranteeManager.Models;

namespace GuaranteeManager
{
    public sealed class NotificationsWorkspaceDataService
    {
        public List<NotificationWorkspaceItem> BuildItems(
            IReadOnlyList<Guarantee> expiring,
            IReadOnlyList<Guarantee> expired)
        {
            return expiring
                .Select(item => NotificationWorkspaceItem.FromGuarantee(item, isExpired: false))
                .Concat(expired.Select(item => NotificationWorkspaceItem.FromGuarantee(item, isExpired: true)))
                .OrderBy(item => item.ExpiryDateValue)
                .ToList();
        }

        public NotificationsWorkspaceFilterResult BuildFilteredItems(
            IReadOnlyList<NotificationWorkspaceItem> allItems,
            string searchText,
            string levelFilter)
        {
            IEnumerable<NotificationWorkspaceItem> query = allItems;
            query = levelFilter switch
            {
                "قريب الانتهاء" => query.Where(item => !item.IsExpired),
                "منتهي" => query.Where(item => item.IsExpired),
                _ => query
            };

            string normalizedSearch = searchText.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(item =>
                    item.GuaranteeNo.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || item.Bank.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || item.Beneficiary.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
            }

            List<NotificationWorkspaceItem> filtered = query
                .OrderBy(item => item.ExpiryDateValue)
                .ToList();

            string closestDate = allItems.OrderBy(item => item.ExpiryDateValue).FirstOrDefault()?.ExpiryDate ?? "---";
            string summary = filtered.Count == 0
                ? "لا توجد تنبيهات مطابقة."
                : $"عرض 1 - {filtered.Count.ToString("N0", CultureInfo.InvariantCulture)} من أصل {allItems.Count.ToString("N0", CultureInfo.InvariantCulture)} تنبيه";

            return new NotificationsWorkspaceFilterResult(
                filtered,
                new NotificationsWorkspaceMetrics(
                    allItems.Count(item => !item.IsExpired).ToString("N0", CultureInfo.InvariantCulture),
                    allItems.Count(item => item.IsExpired).ToString("N0", CultureInfo.InvariantCulture),
                    $"{allItems.Sum(item => item.Amount).ToString("N0", CultureInfo.InvariantCulture)} ريال",
                    closestDate),
                summary);
        }

        public NotificationsWorkspaceDetailState BuildDetailState(NotificationWorkspaceItem? selectedItem)
        {
            if (selectedItem == null)
            {
                return new NotificationsWorkspaceDetailState(
                    "اختر تنبيهًا",
                    "ستظهر تفاصيل التنبيه المحدد هنا.",
                    "---",
                    null,
                    "---",
                    WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                    WorkspaceSurfaceChrome.BrushFrom("#F8FAFC"),
                    WorkspaceSurfaceChrome.BrushFrom("#E2E8F0"),
                    "---",
                    "---",
                    WorkspaceSurfaceChrome.BrushFrom("#111827"),
                    "---",
                    "---",
                    "---",
                    "---",
                    WorkspaceSurfaceChrome.BrushFrom("#0F172A"),
                    "---");
            }

            return new NotificationsWorkspaceDetailState(
                $"{selectedItem.GuaranteeNo} - {selectedItem.AlertTitle}",
                selectedItem.Beneficiary,
                selectedItem.Bank,
                selectedItem.BankLogo,
                selectedItem.Level,
                selectedItem.LevelBrush,
                selectedItem.LevelBackground,
                selectedItem.LevelBorder,
                selectedItem.ReferenceSummary,
                selectedItem.DaysLabel,
                selectedItem.LevelBrush,
                selectedItem.ExpiryDate,
                selectedItem.FollowUpAction,
                selectedItem.AmountDisplay,
                "القيمة المتأثرة بالتنبيه",
                WorkspaceSurfaceChrome.BrushFrom("#0F172A"),
                selectedItem.OperationalNote);
        }
    }

    public sealed record NotificationsWorkspaceMetrics(
        string Expiring,
        string Expired,
        string Amount,
        string ClosestDate);

    public sealed record NotificationsWorkspaceFilterResult(
        IReadOnlyList<NotificationWorkspaceItem> Items,
        NotificationsWorkspaceMetrics Metrics,
        string Summary);

    public sealed record NotificationsWorkspaceDetailState(
        string Title,
        string Subtitle,
        string BankText,
        ImageSource? BankLogo,
        string BadgeText,
        Brush BadgeForeground,
        Brush BadgeBackground,
        Brush BadgeBorder,
        string Reference,
        string Duration,
        Brush DurationBrush,
        string Expiry,
        string Action,
        string AmountHeadline,
        string AmountCaption,
        Brush AmountBrush,
        string Note);

    public sealed record NotificationWorkspaceItem(
        Guarantee Guarantee,
        string GuaranteeNo,
        string Beneficiary,
        string Bank,
        ImageSource BankLogo,
        decimal Amount,
        string AmountDisplay,
        string ExpiryDate,
        DateTime ExpiryDateValue,
        string AlertTitle,
        string ReferenceSummary,
        string Level,
        Brush LevelBrush,
        Brush LevelBackground,
        Brush LevelBorder,
        string DaysLabel,
        string FollowUpAction,
        string OperationalNote,
        bool IsExpired)
    {
        public static NotificationWorkspaceItem FromGuarantee(Guarantee guarantee, bool isExpired)
        {
            int days = (guarantee.ExpiryDate.Date - DateTime.Today).Days;
            string beneficiary = string.IsNullOrWhiteSpace(guarantee.Beneficiary) ? guarantee.Supplier : guarantee.Beneficiary;
            GuaranteeRow referenceRow = GuaranteeRow.FromGuarantee(guarantee, System.Array.Empty<WorkflowRequest>());
            string followUpAction = isExpired
                ? "بدء متابعة فورية مع البنك والجهة المستفيدة."
                : days <= 7
                    ? "إشعار عاجل قبل نهاية المهلة."
                    : "متابعة وقائية قبل التحول إلى حالة متأخرة.";
            string operationalNote = isExpired
                ? "هذا الضمان تجاوز تاريخ الانتهاء ويحتاج إجراءً تشغيلياً مباشراً."
                : days <= 7
                    ? "المهلة المتبقية قصيرة وتحتاج متابعة مباشرة مع البنك."
                    : "الضمان قريب من الانتهاء ويكفي له مسار متابعة وقائي.";
            return new NotificationWorkspaceItem(
                guarantee,
                guarantee.GuaranteeNo,
                string.IsNullOrWhiteSpace(beneficiary) ? "---" : beneficiary,
                guarantee.Bank,
                GuaranteeRow.ResolveBankLogo(guarantee.Bank),
                guarantee.Amount,
                $"{guarantee.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال",
                guarantee.ExpiryDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                guarantee.ExpiryDate,
                isExpired ? "تنبيه انتهاء" : "تنبيه قرب انتهاء",
                $"{referenceRow.ReferenceFieldLabel}: {referenceRow.ReferenceNumber}",
                isExpired ? "منتهي" : "قريب الانتهاء",
                WorkspaceSurfaceChrome.BrushFrom(isExpired ? "#EF4444" : "#E09408"),
                WorkspaceSurfaceChrome.BrushFrom(isExpired ? "#FFF3F3" : "#FFF9EC"),
                WorkspaceSurfaceChrome.BrushFrom(isExpired ? "#F7C5C5" : "#F6DE99"),
                FormatDaysLabel(days, isExpired),
                followUpAction,
                operationalNote,
                isExpired);
        }

        private static string FormatDaysLabel(int days, bool isExpired)
        {
            int absoluteDays = Math.Abs(days);
            return isExpired
                ? $"متأخر {absoluteDays.ToString("N0", CultureInfo.InvariantCulture)} يوماً"
                : $"خلال {Math.Max(0, days).ToString("N0", CultureInfo.InvariantCulture)} أيام";
        }
    }
}
