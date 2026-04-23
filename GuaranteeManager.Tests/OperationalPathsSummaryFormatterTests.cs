using GuaranteeManager.Utils;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class OperationalPathsSummaryFormatterTests
    {
        [Fact]
        public void Build_IncludesAllOperationalPaths()
        {
            string summary = OperationalPathsSummaryFormatter.Build(
                "C:\\storage",
                "C:\\app",
                "C:\\storage\\Data\\guarantees.db",
                "C:\\storage\\Attachments",
                "C:\\storage\\Workflow",
                "C:\\storage\\Logs");

            Assert.Contains("GuaranteeManager - ملخص مسارات التشغيل", summary);
            Assert.Contains("جذر التخزين الفعلي: C:\\storage", summary);
            Assert.Contains("مجلد التنفيذ الحالي: C:\\app", summary);
            Assert.Contains("قاعدة البيانات: C:\\storage\\Data\\guarantees.db", summary);
            Assert.Contains("المرفقات: C:\\storage\\Attachments", summary);
            Assert.Contains("الطلبات والردود: C:\\storage\\Workflow", summary);
            Assert.Contains("السجلات: C:\\storage\\Logs", summary);
        }
    }
}
