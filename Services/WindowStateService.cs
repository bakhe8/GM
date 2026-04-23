using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    public static class WindowStateService
    {
        private static string StateFilePath => Path.Combine(AppPaths.DataFolder, "window-state.json");

        public static void Restore(Window window, string key)
        {
            try
            {
                Dictionary<string, WindowStateInfo> states = LoadStates();
                if (!states.TryGetValue(key, out WindowStateInfo? state))
                {
                    return;
                }

                Rect workArea = SystemParameters.WorkArea;
                double safeMinWidth = Math.Min(window.MinWidth, workArea.Width);
                double safeMinHeight = Math.Min(window.MinHeight, workArea.Height);
                double restoredWidth = IsFinite(state.Width) && state.Width > 0
                    ? Clamp(state.Width, safeMinWidth, workArea.Width)
                    : window.Width;
                double restoredHeight = IsFinite(state.Height) && state.Height > 0
                    ? Clamp(state.Height, safeMinHeight, workArea.Height)
                    : window.Height;

                if (IsFinite(restoredWidth) && restoredWidth > 0)
                {
                    window.Width = restoredWidth;
                }

                if (IsFinite(restoredHeight) && restoredHeight > 0)
                {
                    window.Height = restoredHeight;
                }

                if (IsFinite(state.Left) && IsFinite(state.Top))
                {
                    window.Left = Clamp(state.Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - restoredWidth));
                    window.Top = Clamp(state.Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - restoredHeight));
                }

                if (Enum.TryParse(state.WindowState, out WindowState savedState) && savedState == WindowState.Maximized)
                {
                    window.WindowState = WindowState.Maximized;
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, $"RestoreWindowState ({key})");
            }
        }

        public static void Save(Window window, string key)
        {
            try
            {
                AppPaths.EnsureDirectoriesExist();
                Dictionary<string, WindowStateInfo> states = LoadStates();
                Rect bounds = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;

                states[key] = new WindowStateInfo
                {
                    Left = bounds.Left,
                    Top = bounds.Top,
                    Width = bounds.Width,
                    Height = bounds.Height,
                    WindowState = window.WindowState == WindowState.Minimized ? WindowState.Normal.ToString() : window.WindowState.ToString()
                };

                string json = JsonSerializer.Serialize(states, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(StateFilePath, json);
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, $"SaveWindowState ({key})");
            }
        }

        private static Dictionary<string, WindowStateInfo> LoadStates()
        {
            if (!File.Exists(StateFilePath))
            {
                return new Dictionary<string, WindowStateInfo>();
            }

            string json = File.ReadAllText(StateFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, WindowStateInfo>();
            }

            return JsonSerializer.Deserialize<Dictionary<string, WindowStateInfo>>(json) ?? new Dictionary<string, WindowStateInfo>();
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (maximum < minimum)
            {
                return minimum;
            }

            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private sealed class WindowStateInfo
        {
            public double Left { get; set; }
            public double Top { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public string WindowState { get; set; } = "Normal";
        }
    }
}
