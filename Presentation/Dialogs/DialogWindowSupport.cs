using System;
using System.Windows;
using System.Windows.Input;
using GuaranteeManager.Services;

namespace GuaranteeManager
{
    public static class DialogWindowSupport
    {
        public static void Attach(
            Window window,
            string stateKey,
            Action? saveAction = null,
            string? navigationReason = null,
            bool persistWindowState = true)
        {
            IDisposable? navigationScope = null;
            DialogChrome.ApplyWindowDefaults(window);

            if (persistWindowState)
            {
                window.Loaded += (_, _) => WindowStateService.Restore(window, stateKey);
                window.Closing += (_, _) => WindowStateService.Save(window, stateKey);
            }

            window.Loaded += (_, _) => DialogChrome.ApplyWindowDefaults(window);
            window.Loaded += (_, _) => DialogChrome.ApplyContentDefaults(window);
            window.Loaded += (_, _) => Record(window, stateKey, "loaded");
            window.Loaded += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(navigationReason))
                {
                    navigationScope = App.CurrentApp.GetRequiredService<INavigationGuard>().Block(navigationReason);
                }
            };
            window.Closed += (_, _) =>
            {
                navigationScope?.Dispose();
                Record(window, stateKey, "closed");
            };

            if (saveAction != null)
            {
                window.InputBindings.Add(new KeyBinding(
                    new RelayCommand(_ => saveAction()),
                    new KeyGesture(Key.S, ModifierKeys.Control)));
            }
        }

        private static void Record(Window window, string stateKey, string action)
        {
            App.CurrentApp.GetRequiredService<IUiDiagnosticsService>().RecordEvent(
                "dialog.window",
                action,
                new
                {
                    StateKey = stateKey,
                    Title = window.Title,
                    Width = window.ActualWidth,
                    Height = window.ActualHeight
                });
        }
    }
}
