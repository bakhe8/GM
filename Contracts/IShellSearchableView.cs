namespace GuaranteeManager.Contracts
{
    public interface IShellSearchableView : IRefreshableView
    {
        void ApplyShellSearch(string query);
    }
}
