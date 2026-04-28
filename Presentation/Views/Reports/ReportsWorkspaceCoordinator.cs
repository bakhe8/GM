using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed class ReportsWorkspaceCoordinator
    {
        private readonly IDatabaseService _database;
        private readonly IExcelService _excel;
        private readonly IUiDiagnosticsService _diagnostics;
        private readonly IShellStatusService _shellStatus;
        private readonly Dictionary<string, ReportRunResult> _reportResults = new(StringComparer.OrdinalIgnoreCase);

        public ReportsWorkspaceCoordinator(
            IDatabaseService database,
            IExcelService excel)
        {
            _database = database;
            _excel = excel;
            _diagnostics = App.CurrentApp.GetRequiredService<IUiDiagnosticsService>();
            _shellStatus = App.CurrentApp.GetRequiredService<IShellStatusService>();
        }

        public IReadOnlyDictionary<string, ReportRunResult> Results => _reportResults;

        public bool TryRun(ReportWorkspaceItem? item)
        {
            if (item == null)
            {
                return false;
            }

            _reportResults[item.Key] = Run(item.Key);
            ReportRunResult result = _reportResults[item.Key];
            if (result.Succeeded)
            {
                _shellStatus.ShowSuccess(result.Message, $"التحليلات • {item.Title}");
            }
            _diagnostics.RecordEvent("reports.operation", "run", new
            {
                item.Key,
                item.Title,
                result.Succeeded,
                result.OutputPath
            });
            return true;
        }

        public void OpenLastReport(ReportWorkspaceItem? item)
        {
            if (item == null || !TryGetResult(item, out ReportRunResult result))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(result.OutputPath) || !File.Exists(result.OutputPath))
            {
                MessageBox.Show("لا يوجد ملف ناتج صالح لفتحه حالياً.", "التحليلات", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(result.OutputPath) { UseShellExecute = true });
                _shellStatus.ShowInfo("تم فتح الملف الناتج.", $"التحليلات • {Path.GetFileName(result.OutputPath)}");
                _diagnostics.RecordEvent("reports.operation", "open-last-output", new { item.Key, item.Title, result.OutputPath });
            }
            catch (Exception ex)
            {
                _diagnostics.RecordEvent("reports.operation", "open-last-output-failed", new { item.Key, item.Title, result.OutputPath, ex.Message });
                MessageBox.Show(ex.Message, "التحليلات", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public bool HasOutput(ReportWorkspaceItem item)
        {
            return TryGetResult(item, out ReportRunResult result)
                && !string.IsNullOrWhiteSpace(result.OutputPath)
                && File.Exists(result.OutputPath);
        }

        private bool TryGetResult(ReportWorkspaceItem item, out ReportRunResult result)
        {
            if (_reportResults.TryGetValue(item.Key, out ReportRunResult? stored))
            {
                result = stored!;
                return true;
            }

            result = default!;
            return false;
        }

        private ReportRunResult Run(string reportKey)
        {
            try
            {
                bool exported = WorkspaceReportCatalog.Run(reportKey, _database, _excel);
                if (!exported)
                {
                    return new ReportRunResult(false, "لم يتم إنشاء التقرير.", string.Empty);
                }

                string outputPath = _excel.LastOutputPath ?? string.Empty;
                string fileName = string.IsNullOrWhiteSpace(outputPath) ? "ملف التقرير" : Path.GetFileName(outputPath);
                return new ReportRunResult(true, $"تم إنشاء التقرير: {fileName}", outputPath);
            }
            catch (Exception ex)
            {
                return new ReportRunResult(false, ex.Message, string.Empty);
            }
        }
    }

    public sealed record ReportRunResult(bool Succeeded, string Message, string OutputPath);
}
