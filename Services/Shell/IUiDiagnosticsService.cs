namespace GuaranteeManager.Services
{
    public interface IUiDiagnosticsService
    {
        string EventLogPath { get; }

        string ShellStatePath { get; }

        void RecordEvent(string category, string action, object? payload = null);

        void UpdateShellState(UiShellDiagnosticsState state);
    }
}
