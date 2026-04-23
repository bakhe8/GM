using System;
using System.Collections.Generic;
using System.Linq;

namespace GuaranteeManager.Services
{
    public sealed class DeferredFilePromotionException : Exception
    {
        public DeferredFilePromotionException(
            string operationName,
            IEnumerable<string> pendingFileNames,
            Exception? innerException = null)
            : base(BuildMessage(operationName, pendingFileNames), innerException)
        {
            OperationName = operationName;
            PendingFileNames = pendingFileNames
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public string OperationName { get; }

        public IReadOnlyList<string> PendingFileNames { get; }

        public string UserMessage =>
            "تم حفظ البيانات، لكن تعذر تثبيت بعض الملفات في موقعها النهائي. " +
            "تركها النظام في منطقة الاستعادة وسيحتاج الأمر إلى مراجعة السجلات قبل إعادة تنفيذ العملية نفسها.";

        private static string BuildMessage(string operationName, IEnumerable<string> pendingFileNames)
        {
            string[] files = pendingFileNames
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            string fileSummary = files.Length == 0
                ? "No staged files were promoted."
                : string.Join(", ", files);

            return $"Deferred file promotion after {operationName}. Pending files: {fileSummary}.";
        }
    }
}
