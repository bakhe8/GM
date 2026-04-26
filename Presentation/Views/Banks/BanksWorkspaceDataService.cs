using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using GuaranteeManager.Models;

namespace GuaranteeManager
{
    public sealed class BanksWorkspaceDataService
    {
        public List<BankWorkspaceItem> BuildItems(IReadOnlyList<Guarantee> guarantees)
        {
            decimal totalAmount = guarantees.Sum(item => item.Amount);

            return guarantees
                .GroupBy(item => item.Bank)
                .Select(group =>
                {
                    Guarantee first = group.First();
                    decimal amount = group.Sum(item => item.Amount);
                    string topBeneficiary = group
                        .GroupBy(item => string.IsNullOrWhiteSpace(item.Beneficiary) ? item.Supplier : item.Beneficiary)
                        .OrderByDescending(item => item.Count())
                        .Select(item => item.Key)
                        .FirstOrDefault() ?? "---";

                    return new BankWorkspaceItem(
                        group.Key,
                        group.Count(),
                        group.Count(item => item.LifecycleStatus == GuaranteeLifecycleStatus.Active),
                        group.Count(item => item.IsExpiringSoon),
                        group.Count(item => item.IsExpired),
                        amount,
                        totalAmount <= 0 ? 0 : (amount / totalAmount) * 100m,
                        topBeneficiary,
                        GuaranteeRow.FromGuarantee(first, System.Array.Empty<WorkflowRequest>()).BankLogo);
                })
                .OrderByDescending(item => item.Amount)
                .ToList();
        }

        public BanksWorkspaceFilterResult BuildFilteredItems(
            IReadOnlyList<BankWorkspaceItem> allBanks,
            string searchText,
            string sortFilter)
        {
            IEnumerable<BankWorkspaceItem> query = allBanks;
            string normalizedSearch = searchText.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(item =>
                    item.Bank.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    item.TopBeneficiary.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
            }

            query = sortFilter switch
            {
                "الأكثر عدداً" => query.OrderByDescending(item => item.Count).ThenBy(item => item.Bank),
                "الأعلى نشاطاً" => query.OrderByDescending(item => item.Active).ThenByDescending(item => item.Amount),
                _ => query.OrderByDescending(item => item.Amount).ThenBy(item => item.Bank)
            };

            List<BankWorkspaceItem> filtered = query.ToList();
            string summary = filtered.Count == 0
                ? "لا توجد بنوك مطابقة."
                : $"عرض 1 - {filtered.Count.ToString("N0", CultureInfo.InvariantCulture)} من أصل {allBanks.Count.ToString("N0", CultureInfo.InvariantCulture)} بنك";

            return new BanksWorkspaceFilterResult(
                filtered,
                new BanksWorkspaceMetrics(
                    allBanks.Count.ToString("N0", CultureInfo.InvariantCulture),
                    allBanks.Sum(item => item.Count).ToString("N0", CultureInfo.InvariantCulture),
                    allBanks.Sum(item => item.Active).ToString("N0", CultureInfo.InvariantCulture),
                    $"{allBanks.Sum(item => item.Amount).ToString("N0", CultureInfo.InvariantCulture)} ريال"),
                summary);
        }

