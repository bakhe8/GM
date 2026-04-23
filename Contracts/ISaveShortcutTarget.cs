namespace GuaranteeManager.Contracts
{
    public interface ISaveShortcutTarget
    {
        bool CanExecuteSave { get; }
        string GetSaveShortcutUnavailableReason();
        void ExecuteSaveShortcut();
    }
}
