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
            string categoryFilter)
        {
            IEnumerable<ReportWorkspaceItem> query = allReports;
            query = categoryFilter switch
            {
                ReportWorkspaceItem.PortfolioFilterLabel => query.Where(item => item.CategoryFilter == ReportWorkspaceItem.PortfolioFilterLabel),
                ReportWorkspaceItem.RequestsFilterLabel => query.Where(item => item.CategoryFilter == ReportWorkspaceItem.RequestsFilterLabel),
                ReportWorkspaceItem.OperationalFilterLabel => query.Where(item => item.CategoryFilter == ReportWorkspaceItem.OperationalFilterLabel),
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

            return new ReportsWorkspaceFilterResult(
                filtered,
                new ReportsWorkspaceMetrics(
                    CountCategory(allReports, ReportWorkspaceItem.PortfolioFilterLabel),
                    CountCategory(allReports, ReportWorkspaceItem.RequestsFilterLabel),
                    CountCategory(allReports, ReportWorkspaceItem.OperationalFilterLabel),
                    allReports.Count.ToString("N0", CultureInfo.InvariantCulture)));
        }

        private static string CountCategory(IReadOnlyList<ReportWorkspaceItem> reports, string categoryFilter)
        {
            return reports
                .Count(item => item.CategoryFilter == categoryFilter)
                .ToString("N0", CultureInfo.InvariantCulture);
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
                    "اختر تقريرًا",
                    "ستظهر تفاصيل التقرير المحدد هنا مع آخر ملف ناتج له.",
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
            string output = TryGetResult(selectedItem, results, out ReportRunResult result)
                ? !string.IsNullOrWhiteSpace(result.OutputPath)
                    ? result.OutputPath
                    : result.Succeeded
                        ? result.Message
                        : "لم يتم إنشاء ملف ناتج بعد."
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
                    "أنشئ التقرير الآن.",
                    WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                    WorkspaceSurfaceChrome.BrushFrom("#F8FAFC"),
                    WorkspaceSurfaceChrome.BrushFrom("#E2E8F0"));
            }

            if (result.Succeeded)
            {
                bool hasOutput = !string.IsNullOrWhiteSpace(result.OutputPath);
                return new ReportRunState(
                    "آخر تشغيل ناجح",
                    string.IsNullOrWhiteSpace(result.Message) ? "تم إنشاء ملف ناتج حديث لهذا التقرير." : result.Message,
                    hasOutput ? "يمكن فتح الملف الناتج أو إعادة إنشاء نسخة أحدث." : "يمكن إعادة تنفيذ الإجراء عند الحاجة.",
                    WorkspaceSurfaceChrome.BrushFrom("#16A34A"),
                    WorkspaceSurfaceChrome.BrushFrom("#F2FBF4"),
                    WorkspaceSurfaceChrome.BrushFrom("#C9EFCF"));
            }

            return new ReportRunState(
                "تعذر الإنشاء",
                string.IsNullOrWhiteSpace(result.Message) ? "فشل إنشاء التقرير في آخر محاولة." : result.Message,
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
        string Requests,
        string Operational,
        string Total);

    public sealed record ReportsWorkspaceFilterResult(
        IReadOnlyList<ReportWorkspaceItem> Items,
        ReportsWorkspaceMetrics Metrics);

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
        string CategoryFilter,
        Brush CategoryBrush)
    {
        public const string PortfolioFilterLabel = "تقارير المحفظة";
        public const string RequestsFilterLabel = "تقارير الطلبات";
        public const string OperationalFilterLabel = "تقارير تشغيلية";

        public static ReportWorkspaceItem FromAction(WorkspaceReportCatalog.WorkspaceReportAction action)
        {
            if (action.Key.StartsWith("portfolio.", StringComparison.OrdinalIgnoreCase))
            {
                return new ReportWorkspaceItem(
                    action.Key,
                    action.Title,
                    action.Description,
                    "تقرير محفظة",
                    PortfolioFilterLabel,
                    WorkspaceSurfaceChrome.BrushFrom("#2563EB"));
            }

            if (action.Key.StartsWith("requests.", StringComparison.OrdinalIgnoreCase))
            {
                return new ReportWorkspaceItem(
                    action.Key,
                    action.Title,
                    action.Description,
                    "تقرير طلبات",
                    RequestsFilterLabel,
                    WorkspaceSurfaceChrome.BrushFrom("#E09408"));
            }

            return new ReportWorkspaceItem(
                action.Key,
                action.Title,
                action.Description,
                "تقرير تشغيلي",
                OperationalFilterLabel,
                WorkspaceSurfaceChrome.BrushFrom("#16A34A"));
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
