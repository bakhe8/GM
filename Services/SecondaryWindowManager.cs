using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace GuaranteeManager.Services
{
    public sealed class SecondaryWindowManager
    {
        private readonly Dictionary<string, Window> _openWindows = new Dictionary<string, Window>();

        public static SecondaryWindowManager Instance { get; } = new SecondaryWindowManager();

        private SecondaryWindowManager()
        {
        }

        public bool ShowOrActivate(string key, Func<Window> factory, Action<Window>? onExisting = null)
        {
            if (_openWindows.TryGetValue(key, out Window? existingWindow))
            {
                if (existingWindow.IsLoaded)
                {
                    onExisting?.Invoke(existingWindow);
                    BringToFront(existingWindow);
                    return false;
                }

                _openWindows.Remove(key);
            }

            Window window = factory();
            AttachOwnerIfMissing(window);
            _openWindows[key] = window;
            window.Closed += (_, _) =>
            {
                if (_openWindows.TryGetValue(key, out Window? currentWindow) && ReferenceEquals(currentWindow, window))
                {
                    _openWindows.Remove(key);
                }
            };

            window.Show();
            BringToFront(window);
            return true;
        }

        public void CloseAll()
        {
            foreach (Window window in _openWindows.Values.Distinct().ToList())
            {
                try
                {
                    if (window.IsLoaded)
                    {
                        window.Close();
                    }
                }
                catch (Exception ex)
                {
                    SimpleLogger.LogError(ex, $"SecondaryWindowManager.CloseAll ({window.GetType().Name})");
                }
            }

            _openWindows.Clear();
        }

        private static void AttachOwnerIfMissing(Window window)
        {
            if (window.Owner != null)
            {
                return;
            }

            if (Application.Current?.MainWindow is not Window mainWindow)
            {
                return;
            }

            if (ReferenceEquals(window, mainWindow))
            {
                return;
            }

            try
            {
                window.Owner = mainWindow;
            }
            catch (InvalidOperationException ex)
            {
                SimpleLogger.LogError(ex, $"SecondaryWindowManager.AttachOwnerIfMissing ({window.GetType().Name})");
            }
        }

        private static void BringToFront(Window window)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
            window.Topmost = true;
            window.Topmost = false;
            window.Focus();
        }
    }
}
