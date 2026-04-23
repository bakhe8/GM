using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using GuaranteeManager.Contracts;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Themes.Base;
using GuaranteeManager.ViewModels;

namespace GuaranteeManager
{
    public partial class MainWindow : Window
    {
        private const int MonitorDefaultToNearest = 2;
        private const int WmGetMinMaxInfo = 0x0024;
        private const double ExpandedSidebarWidth = 232;
        private const double CompactSidebarWidth = 84;
        private const double CompactSidebarThreshold = 1320;

        private readonly ShellViewModel _shellViewModel;
        private readonly SecondaryWindowManager _windowManager = SecondaryWindowManager.Instance;

        public FrameworkElement ContentOverlayAnchor => ShellContentOverlayAnchor;

        public MainWindow(ShellViewModel shellViewModel)
        {
            InitializeComponent();
            _shellViewModel = shellViewModel;
            DataContext = _shellViewModel;
            _shellViewModel.PropertyChanged += ShellViewModel_PropertyChanged;

            WindowStateService.Restore(this, nameof(MainWindow));
            Closing += MainWindow_Closing;
            StateChanged += (_, _) => UpdateMaximizeButtonGlyph();
            Loaded += MainWindow_Loaded;
            SourceInitialized += MainWindow_SourceInitialized;

            ApplyShellVisualState();
            UpdateMaximizeButtonGlyph();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            EnsureWindowFitsVisibleWorkArea();
            ApplyResponsiveShellLayout();
            WarmUpViews();
        }

        private void WarmUpViews()
        {
            // Pre-initialize main views in the background to avoid the "first click delay"
            Dispatcher.InvokeAsync(() =>
            {
                _shellViewModel.WarmUpViews();
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        public void ShowTodayDesk(bool refreshExisting = false)
        {
            _shellViewModel.ShowTodayDesk(refreshExisting);
        }

        public void ShowDataTable(bool refreshExisting = false)
        {
            _shellViewModel.ShowDataTable(refreshExisting);
        }

        public void ShowOperationCenter(bool refreshExisting = false, int? requestIdToFocus = null)
        {
            _shellViewModel.ShowOperationCenter(refreshExisting, requestIdToFocus);
        }

        public void ShowSettings(bool refreshExisting = false)
        {
            _shellViewModel.ShowSettings(refreshExisting);
        }

        public void ShowAddEntryScreen(bool resetExisting = false)
        {
            _shellViewModel.ShowAddEntryScreen(resetExisting);
        }

        public void ShowAddEntry(GuaranteeFormReturnTarget returnTarget = GuaranteeFormReturnTarget.DataTable)
        {
            _shellViewModel.ShowAddEntry(returnTarget);
        }

        public void ShowEditGuarantee(Guarantee guarantee, GuaranteeFormReturnTarget returnTarget = GuaranteeFormReturnTarget.DataTable)
        {
            _shellViewModel.ShowEditGuarantee(guarantee, returnTarget);
        }

        public void ShowGuaranteeFile(
            Guarantee guarantee,
            string? sourceLabel = null,
            bool refreshExisting = false,
            GuaranteeFileFocusArea focusArea = GuaranteeFileFocusArea.None,
            int? requestIdToFocus = null)
        {
            _shellViewModel.ShowGuaranteeFile(guarantee, sourceLabel, refreshExisting, focusArea, requestIdToFocus);
        }

        public void ShowSideSheet(string title, string subtitle, UIElement content)
        {
            TxtSideSheetTitle.Text = title;
            TxtSideSheetSubtitle.Text = subtitle;
            SideSheetContentHost.Content = content;
            ShellContentOverlayAnchor.Visibility = Visibility.Visible;
        }

        public void CloseSideSheet()
        {
            SideSheetContentHost.Content = null;
            ShellContentOverlayAnchor.Visibility = Visibility.Collapsed;
        }

        public void SetStatus(string message, ShellStatusTone tone = ShellStatusTone.Info)
        {
            _shellViewModel.SetStatus(message, tone);
        }

        private bool CanNavigateAway()
        {
            return _shellViewModel.CanNavigateAway();
        }

        private void SetActiveNavigation(string? navKey)
        {
            ApplyNavigationState(BtnNavToday, navKey == "Today");
            ApplyNavigationState(BtnNavReception, navKey == "Reception");
            ApplyNavigationState(BtnNavPortfolio, navKey == "Portfolio");
            ApplyNavigationState(BtnNavBankRoom, navKey == "BankRoom");
            ApplyNavigationState(BtnNavAdministration, navKey == "Administration");
        }

        private void ApplyNavigationState(Button button, bool isActive)
        {
            Nav.SetIsActive(button, isActive);
        }

        private void ApplyStatusTone(ShellStatusTone tone)
        {
            Brush brush = tone switch
            {
                ShellStatusTone.Success => (Brush)FindResource("Status_Success"),
                ShellStatusTone.Warning => (Brush)FindResource("Status_Warning"),
                ShellStatusTone.Error => (Brush)FindResource("Status_Error"),
                _ => (Brush)FindResource("Status_Info")
            };

            string label = tone switch
            {
                ShellStatusTone.Success => "نجاح",
                ShellStatusTone.Warning => "تنبيه",
                ShellStatusTone.Error => "خطأ",
                _ => "معلومة"
            };

            StatusToneDot.Fill = brush;
            TxtStatusTone.Foreground = brush;
            TxtStatusTone.Text = label;
        }

        private void UpdateMaximizeButtonGlyph()
        {
            if (BtnMaximizeRestoreWindow.Content is TextBlock glyph)
            {
                glyph.Text = WindowState == WindowState.Maximized ? "" : "";
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!CanNavigateAway())
            {
                e.Cancel = true;
                return;
            }

            _windowManager.CloseAll();
            WindowStateService.Save(this, nameof(MainWindow));
        }

        public void OpenAttachmentWindow(
            string windowKey,
            Func<Window> factory,
            string openedStatusMessage,
            string activatedStatusMessage,
            Action<Window>? onExisting = null)
        {
            bool opened = _windowManager.ShowOrActivate(
                windowKey,
                factory,
                onExisting);

            SetStatus(opened ? openedStatusMessage : activatedStatusMessage, ShellStatusTone.Info);
        }

        private void TxtUnifiedSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_shellViewModel.SearchCommand.CanExecute(null))
                {
                    _shellViewModel.SearchCommand.Execute(null);
                }

                e.Handled = true;
            }
        }

