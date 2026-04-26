using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        public MainWindow()
        {
            InitializeComponent();
            Title = "إدارة الضمانات البنكية";
            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += (_, _) => WindowStateService.Restore(this, nameof(MainWindow));
            Closing += (_, _) => WindowStateService.Save(this, nameof(MainWindow));
            DataContext = ShellViewModel.Create(
                App.CurrentApp.GetRequiredService<IDatabaseService>(),
                App.CurrentApp.GetRequiredService<IWorkflowService>(),
                App.CurrentApp.GetRequiredService<IExcelService>(),
                App.CurrentApp.GetRequiredService<IGuaranteeHistoryDocumentService>(),
                App.CurrentApp.GetRequiredService<IOperationalInquiryService>(),
                App.CurrentApp.GetRequiredService<IContextActionService>(),
                App.CurrentApp.GetRequiredService<INavigationGuard>(),
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

        private void ToggleWindowState()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook(WindowProc);
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

        private const int WmGetMinMaxInfo = 0x0024;
        private const int MonitorDefaultToNearest = 0x00000002;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

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
