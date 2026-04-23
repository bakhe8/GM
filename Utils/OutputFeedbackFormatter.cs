using System;
using System.IO;

namespace GuaranteeManager.Utils
{
    public static class OutputFeedbackFormatter
    {
        public static string BuildSavedFileSuccessMessageOrFallback(string baseMessage, string? outputPath)
        {
            return string.IsNullOrWhiteSpace(outputPath)
                ? baseMessage
                : BuildSavedFileSuccessMessage(baseMessage, outputPath);
        }

        public static string BuildSavedFileSuccessMessage(string baseMessage, string outputPath)
        {
            string fileName = Path.GetFileName(outputPath);
            return $"{baseMessage}{Environment.NewLine}اسم الملف: {fileName}{Environment.NewLine}المسار: {outputPath}";
        }

        public static string BuildSavedFileStatusOrFallback(string baseMessage, string? outputPath)
        {
            return string.IsNullOrWhiteSpace(outputPath)
                ? baseMessage
                : BuildSavedFileStatus(baseMessage, outputPath);
        }

        public static string BuildSavedFileStatus(string baseMessage, string outputPath)
        {
            string fileName = Path.GetFileName(outputPath);
            return $"{baseMessage}: {fileName}";
        }
    }
}
