using System.ComponentModel;

namespace GuaranteeManager.Services
{
    public interface IShellStatusService : INotifyPropertyChanged
    {
        string PrimaryText { get; }
        string SecondaryText { get; }
        ShellStatusTone Tone { get; }

        void ResetToReady();
        void ShowInfo(string primaryText, string? secondaryText = null, int autoResetMilliseconds = 4500);
        void ShowSuccess(string primaryText, string? secondaryText = null, int autoResetMilliseconds = 4500);
        void ShowWarning(string primaryText, string? secondaryText = null, int autoResetMilliseconds = 6000);
        void ShowError(string primaryText, string? secondaryText = null, int autoResetMilliseconds = 7000);
    }
}
