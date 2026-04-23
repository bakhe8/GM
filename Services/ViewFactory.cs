using GuaranteeManager.Contracts;
using GuaranteeManager.Models;
using GuaranteeManager.Views;

#if DEBUG
using GuaranteeManager.Development;
#endif

namespace GuaranteeManager.Services
{
    public class ViewFactory : IViewFactory
    {
        private readonly IDatabaseService _dbService;
        private readonly IWorkflowService _workflowService;
        private readonly IExcelService _excelService;
        private readonly IOperationalInquiryService _inquiryService;
        private readonly IContextActionService _contextActionService;
        private readonly BackupService _backupService;
#if DEBUG
        private readonly DataSeedingService _seedingService;
#endif

        public ViewFactory(
            IDatabaseService dbService,
            IWorkflowService workflowService,
            IExcelService excelService,
            IOperationalInquiryService inquiryService,
            IContextActionService contextActionService,
            BackupService backupService
#if DEBUG
            , DataSeedingService seedingService
#endif
            )
        {
            _dbService = dbService;
            _workflowService = workflowService;
            _excelService = excelService;
            _inquiryService = inquiryService;
            _contextActionService = contextActionService;
            _backupService = backupService;
#if DEBUG
            _seedingService = seedingService;
#endif
        }

        public IRefreshableView CreateTodayDesk() =>
            new TodayDeskView(_dbService, _workflowService, _excelService, _inquiryService, _contextActionService);

        public IShellSearchableView CreateDataTable() =>
            new DataTableView(_dbService, _workflowService, _excelService, _inquiryService, _contextActionService);

        public IOperationCenterWorkspace CreateOperationCenter() =>
            new OperationCenterView(_dbService, _workflowService, _excelService, _contextActionService);

        public IRefreshableView CreateSettings() =>
            new SettingsView(_dbService, _backupService
#if DEBUG
                , _seedingService
#endif
                );

        public object CreateAddEntry(
            Guarantee? editGuarantee = null,
            GuaranteeFormReturnTarget returnTarget = GuaranteeFormReturnTarget.DataTable) =>
            new AddEntryView(_dbService, _excelService, editGuarantee, returnTarget);

        public IGuaranteeFileWorkspace CreateGuaranteeFile() =>
            new GuaranteeFileView(_dbService, _workflowService, _excelService, _inquiryService, _contextActionService);
    }
}
