using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    public sealed class UiDiagnosticsService : IUiDiagnosticsService
    {
        private static readonly Lock FileLock = new();
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        private static readonly JsonSerializerOptions JsonLineOptions = new()
        {
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public string EventLogPath => Path.Combine(AppPaths.LogsFolder, "ui-events.jsonl");

        public string ShellStatePath => Path.Combine(AppPaths.LogsFolder, "ui-shell-state.json");

        public void RecordEvent(string category, string action, object? payload = null)
        {
            AppPaths.EnsureDirectoriesExist();

            UiDiagnosticsEventRecord record = new(
                DateTimeOffset.Now,
                category,
                action,
                payload);

            string line = JsonSerializer.Serialize(record, JsonLineOptions) + Environment.NewLine;
            lock (FileLock)
            {
                File.AppendAllText(EventLogPath, line);
            }
        }

        public void UpdateShellState(UiShellDiagnosticsState state)
        {
            AppPaths.EnsureDirectoriesExist();
            string content = JsonSerializer.Serialize(state, JsonOptions);
            lock (FileLock)
            {
                File.WriteAllText(ShellStatePath, content);
            }
        }

        private sealed record UiDiagnosticsEventRecord(
            DateTimeOffset Timestamp,
            string Category,
            string Action,
            object? Payload);
    }
}
