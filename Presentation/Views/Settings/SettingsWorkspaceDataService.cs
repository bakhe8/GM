using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media;
using GuaranteeManager.Utils;

namespace GuaranteeManager
{
    public sealed class SettingsWorkspaceDataService
    {
        public List<SettingPathItem> BuildItems()
        {
            return
            [
                new SettingPathItem("قاعدة البيانات", "بيانات", AppPaths.DatabasePath, Path.GetDirectoryName(AppPaths.DatabasePath) ?? AppPaths.DataFolder, File.Exists(AppPaths.DatabasePath)),
                new SettingPathItem("المرفقات", "بيانات", AppPaths.AttachmentsFolder, AppPaths.AttachmentsFolder, Directory.Exists(AppPaths.AttachmentsFolder)),
                new SettingPathItem("خطابات الطلبات", "سير العمل", AppPaths.WorkflowLettersFolder, AppPaths.WorkflowLettersFolder, Directory.Exists(AppPaths.WorkflowLettersFolder)),
                new SettingPathItem("ردود البنوك", "سير العمل", AppPaths.WorkflowResponsesFolder, AppPaths.WorkflowResponsesFolder, Directory.Exists(AppPaths.WorkflowResponsesFolder)),
                new SettingPathItem("السجلات", "سير العمل", AppPaths.LogsFolder, AppPaths.LogsFolder, Directory.Exists(AppPaths.LogsFolder))
            ];
        }

        public SettingsWorkspaceFilterResult BuildFilteredItems(
            IReadOnlyList<SettingPathItem> allItems,
            string searchText,
            string categoryFilter)
        {
            IEnumerable<SettingPathItem> query = allItems;
            query = categoryFilter switch
            {
                SettingsPathFilters.Data => query.Where(item => item.Category == SettingsPathFilters.Data),
                SettingsPathFilters.Workflow => query.Where(item => item.Category == SettingsPathFilters.Workflow),
                _ => query
            };

            string normalizedSearch = searchText.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(item =>
                    item.Label.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                    item.Path.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
            }

            return new SettingsWorkspaceFilterResult(
                query.ToList(),
                BuildMetrics(allItems));
        }

        private static SettingsWorkspaceMetrics BuildMetrics(IReadOnlyList<SettingPathItem> allItems)
        {
            int dataTotal = allItems.Count(item => item.Category == SettingsPathFilters.Data);
            int dataReady = allItems.Count(item => item.Category == SettingsPathFilters.Data && item.IsReady);
            int workflowTotal = allItems.Count(item => item.Category == SettingsPathFilters.Workflow);
            int workflowReady = allItems.Count(item => item.Category == SettingsPathFilters.Workflow && item.IsReady);

            return new SettingsWorkspaceMetrics(
                allItems.Count.ToString("N0", CultureInfo.InvariantCulture),
                $"{dataReady.ToString("N0", CultureInfo.InvariantCulture)}/{dataTotal.ToString("N0", CultureInfo.InvariantCulture)}",
                $"{workflowReady.ToString("N0", CultureInfo.InvariantCulture)}/{workflowTotal.ToString("N0", CultureInfo.InvariantCulture)}");
        }

        public SettingsWorkspaceDetailState BuildDetailState(SettingPathItem? selectedItem)
        {
            if (selectedItem == null)
            {
                return new SettingsWorkspaceDetailState(
                    "اختر مساراً",
                    "ستظهر تفاصيل العنصر المحدد هنا.",
                    "---",
                    WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                    WorkspaceSurfaceChrome.BrushFrom("#F8FAFC"),
                    WorkspaceSurfaceChrome.BrushFrom("#E3E9F2"),
                    "---",
                    WorkspaceSurfaceChrome.BrushFrom("#0F172A"),
                    "---",
                    "---",
                    "---");
            }

            return new SettingsWorkspaceDetailState(
                selectedItem.Label,
                selectedItem.Category,
                selectedItem.StateLabel,
                selectedItem.StateBrush,
                selectedItem.StateBackground,
                selectedItem.StateBorder,
                selectedItem.StateLabel,
                selectedItem.StateBrush,
                selectedItem.ActionLabel,
                selectedItem.Path,
                selectedItem.OpenPath);
        }
    }

    public sealed record SettingsWorkspaceMetrics(
        string Total,
        string Data,
        string Workflow);

    public sealed record SettingsWorkspaceFilterResult(
        IReadOnlyList<SettingPathItem> Items,
        SettingsWorkspaceMetrics Metrics);

    public sealed record SettingsWorkspaceDetailState(
        string Title,
        string Subtitle,
        string BadgeText,
        Brush BadgeForeground,
        Brush BadgeBackground,
        Brush BadgeBorder,
        string State,
        Brush StateBrush,
        string Action,
        string Path,
        string OpenPath);

    public sealed record SettingPathItem(string Label, string Category, string Path, string OpenPath, bool IsReady)
    {
        public string StateLabel => IsReady ? "جاهز" : "غير جاهز";
        public Brush StateBrush => WorkspaceSurfaceChrome.BrushFrom(IsReady ? "#16A34A" : "#EF4444");
        public Brush StateBackground => WorkspaceSurfaceChrome.BrushFrom(IsReady ? "#F2FBF4" : "#FFF3F3");
        public Brush StateBorder => WorkspaceSurfaceChrome.BrushFrom(IsReady ? "#C9EFCF" : "#F7C5C5");
        public Brush CategoryBrush => WorkspaceSurfaceChrome.BrushFrom(Category == "بيانات" ? "#2563EB" : "#E09408");
        public string ActionLabel => IsReady ? "يمكن فتح المسار أو نسخه مباشرة." : "راجع وجود المجلد أو أنشئه قبل المتابعة.";
    }

    public static class SettingsPathFilters
    {
        public const string All = "كل المسارات";
        public const string Data = "بيانات";
        public const string Workflow = "سير العمل";

        public static string Normalize(string? filter)
        {
            return filter switch
            {
                Data => Data,
                Workflow => Workflow,
                _ => All
            };
        }
    }
}
