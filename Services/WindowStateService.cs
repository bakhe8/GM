using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using GuaranteeManager.Utils;

namespace GuaranteeManager
{
    public static class WindowStateService
    {
        private static readonly Lock SyncLock = new();
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private static string StateFilePath => Path.Combine(AppPaths.DataFolder, "window-state.json");

        public static void Restore(Window window, string key)
        {
            try
            {
                if (!TryGetState(key, out WindowStateRecord? state) || state == null)
                {
                    return;
                }

                window.WindowStartupLocation = WindowStartupLocation.Manual;
                if (window.ResizeMode != ResizeMode.NoResize && state.Width > 200 && state.Height > 120)
                {
                    window.Width = state.Width;
                    window.Height = state.Height;
                }

                window.Left = state.Left;
                window.Top = state.Top;
                EnsureVisible(window);

                if (state.IsMaximized && window.ResizeMode != ResizeMode.NoResize)
                {
                    window.WindowState = WindowState.Maximized;
                }
            }
            catch
            {
                // تجاهل مشاكل الاستعادة حتى لا تؤثر على فتح النافذة.
            }
        }

        public static void Save(Window window, string key)
        {
            try
            {
                Rect bounds = window.WindowState == WindowState.Normal
                    ? new Rect(window.Left, window.Top, window.Width, window.Height)
                    : window.RestoreBounds;

                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    return;
                }

                Dictionary<string, WindowStateRecord> states = LoadStates();
                states[key] = new WindowStateRecord(
                    bounds.Left,
                    bounds.Top,
                    bounds.Width,
                    bounds.Height,
                    window.WindowState == WindowState.Maximized);

                SaveStates(states);
            }
            catch
            {
                // تجاهل مشاكل الحفظ حتى لا تؤثر على إغلاق النافذة.
            }
        }

        private static bool TryGetState(string key, out WindowStateRecord? state)
        {
            Dictionary<string, WindowStateRecord> states = LoadStates();
            return states.TryGetValue(key, out state);
        }

        private static Dictionary<string, WindowStateRecord> LoadStates()
        {
            lock (SyncLock)
            {
                AppPaths.EnsureDirectoriesExist();
                if (!File.Exists(StateFilePath))
                {
                    return new Dictionary<string, WindowStateRecord>(StringComparer.OrdinalIgnoreCase);
                }

                using FileStream stream = File.OpenRead(StateFilePath);
                return JsonSerializer.Deserialize<Dictionary<string, WindowStateRecord>>(stream)
                    ?? new Dictionary<string, WindowStateRecord>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void SaveStates(Dictionary<string, WindowStateRecord> states)
        {
            lock (SyncLock)
            {
                AppPaths.EnsureDirectoriesExist();
                using FileStream stream = File.Create(StateFilePath);
                JsonSerializer.Serialize(stream, states, JsonOptions);
            }
        }

        private static void EnsureVisible(Window window)
        {
            Rect virtualBounds = new(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);

            Rect currentBounds = new(window.Left, window.Top, window.Width, window.Height);
            if (!virtualBounds.IntersectsWith(currentBounds))
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                return;
            }

            double maxLeft = Math.Max(virtualBounds.Left, virtualBounds.Right - window.Width);
            double maxTop = Math.Max(virtualBounds.Top, virtualBounds.Bottom - window.Height);
            window.Left = Math.Max(virtualBounds.Left, Math.Min(window.Left, maxLeft));
            window.Top = Math.Max(virtualBounds.Top, Math.Min(window.Top, maxTop));
        }

        private sealed record WindowStateRecord(
            double Left,
            double Top,
            double Width,
            double Height,
            bool IsMaximized);
    }
}
