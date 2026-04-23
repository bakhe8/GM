using GuaranteeManager.Models;
namespace GuaranteeManager.Contracts
{
    public interface IViewFactory
    {
        IRefreshableView CreateTodayDesk();
        IShellSearchableView CreateDataTable();
        IOperationCenterWorkspace CreateOperationCenter();
        IRefreshableView CreateSettings();
        object CreateAddEntry(Guarantee? editGuarantee = null, GuaranteeFormReturnTarget returnTarget = GuaranteeFormReturnTarget.DataTable);
        IGuaranteeFileWorkspace CreateGuaranteeFile();
    }
}
