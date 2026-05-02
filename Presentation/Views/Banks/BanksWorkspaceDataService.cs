using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager
{
    public sealed class BanksWorkspaceDataService
    {
        public List<BankWorkspaceItem> BuildItems(
            IReadOnlyList<Guarantee> guarantees,
            IReadOnlyList<string> bankReferences)
        {
            decimal totalAmount = guarantees.Sum(item => item.Amount);

            List<BankWorkspaceItem> items = guarantees
                .Where(item => !string.IsNullOrWhiteSpace(item.Bank))
                .GroupBy(item => item.Bank.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    Guarantee first = group.First();
                    decimal amount = group.Sum(item => item.Amount);
                    string topSupplier = group
                        .GroupBy(item => string.IsNullOrWhiteSpace(item.Supplier) ? "---" : item.Supplier.Trim())
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
                        topSupplier,
                        GuaranteeRow.ResolveBankLogo(first.Bank));
                })
                .OrderByDescending(item => item.Amount)
                .ToList();

            var existingBanks = new HashSet<string>(
                items.Select(item => item.Bank),
                StringComparer.OrdinalIgnoreCase);

            foreach (string bankReference in bankReferences)
            {
                string bank = bankReference.Trim();
                if (string.IsNullOrWhiteSpace(bank) || !existingBanks.Add(bank))
                {
                    continue;
                }

                items.Add(BankWorkspaceItem.Empty(bank));
            }

            return items
                .OrderByDescending(item => item.Amount)
                .ThenBy(item => item.Bank, StringComparer.OrdinalIgnoreCase)
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
                    item.TopSupplier.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
            }

            query = sortFilter switch
            {
                "الأكثر عدداً" => query.OrderByDescending(item => item.Count).ThenBy(item => item.Bank),
                "الأعلى نشاطاً" => query.OrderByDescending(item => item.Active).ThenByDescending(item => item.Amount),
                _ => query.OrderByDescending(item => item.Amount).ThenBy(item => item.Bank)
            };

            return new BanksWorkspaceFilterResult(
                query.ToList(),
                new BanksWorkspaceMetrics(
                    allBanks.Count.ToString("N0", CultureInfo.InvariantCulture),
                    allBanks.Sum(item => item.Count).ToString("N0", CultureInfo.InvariantCulture),
                    $"{allBanks.Sum(item => item.Amount).ToString("N0", CultureInfo.InvariantCulture)} ريال"));
        }

        public BanksWorkspaceDetailState BuildDetailState(BankWorkspaceItem? selectedItem)
        {
            if (selectedItem == null)
            {
                return new BanksWorkspaceDetailState(
                    null,
                    "اختر بنكاً",
                    "ستظهر بيانات البنك المحدد هنا.",
                    "---",
                    WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                    WorkspaceSurfaceChrome.BrushFrom("#F8FAFC"),
                    WorkspaceSurfaceChrome.BrushFrom("#E3E9F2"),
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
                selectedItem.Count == 0
                    ? "لا توجد ضمانات مرتبطة بهذا البنك بعد."
                    : $"أعلى مورد: {selectedItem.TopSupplier}",
                selectedItem.PortfolioStatusLabel,
                selectedItem.PortfolioStatusBrush,
                selectedItem.PortfolioStatusBackground,
                selectedItem.PortfolioStatusBorder,
                selectedItem.AmountDisplay,
                selectedItem.AmountInWords,
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
        string Amount);

    public sealed record BanksWorkspaceFilterResult(
        IReadOnlyList<BankWorkspaceItem> Items,
        BanksWorkspaceMetrics Metrics);

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
        string TopSupplier,
        ImageSource Logo)
    {
        public string AmountDisplay => ArabicAmountFormatter.FormatSaudiRiyals(Amount);
        public string AmountInWords => ArabicAmountFormatter.FormatSaudiRiyalsInWords(Amount);
        public string ShareDisplay => $"{Share:0.#}%";
        public string CountDisplay => Count.ToString("N0", CultureInfo.InvariantCulture);
        public string ActiveDisplay => Active.ToString("N0", CultureInfo.InvariantCulture);
        public string ExpiringDisplay => ExpiringSoon.ToString("N0", CultureInfo.InvariantCulture);
        public string ExpiredDisplay => Expired.ToString("N0", CultureInfo.InvariantCulture);
        public string PortfolioStatusLabel => Count == 0
            ? "لا ضمانات بعد"
            : Expired == Count
                ? "كلها منتهية"
                : Expired > 0
                    ? $"منتهي: {Expired.ToString("N0", CultureInfo.InvariantCulture)}"
                    : ExpiringSoon > 0
                        ? $"قريب الانتهاء: {ExpiringSoon.ToString("N0", CultureInfo.InvariantCulture)}"
                        : "محفظة نشطة";
        public Brush PortfolioStatusBrush => Count == 0
            ? WorkspaceSurfaceChrome.BrushFrom("#64748B")
            : Expired > 0
                ? WorkspaceSurfaceChrome.BrushFrom("#EF4444")
            : ExpiringSoon > 0
                ? WorkspaceSurfaceChrome.BrushFrom("#E09408")
                : WorkspaceSurfaceChrome.BrushFrom("#16A34A");
        public Brush PortfolioStatusBackground => Count == 0
            ? WorkspaceSurfaceChrome.BrushFrom("#F8FAFC")
            : Expired > 0
                ? WorkspaceSurfaceChrome.BrushFrom("#FFF3F3")
            : ExpiringSoon > 0
                ? WorkspaceSurfaceChrome.BrushFrom("#FFF9EC")
                : WorkspaceSurfaceChrome.BrushFrom("#F2FBF4");
        public Brush PortfolioStatusBorder => Count == 0
            ? WorkspaceSurfaceChrome.BrushFrom("#E3E9F2")
            : Expired > 0
                ? WorkspaceSurfaceChrome.BrushFrom("#F7C5C5")
            : ExpiringSoon > 0
                ? WorkspaceSurfaceChrome.BrushFrom("#F6DE99")
                : WorkspaceSurfaceChrome.BrushFrom("#C9EFCF");

        public static BankWorkspaceItem Empty(string bank)
        {
            return new BankWorkspaceItem(
                bank,
                0,
                0,
                0,
                0,
                0m,
                0m,
                "لا توجد ضمانات بعد",
                GuaranteeRow.ResolveBankLogo(bank));
        }
    }
}
