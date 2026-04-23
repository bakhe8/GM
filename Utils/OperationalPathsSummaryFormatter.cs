using System.Text;

namespace GuaranteeManager.Utils
{
    public static class OperationalPathsSummaryFormatter
    {
        public static string Build(
            string storageRootPath,
            string baseDirectoryPath,
            string databasePath,
            string attachmentsPath,
            string workflowPath,
            string logsPath)
        {
            StringBuilder summary = new();
            summary.AppendLine("GuaranteeManager - ملخص مسارات التشغيل");
            summary.AppendLine($"جذر التخزين الفعلي: {storageRootPath}");
            summary.AppendLine($"مجلد التنفيذ الحالي: {baseDirectoryPath}");
            summary.AppendLine($"قاعدة البيانات: {databasePath}");
            summary.AppendLine($"المرفقات: {attachmentsPath}");
            summary.AppendLine($"الطلبات والردود: {workflowPath}");
            summary.Append($"السجلات: {logsPath}");
            return summary.ToString();
        }
    }
}
