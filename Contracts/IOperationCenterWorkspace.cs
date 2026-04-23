namespace GuaranteeManager.Contracts
{
    public interface IOperationCenterWorkspace : IShellSearchableView
    {
        void SetRequestFocus(int? requestId);
    }
}
