using System.Collections.Generic;
using System.Windows.Media;
using GuaranteeManager.Utils;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class ReportsWorkspaceDataServiceTests
    {
        [Fact]
        public void BuildDetailState_WhenReportIsRunning_DisablesRunAndOpenActions()
        {
            var service = new ReportsWorkspaceDataService();
            ReportWorkspaceItem item = CreateItem();

            ReportsWorkspaceDetailState state = service.BuildDetailState(
                item,
                new Dictionary<string, ReportRunResult>(),
                canOpen: true,
                isSelectedRunning: true,
                isAnyReportRunning: true);

            Assert.Equal("قيد الإنشاء", state.BadgeText);
            Assert.False(state.CanRun);
            Assert.False(state.CanOpen);
            Assert.Contains("الخلفية", state.Status);
        }

        [Fact]
        public void BuildRowState_WhenReportIsRunning_ShowsRunningStatus()
        {
            var service = new ReportsWorkspaceDataService();

            ReportWorkspaceRowState state = service.BuildRowState(
                CreateItem(),
                new Dictionary<string, ReportRunResult>(),
                canOpen: true,
                isRunning: true);

            Assert.Equal("قيد الإنشاء", state.StatusLabel);
            Assert.False(state.CanOpen);
        }

        private static ReportWorkspaceItem CreateItem()
            => new(
                "portfolio.all",
                "المحفظة الكاملة",
                "تصدير قائمة الضمانات.",
                "تقرير محفظة",
                ReportWorkspaceItem.PortfolioFilterLabel,
                WorkspaceSurfaceChrome.BrushFrom("#2563EB"));
    }
}
