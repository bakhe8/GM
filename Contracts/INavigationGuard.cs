namespace GuaranteeManager.Contracts
{
    public interface INavigationGuard
    {
        bool HasUnsavedChanges { get; }
        bool ConfirmNavigationAway();
    }
}
