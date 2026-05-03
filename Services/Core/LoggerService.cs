using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    public static class SimpleLogger
    {
        private const long MaxLogFileSizeBytes = 10 * 1024 * 1024; // 10 MB
        private static readonly Lock FileLock = new();

        public static IDisposable BeginScope(string operationName)
        {
            return OperationLogScope.Begin(operationName);
        }

        public static void Log(
            string message,
            string level = "INFO",
            [CallerMemberName] string caller = "")
        {
            WriteEntry(level, message, caller);
        }

        public static void LogError(
            Exception ex,
            string context = "",
            [CallerMemberName] string caller = "")
        {
            string message = string.IsNullOrWhiteSpace(context)
                ? ex.ToString()
                : $"[{context}] {ex}";
            WriteEntry("ERROR", message, caller);
        }

        public static void LogAudit(string action, string entity, string detail = "")
        {
            try
            {
                string auditPath = GetAuditLogPath();
                string diagnosticPrefix = BuildDiagnosticPrefix();
                string entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [AUDIT]{diagnosticPrefix} Action={action} | Entity={entity}{(string.IsNullOrWhiteSpace(detail) ? "" : " | " + detail)}{Environment.NewLine}";

                lock (FileLock)
                {
                    File.AppendAllText(auditPath, entry);
                }
            }
            catch (Exception inner)
            {
                Trace.WriteLine($"[SimpleLogger AUDIT FAULT] {inner.Message}");
            }
        }

        private static string GetAuditLogPath()
        {
            if (!Directory.Exists(AppPaths.LogsFolder))
            {
                Directory.CreateDirectory(AppPaths.LogsFolder);
            }

            return Path.Combine(AppPaths.LogsFolder, $"audit_{DateTime.Now:yyyyMM}.txt");
        }

        private static void WriteEntry(string level, string message, string caller)
        {
            try
            {
                string logPath = GetCurrentLogPath();
                string diagnosticPrefix = BuildDiagnosticPrefix();
                string entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level,-5}] [{caller}]{diagnosticPrefix} {message}{Environment.NewLine}";

                lock (FileLock)
                {
                    RotateIfNeeded(logPath);
                    File.AppendAllText(logPath, entry);
                }
            }
            catch (Exception inner)
            {
                // اللجوء لـ Trace إذا فشل الكتابة للملف
                Trace.WriteLine($"[SimpleLogger FAULT] {inner.Message} | Original: {message}");
            }
        }

        private static string BuildDiagnosticPrefix()
        {
            string? operationId = OperationLogScope.CurrentOperationId;
            string scopePath = OperationLogScope.CurrentScopePath;

            string operationSegment = string.IsNullOrWhiteSpace(operationId)
                ? string.Empty
                : $" [Op={operationId}]";
            string scopeSegment = string.IsNullOrWhiteSpace(scopePath)
                ? string.Empty
                : $" [Scope={scopePath}]";

            return operationSegment + scopeSegment;
        }

        private static string GetCurrentLogPath()
        {
            if (!Directory.Exists(AppPaths.LogsFolder))
            {
                Directory.CreateDirectory(AppPaths.LogsFolder);
            }

            return Path.Combine(AppPaths.LogsFolder, $"log_{DateTime.Now:yyyyMMdd}.txt");
        }

        private static void RotateIfNeeded(string logPath)
        {
            if (!File.Exists(logPath))
            {
                return;
            }

            if (new FileInfo(logPath).Length < MaxLogFileSizeBytes)
            {
                return;
            }

            string rotated = Path.ChangeExtension(logPath, null) + $"_rotated_{DateTime.Now:HHmmss}.txt";
            try
            {
                File.Move(logPath, rotated, overwrite: false);
            }
            catch
            {
                // إذا فشل الـ rotation نكمل الكتابة في نفس الملف
            }
        }
    }
}
