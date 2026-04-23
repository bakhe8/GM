using System.IO;
using GuaranteeManager.Models;

namespace GuaranteeManager.Utils
{
    public static class ExternalOpenFeedbackFormatter
    {
        public static string BuildOpenedFileStatusOrFallback(string itemLabel, string? fullPath, string? displayName = null)
        {
            if (string.IsNullOrWhiteSpace(fullPath) && string.IsNullOrWhiteSpace(displayName))
            {
                return $"تم فتح {itemLabel} خارج البرنامج في التطبيق المرتبط.";
            }

            string resolvedName = !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : Path.GetFileName(fullPath) ?? itemLabel;

            return $"تم فتح {itemLabel} خارج البرنامج في التطبيق المرتبط: {resolvedName}";
        }

        public static string BuildOpenedFolderStatusOrFallback(string folderLabel, string? folderPath)
        {
            return string.IsNullOrWhiteSpace(folderPath)
                ? $"تم فتح {folderLabel} خارج البرنامج في مستكشف الملفات."
                : $"تم فتح {folderLabel} خارج البرنامج في مستكشف الملفات: {folderPath}";
        }

        public static string BuildOpenedRequestLetterStatus(WorkflowRequest request)
        {
            return BuildOpenedFileStatusOrFallback(
                $"خطاب الطلب رقم {request.SequenceNumber}",
                request.LetterFilePath,
                request.LetterOriginalFileName);
        }

        public static string BuildOpenedResponseDocumentStatus(WorkflowRequest request)
        {
            return BuildOpenedFileStatusOrFallback(
                $"مستند رد البنك للطلب رقم {request.SequenceNumber}",
                request.ResponseFilePath,
                request.ResponseOriginalFileName);
        }
    }
}
