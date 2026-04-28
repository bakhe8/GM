using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media;
using GuaranteeManager.Utils;

namespace GuaranteeManager
{
    public sealed class ReportsWorkspaceDataService
    {
        public List<ReportWorkspaceItem> BuildItems(IReadOnlyList<WorkspaceReportCatalog.WorkspaceReportAction> actions)
        {
            return actions
                .Select(ReportWorkspaceItem.FromAction)
                .ToList();
        }

        public ReportsWorkspaceFilterResult BuildFilteredItems(
            IReadOnlyList<ReportWorkspaceItem> allReports,
            string searchText,
            string categoryFilter,
            IReadOnlyDictionary<string, ReportRunResult> results)
        {
            IEnumerable<ReportWorkspaceItem> query = allReports;
            query = categoryFilter switch
            {
                "مخرجات المحفظة" => query.Where(item => item.IsPortfolio),
                "مخرجات تشغيلية" => query.Where(item => !item.IsPortfolio),
                _ => query
            };

            string normalizedSearch = searchText.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(item =>
                    item.Title.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || item.Description.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || item.Key.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
            }

            List<ReportWorkspaceItem> filtered = query.ToList();
            string status = results.Values.Any(result => !result.Succeeded) ? "متابعة" : "جاهز";
            string summary = filtered.Count == 0
                ? "لا توجد مخرجات مطابقة."
                : $"عرض 1 - {filtered.Count.ToString("N0", CultureInfo.InvariantCulture)} من أصل {allReports.Count.ToString("N0", CultureInfo.InvariantCulture)} مخرج";

            return new ReportsWorkspaceFilterResult(
                filtered,
                new ReportsWorkspaceMetrics(
                    allReports.Count(item => item.IsPortfolio).ToString("N0", CultureInfo.InvariantCulture),
                    allReports.Count(item => !item.IsPortfolio).ToString("N0", CultureInfo.InvariantCulture),
                    allReports.Count.ToString("N0", CultureInfo.InvariantCulture),
                    status),
                summary);
        }

        public ReportWorkspaceRowState BuildRowState(
            ReportWorkspaceItem item,
            IReadOnlyDictionary<string, ReportRunResult> results,
            bool canOpen)
        {
            if (!TryGetResult(item, results, out ReportRunResult result))
            {
                return new ReportWorkspaceRowState("جاهز", WorkspaceSurfaceChrome.BrushFrom("#64748B"), canOpen);
            }

            return new ReportWorkspaceRowState(
                result.Succeeded ? "تم الإنشاء" : "فشل",
                WorkspaceSurfaceChrome.BrushFrom(result.Succeeded ? "#16A34A" : "#EF4444"),
                canOpen);
        }

        public ReportsWorkspaceDetailState BuildDetailState(
            ReportWorkspaceItem? selectedItem,
            IReadOnlyDictionary<string, ReportRunResult> results,
            bool canOpen)
        {
            if (selectedItem == null)
            {
                return new ReportsWorkspaceDetailState(
                    "اختر مخرجًا",
                    "ستظهر تفاصيل المخرج المحدد هنا مع آخر ملف ناتج له.",
                    "---",
                    WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                    WorkspaceSurfaceChrome.BrushFrom("#F8FAFC"),
                    WorkspaceSurfaceChrome.BrushFrom("#E2E8F0"),
                    "---",
                    "---",
                    WorkspaceSurfaceChrome.BrushFrom("#111827"),
                    "---",
                    WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                    "---",
                    "---",
                    false,
                    false);
            }

            ReportRunState runState = GetRunState(selectedItem, results);
            string output = TryGetResult(selectedItem, results, out ReportRunResult result) && !string.IsNullOrWhiteSpace(result.OutputPath)
                ? result.OutputPath
                : "لم يتم إنشاء ملف ناتج بعد.";

            return new ReportsWorkspaceDetailState(
                selectedItem.Title,
                selectedItem.Description,
                runState.BadgeLabel,
                runState.Foreground,
                runState.Background,
                runState.Border,
                selectedItem.Key,
                selectedItem.Category,
                selectedItem.CategoryBrush,
                runState.DetailLabel,
                runState.Foreground,
                runState.ActionLabel,
                output,
                true,
                canOpen);
        }