        private void CloseSideSheet_Click(object sender, RoutedEventArgs e)
        {
            CloseSideSheet();
        }

        private void SideSheetScrim_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CloseSideSheet();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximizeRestore();
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreWindow_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximizeRestore();
        }

        private void ToggleMaximizeRestore()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

            UpdateMaximizeButtonGlyph();
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && ShellContentOverlayAnchor.Visibility == Visibility.Visible)
            {
                CloseSideSheet();
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                TxtUnifiedSearch.Focus();
                TxtUnifiedSearch.SelectAll();
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
            {
                _shellViewModel.ShowAddEntryScreen(true);
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
            {
                if (MainContentArea.Content is ISaveShortcutTarget saveTarget)
                {
                    if (saveTarget.CanExecuteSave)
                    {
                        saveTarget.ExecuteSaveShortcut();
                    }
                    else
                    {
                        SetStatus(saveTarget.GetSaveShortcutUnavailableReason(), ShellStatusTone.Warning);
                    }

                    e.Handled = true;
                }
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveShellLayout();
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                source.AddHook(WindowProc);
            }

            EnsureWindowFitsVisibleWorkArea();
            ApplyResponsiveShellLayout();
        }

        private void ApplyResponsiveShellLayout()
        {
            bool useCompactSidebar = ActualWidth > 0 && ActualWidth < CompactSidebarThreshold;
            double sidebarWidth = useCompactSidebar ? CompactSidebarWidth : ExpandedSidebarWidth;

            HeaderSidebarColumn.Width = new GridLength(sidebarWidth);
            WorkspaceSidebarColumn.Width = new GridLength(sidebarWidth);

            Visibility labelVisibility = useCompactSidebar ? Visibility.Collapsed : Visibility.Visible;
            TxtNavigationSection.Visibility = labelVisibility;
            TxtQuickAccessSection.Visibility = labelVisibility;

            ConfigureSidebarButton(BtnNavToday, TxtNavTodayLabel, useCompactSidebar);
            ConfigureSidebarButton(BtnNavReception, TxtNavReceptionLabel, useCompactSidebar);
            ConfigureSidebarButton(BtnNavPortfolio, TxtNavPortfolioLabel, useCompactSidebar);
            ConfigureSidebarButton(BtnNavBankRoom, TxtNavBankRoomLabel, useCompactSidebar);
            ConfigureSidebarButton(BtnResumeLastFileSide, TxtResumeLastFileSideLabel, useCompactSidebar);
            ConfigureSidebarButton(BtnNavAdministration, TxtNavAdministrationLabel, useCompactSidebar);
        }

        private static void ConfigureSidebarButton(Button button, TextBlock label, bool useCompactSidebar)
        {
            label.Visibility = useCompactSidebar ? Visibility.Collapsed : Visibility.Visible;
            button.Padding = useCompactSidebar ? new Thickness(16, 0, 16, 0) : new Thickness(24, 0, 24, 0);
            button.HorizontalContentAlignment = useCompactSidebar ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        }

        private void ShellViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ShellViewModel.CurrentContent):
                    CloseSideSheet();
                    break;
                case nameof(ShellViewModel.ActiveNavigationKey):
                    SetActiveNavigation(_shellViewModel.ActiveNavigationKey);
                    break;
                case nameof(ShellViewModel.StatusTone):
                    ApplyStatusTone(_shellViewModel.StatusTone);
                    break;
            }
        }

        private void ApplyShellVisualState()
        {
            SetActiveNavigation(_shellViewModel.ActiveNavigationKey);
            ApplyStatusTone(_shellViewModel.StatusTone);
        }

        private void EnsureWindowFitsVisibleWorkArea()
        {
            if (WindowState != WindowState.Normal)
            {
                return;
            }

            Rect workArea = GetNearestWorkArea();
            double safeMinWidth = Math.Min(MinWidth, workArea.Width);
            double safeMinHeight = Math.Min(MinHeight, workArea.Height);

            Width = Clamp(Width, safeMinWidth, workArea.Width);
            Height = Clamp(Height, safeMinHeight, workArea.Height);
            Left = Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width));
            Top = Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height));
        }

        private Rect GetNearestWorkArea()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return SystemParameters.WorkArea;
            }

            IntPtr monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero)
            {
                return SystemParameters.WorkArea;
            }

            MONITORINFO monitorInfo = new()
            {
                cbSize = Marshal.SizeOf<MONITORINFO>()
            };

            if (!GetMonitorInfo(monitor, ref monitorInfo))
            {
                return SystemParameters.WorkArea;
            }

            RECT workArea = monitorInfo.rcWork;
            return new Rect(
                workArea.left,
                workArea.top,
                Math.Max(0, workArea.right - workArea.left),
                Math.Max(0, workArea.bottom - workArea.top));
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (maximum < minimum)
            {
                return minimum;
            }

            return Math.Max(minimum, Math.Min(maximum, value));
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
