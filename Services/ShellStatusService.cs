using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace GuaranteeManager.Services
{
    public sealed class ShellStatusService : IShellStatusService
    {
        private const string ReadyPrimaryText = "جاهز";
        private const string ReadySecondaryText = "قاعدة البيانات متصلة";

        private readonly DispatcherTimer _resetTimer;
        private string _primaryText = ReadyPrimaryText;
        private string _secondaryText = ReadySecondaryText;
        private ShellStatusTone _tone = ShellStatusTone.Info;

        public ShellStatusService()
        {
            _resetTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(4500)
            };
            _resetTimer.Tick += (_, _) =>
            {
                _resetTimer.Stop();
                ResetToReady();
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string PrimaryText => _primaryText;

        public string SecondaryText => _secondaryText;

        public ShellStatusTone Tone => _tone;

        public void ResetToReady()
            => ApplyState(ReadyPrimaryText, ReadySecondaryText, ShellStatusTone.Info, autoResetMilliseconds: null);

        public void ShowInfo(string primaryText, string? secondaryText = null, int autoResetMilliseconds = 4500)
            => ApplyState(primaryText, secondaryText ?? ReadySecondaryText, ShellStatusTone.Info, autoResetMilliseconds);

        public void ShowSuccess(string primaryText, string? secondaryText = null, int autoResetMilliseconds = 4500)
            => ApplyState(primaryText, secondaryText ?? ReadySecondaryText, ShellStatusTone.Success, autoResetMilliseconds);

        public void ShowWarning(string primaryText, string? secondaryText = null, int autoResetMilliseconds = 6000)
            => ApplyState(primaryText, secondaryText ?? ReadySecondaryText, ShellStatusTone.Warning, autoResetMilliseconds);

        public void ShowError(string primaryText, string? secondaryText = null, int autoResetMilliseconds = 7000)
            => ApplyState(primaryText, secondaryText ?? ReadySecondaryText, ShellStatusTone.Error, autoResetMilliseconds);

        private void ApplyState(string primaryText, string secondaryText, ShellStatusTone tone, int? autoResetMilliseconds)
        {
            _resetTimer.Stop();

            SetProperty(ref _primaryText, string.IsNullOrWhiteSpace(primaryText) ? ReadyPrimaryText : primaryText);
            SetProperty(ref _secondaryText, string.IsNullOrWhiteSpace(secondaryText) ? ReadySecondaryText : secondaryText);
            SetProperty(ref _tone, tone);

            if (autoResetMilliseconds is > 0)
            {
                _resetTimer.Interval = TimeSpan.FromMilliseconds(autoResetMilliseconds.Value);
                _resetTimer.Start();
            }
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