        public BanksWorkspaceDetailState BuildDetailState(BankWorkspaceItem? selectedItem)
        {
            if (selectedItem == null)
            {
                return new BanksWorkspaceDetailState(
                    null,
                    "اختر بنكاً",
                    "ستظهر تفاصيل البنك المحدد هنا.",
                    "---",
                    WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                    WorkspaceSurfaceChrome.BrushFrom("#F8FAFC"),
                    WorkspaceSurfaceChrome.BrushFrom("#E2E8F0"),
                    "---",
                    "---",
                    "---",
                    WorkspaceSurfaceChrome.BrushFrom("#16A34A"),
                    "---",
                    WorkspaceSurfaceChrome.BrushFrom("#E09408"),
                    "---",
                    WorkspaceSurfaceChrome.BrushFrom("#EF4444"),
                    "---",
                    "---");
            }

            return new BanksWorkspaceDetailState(
                selectedItem.Logo,
                selectedItem.Bank,
                $"أعلى مستفيد: {selectedItem.TopBeneficiary}",
                selectedItem.PortfolioStatusLabel,
                selectedItem.PortfolioStatusBrush,
                selectedItem.PortfolioStatusBackground,
                selectedItem.PortfolioStatusBorder,
                selectedItem.AmountDisplay,
                "إجمالي قيمة الضمانات لدى البنك",
                selectedItem.CountDisplay,
                WorkspaceSurfaceChrome.BrushFrom("#16A34A"),
                selectedItem.ActiveDisplay,
                WorkspaceSurfaceChrome.BrushFrom("#E09408"),
                selectedItem.ExpiringDisplay,
                WorkspaceSurfaceChrome.BrushFrom("#EF4444"),
                selectedItem.ExpiredDisplay,
                selectedItem.ShareDisplay);
        }
    }

    public sealed record BanksWorkspaceMetrics(
        string BankCount,
        string GuaranteeCount,
        string ActiveCount,
        string Amount);

    public sealed record BanksWorkspaceFilterResult(
        IReadOnlyList<BankWorkspaceItem> Items,
        BanksWorkspaceMetrics Metrics,
        string Summary);

    public sealed record BanksWorkspaceDetailState(
        ImageSource? Logo,
        string Title,
        string Subtitle,
        string BadgeText,
        Brush BadgeForeground,
        Brush BadgeBackground,
        Brush BadgeBorder,
        string AmountHeadline,
        string AmountCaption,
        string Count,
        Brush ActiveBrush,
        string Active,
        Brush ExpiringBrush,
        string Expiring,
        Brush ExpiredBrush,
        string Expired,
        string Share);

    public sealed record BankWorkspaceItem(
        string Bank,
        int Count,
        int Active,
        int ExpiringSoon,
        int Expired,
        decimal Amount,
        decimal Share,
        string TopBeneficiary,
        ImageSource Logo)
    {
        public string AmountDisplay => $"{Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال";
        public string ShareDisplay => $"{Share:0.#}%";
        public string CountDisplay => Count.ToString("N0", CultureInfo.InvariantCulture);
        public string ActiveDisplay => Active.ToString("N0", CultureInfo.InvariantCulture);
        public string ExpiringDisplay => ExpiringSoon.ToString("N0", CultureInfo.InvariantCulture);
        public string ExpiredDisplay => Expired.ToString("N0", CultureInfo.InvariantCulture);
        public string PortfolioStatusLabel => Expired == Count ? "محفظة منتهية" : ExpiringSoon > 0 ? "تحتاج متابعة" : "محفظة نشطة";
        public Brush PortfolioStatusBrush => Expired == Count
            ? WorkspaceSurfaceChrome.BrushFrom("#EF4444")
            : ExpiringSoon > 0
                ? WorkspaceSurfaceChrome.BrushFrom("#E09408")
                : WorkspaceSurfaceChrome.BrushFrom("#16A34A");
        public Brush PortfolioStatusBackground => Expired == Count
            ? WorkspaceSurfaceChrome.BrushFrom("#FFF3F3")
            : ExpiringSoon > 0
                ? WorkspaceSurfaceChrome.BrushFrom("#FFF9EC")
                : WorkspaceSurfaceChrome.BrushFrom("#F2FBF4");
        public Brush PortfolioStatusBorder => Expired == Count
            ? WorkspaceSurfaceChrome.BrushFrom("#F7C5C5")
            : ExpiringSoon > 0
                ? WorkspaceSurfaceChrome.BrushFrom("#F6DE99")
                : WorkspaceSurfaceChrome.BrushFrom("#C9EFCF");
    }
}
