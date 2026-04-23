using System;
using System.IO;
using System.Linq;

namespace GuaranteeManager.Utils
{
    public static class FileHelper
    {
        public static string SanitizeFileName(string fileName, string replacement = "_")
        {
            if (string.IsNullOrEmpty(fileName)) return "unnamed_file";

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join(replacement, fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            
            // Clean up multiple underscores and trim
            sanitized = sanitized.Replace("__", "_").Trim('_');
            
            return sanitized;
        }

        /// <summary>
        /// Generates a filename using timestamp + random suffix.
        /// Requirement: Do NOT rely on GuaranteeId or GuaranteeNo.
        /// </summary>
        public static string GenerateUniqueFileName(string extension)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            string randomSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            return $"{timestamp}_{randomSuffix}{extension}";
        }
    }
}