        private static ReportRunState GetRunState(
            ReportWorkspaceItem item,
            IReadOnlyDictionary<string, ReportRunResult> results)
        {
            if (!TryGetResult(item, results, out ReportRunResult result))
            {
                return new ReportRunState(
                    "جاهز للتشغيل",
                    "جاهز للتشغيل",
                    "أنشئ المخرج الآن.",
                    WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                    WorkspaceSurfaceChrome.BrushFrom("#F8FAFC"),
                    WorkspaceSurfaceChrome.BrushFrom("#E2E8F0"));
            }

            if (result.Succeeded)
            {
                return new ReportRunState(
                    "آخر تشغيل ناجح",
                    string.IsNullOrWhiteSpace(result.Message) ? "تم إنشاء ملف ناتج حديث لهذا المخرج." : result.Message,
                    "يمكن فتح الملف الناتج أو إعادة إنشاء نسخة أحدث.",
                    WorkspaceSurfaceChrome.BrushFrom("#16A34A"),
                    WorkspaceSurfaceChrome.BrushFrom("#F2FBF4"),
                    WorkspaceSurfaceChrome.BrushFrom("#C9EFCF"));
            }

            return new ReportRunState(
                "تعذر الإنشاء",
                string.IsNullOrWhiteSpace(result.Message) ? "فشل إنشاء المخرج في آخر محاولة." : result.Message,
                "راجع بيانات الإدخال ثم أعد المحاولة.",
                WorkspaceSurfaceChrome.BrushFrom("#EF4444"),
                WorkspaceSurfaceChrome.BrushFrom("#FFF3F3"),
                WorkspaceSurfaceChrome.BrushFrom("#F7C5C5"));
        }

        private static bool TryGetResult(
            ReportWorkspaceItem item,
            IReadOnlyDictionary<string, ReportRunResult> results,
            out ReportRunResult result)
        {
            if (results.TryGetValue(item.Key, out ReportRunResult? stored))
            {
                result = stored!;
                return true;
            }

            result = default!;
            return false;
        }
    }

    public sealed record ReportsWorkspaceMetrics(
        string Portfolio,
        string Operational,
        string Total,
        string Status);

    public sealed record ReportsWorkspaceFilterResult(
        IReadOnlyList<ReportWorkspaceItem> Items,
        ReportsWorkspaceMetrics Metrics,
        string Summary);

    public sealed record ReportsWorkspaceDetailState(
        string Title,
        string Subtitle,
        string BadgeText,
        Brush BadgeForeground,
        Brush BadgeBackground,
        Brush BadgeBorder,
        string Key,
        string Category,
        Brush CategoryBrush,
        string Status,
        Brush StatusBrush,
        string Action,
        string Output,
        bool CanRun,
        bool CanOpen);

    public sealed record ReportWorkspaceRowState(
        string StatusLabel,
        Brush StatusBrush,
        bool CanOpen);

    public sealed record ReportWorkspaceItem(
        string Key,
        string Title,
        string Description,
        string Category,
        bool IsPortfolio,
        Brush CategoryBrush)
    {
        public static ReportWorkspaceItem FromAction(WorkspaceReportCatalog.WorkspaceReportAction action)
        {
            bool isPortfolio = action.Key.StartsWith("portfolio.", StringComparison.OrdinalIgnoreCase);
            return new ReportWorkspaceItem(
                action.Key,
                action.Title,
                action.Description,
                isPortfolio ? "مخرج محفظة" : "مخرج تشغيلي",
                isPortfolio,
                WorkspaceSurfaceChrome.BrushFrom(isPortfolio ? "#2563EB" : "#E09408"));
        }
    }

    public sealed record ReportRunState(
        string BadgeLabel,
        string DetailLabel,
        string ActionLabel,
        Brush Foreground,
        Brush Background,
        Brush Border);
}
