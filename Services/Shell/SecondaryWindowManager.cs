using System;
using System.Collections.Generic;
using System.Windows;

namespace GuaranteeManager.Services
{
    public sealed class SecondaryWindowManager
    {
        private readonly IAppDialogService _dialogs;
        private readonly IUiDiagnosticsService _diagnostics;
        private readonly HashSet<string> _activeKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _gate = new();

        public SecondaryWindowManager(IAppDialogService dialogs, IUiDiagnosticsService diagnostics)
        {
            _dialogs = dialogs;
            _diagnostics = diagnostics;
        }

        public bool? ShowDialog(string windowKey, Func<Window> windowFactory, string title, string duplicateMessage)
        {
            if (!TryEnter(windowKey))
            {
                _diagnostics.RecordEvent(
                    "dialog.secondary",
                    "duplicate-blocked",
                    new
                    {
                        WindowKey = windowKey,
                        Title = title
                    });
                _dialogs.ShowInformation(duplicateMessage, title);
                return null;
            }

            Window window = windowFactory();
            if (window.Owner == null && Application.Current?.MainWindow != window)
            {
                window.Owner = Application.Current?.MainWindow;
            }

            try
            {
                _diagnostics.RecordEvent(
                    "dialog.secondary",
                    "open",
                    new
                    {
                        WindowKey = windowKey,
                        Title = window.Title,
                        OwnerTitle = window.Owner?.Title ?? string.Empty
                    });

                bool? result = window.ShowDialog();
                _diagnostics.RecordEvent(
                    "dialog.secondary",
                    "close",
                    new
                    {
                        WindowKey = windowKey,
                        Title = window.Title,
                        Result = result
                    });
                return result;
            }
            finally
            {
                Exit(windowKey);
            }
        }

        private bool TryEnter(string windowKey)
        {
            lock (_gate)
            {
                return _activeKeys.Add(windowKey);
            }
        }

        private void Exit(string windowKey)
        {
            lock (_gate)
            {
                _activeKeys.Remove(windowKey);
            }
        }
    }
}
