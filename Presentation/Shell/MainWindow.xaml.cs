using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Microsoft.Win32;

namespace GuaranteeManager
{
    public partial class MainWindow : Window
    {
        private readonly IShellStatusService _shellStatus;
        private IntPtr _hwnd;

        public MainWindow()
        {
            InitializeComponent();
            Title = "إدارة الضمانات البنكية";
            _shellStatus = App.CurrentApp.GetRequiredService<IShellStatusService>();
            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += (_, _) => WindowStateService.Restore(this, nameof(MainWindow));
            StateChanged += (_, _) => UpdateRoundedWindowRegion();
            SizeChanged += (_, _) => UpdateRoundedWindowRegion();
            Closing += (_, _) => WindowStateService.Save(this, nameof(MainWindow));
            DataContext = ShellViewModel.Create(
                App.CurrentApp.GetRequiredService<IDatabaseService>(),
                App.CurrentApp.GetRequiredService<IWorkflowService>(),
                App.CurrentApp.GetRequiredService<IExcelService>(),
                App.CurrentApp.GetRequiredService<IOperationalInquiryService>(),
                App.CurrentApp.GetRequiredService<IContextActionService>(),
                App.CurrentApp.GetRequiredService<INavigationGuard>(),
                _shellStatus,
                App.CurrentApp.GetRequiredService<IUiDiagnosticsService>());
        }

        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleWindowState();
                return;
            }

            DragMove();
        }

        private void ShellGlobalSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is ShellViewModel viewModel)
            {
                viewModel.ExecuteGlobalSearch();
                e.Handled = true;
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleWindowState();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ShellMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.ContextMenu == null)
            {
                return;
            }

            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            var menuWidth = double.IsNaN(button.ContextMenu.Width) || button.ContextMenu.Width <= 0
                ? button.ContextMenu.ActualWidth
                : button.ContextMenu.Width;
            button.ContextMenu.HorizontalOffset = button.ActualWidth - menuWidth;
            button.ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AppMessageBox.Show(
                $"إدارة الضمانات البنكية\n{AppReleaseInfo.VersionTag} | {AppReleaseInfo.RuntimeTag}",
                "حول البرنامج",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ToggleWindowState()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            ApplySystemCornerPreference(_hwnd);
            UpdateRoundedWindowRegion();
            HwndSource.FromHwnd(_hwnd)?.AddHook(WindowProc);
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmGetMinMaxInfo)
            {
                UpdateMaximizedBounds(hwnd, lParam);
                handled = true;
            }

            return IntPtr.Zero;
        }

        private static void UpdateMaximizedBounds(IntPtr hwnd, IntPtr lParam)
        {
            IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
            {
                return;
            }

            MONITORINFO monitorInfo = new()
            {
                cbSize = Marshal.SizeOf<MONITORINFO>()
            };

            if (!GetMonitorInfo(monitor, ref monitorInfo))
            {
                return;
            }

            MINMAXINFO minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            RECT workArea = monitorInfo.rcWork;
            RECT monitorArea = monitorInfo.rcMonitor;

            minMaxInfo.ptMaxPosition.x = Math.Abs(workArea.left - monitorArea.left);
            minMaxInfo.ptMaxPosition.y = Math.Abs(workArea.top - monitorArea.top);
            minMaxInfo.ptMaxSize.x = Math.Abs(workArea.right - workArea.left);
            minMaxInfo.ptMaxSize.y = Math.Abs(workArea.bottom - workArea.top);

            Marshal.StructureToPtr(minMaxInfo, lParam, true);
        }

        private static void ApplySystemCornerPreference(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                return;
            }

            int preference = DwmWindowCornerPreferenceRound;
            try
            {
                _ = DwmSetWindowAttribute(
                    hwnd,
                    DwmwaWindowCornerPreference,
                    ref preference,
                    sizeof(int));
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        private void UpdateRoundedWindowRegion()
        {
            if (_hwnd == IntPtr.Zero)
            {
                return;
            }

            if (WindowState != WindowState.Normal)
            {
                _ = SetWindowRgn(_hwnd, IntPtr.Zero, true);
                return;
            }

            if (!GetWindowRect(_hwnd, out RECT rect))
            {
                return;
            }

            int width = rect.right - rect.left;
            int height = rect.bottom - rect.top;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            double dpiScale = HwndSource.FromHwnd(_hwnd)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            int cornerDiameter = Math.Max(2, (int)Math.Round(16 * dpiScale));
            IntPtr region = CreateRoundRectRgn(0, 0, width + 1, height + 1, cornerDiameter, cornerDiameter);
            if (region == IntPtr.Zero)
            {
                return;
            }

            if (SetWindowRgn(_hwnd, region, true) == 0)
            {
                _ = DeleteObject(region);
            }
        }

        private const int WmGetMinMaxInfo = 0x0024;
        private const int MonitorDefaultToNearest = 0x00000002;
        private const int DwmwaWindowCornerPreference = 33;
        private const int DwmWindowCornerPreferenceRound = 2;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect,
            int nTopRect,
            int nRightRect,
            int nBottomRect,
            int nWidthEllipse,
            int nHeightEllipse);

        [DllImport("gdi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }
    }
}
