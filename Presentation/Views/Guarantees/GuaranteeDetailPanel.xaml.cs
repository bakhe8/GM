using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using GuaranteeManager.Models;

namespace GuaranteeManager
{
    public partial class GuaranteeDetailPanel : UserControl
    {
        public static readonly DependencyProperty IsDetachedFileProperty =
            DependencyProperty.Register(
                nameof(IsDetachedFile),
                typeof(bool),
                typeof(GuaranteeDetailPanel),
                new PropertyMetadata(false));

        private ShellViewModel? _shellViewModel;
        private int _lastAppliedFocusRequestVersion;

        public bool IsDetachedFile
        {
            get => (bool)GetValue(IsDetachedFileProperty);
            private set => SetValue(IsDetachedFileProperty, value);
        }

        public GuaranteeDetailPanel()
        {
            InitializeComponent();
            DataContextChanged += GuaranteeDetailPanel_DataContextChanged;
            Loaded += GuaranteeDetailPanel_Loaded;
            Unloaded += GuaranteeDetailPanel_Unloaded;
        }

        private void GuaranteeDetailPanel_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_shellViewModel != null)
            {
                _shellViewModel.GuaranteeFocusRequested -= OnGuaranteeFocusRequested;
            }

            _shellViewModel = e.NewValue as ShellViewModel;
            if (_shellViewModel != null)
            {
                _shellViewModel.GuaranteeFocusRequested += OnGuaranteeFocusRequested;
                TryApplyPendingOrCurrentFocus();
            }
        }

        private void GuaranteeDetailPanel_Loaded(object sender, RoutedEventArgs e)
        {
            IsDetachedFile = Window.GetWindow(this) is Window window && window is not MainWindow;
            TryApplyPendingOrCurrentFocus();
        }

        private void GuaranteeDetailPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_shellViewModel != null)
            {
                _shellViewModel.GuaranteeFocusRequested -= OnGuaranteeFocusRequested;
            }
        }

        private void OnGuaranteeFocusRequested(GuaranteeFileFocusArea area, int? requestIdToFocus)
        {
            if (_shellViewModel != null)
            {
                _lastAppliedFocusRequestVersion = _shellViewModel.GuaranteeFocusRequestVersion;
            }

            ApplyFocus(area, requestIdToFocus);
        }

        private void ClosePanelButton_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is Window window && window is not MainWindow)
            {
                window.Close();
                e.Handled = true;
            }
        }

        private void ScrollToArea(GuaranteeFileFocusArea area)
        {
            FrameworkElement? target = area switch
            {
                GuaranteeFileFocusArea.ExecutiveSummary => ExecutiveSummaryAnchor,
                GuaranteeFileFocusArea.Requests => RequestsAnchor,
                GuaranteeFileFocusArea.Series => TimelineAnchor,
                GuaranteeFileFocusArea.Attachments => AttachmentsAnchor,
                GuaranteeFileFocusArea.Actions => ActionsAnchor,
                GuaranteeFileFocusArea.Outputs => OutputsAnchor,
                _ => ExecutiveSummaryAnchor
            };

            if (target == null || RootScrollViewer.Content is not Visual content)
            {
                return;
            }

            try
            {
                Point point = target.TransformToAncestor(content).Transform(new Point(0, 0));
                RootScrollViewer.ScrollToVerticalOffset(Math.Max(0, point.Y - 10));
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void TryApplyPendingOrCurrentFocus()
        {
            if (_shellViewModel?.SelectedGuarantee == null)
            {
                return;
            }

            if (_shellViewModel.TryConsumePendingGuaranteeFileOpenFocus(
                    _shellViewModel.SelectedGuarantee.RootId,
                    out GuaranteeFileFocusArea pendingArea,
                    out int? pendingRequestId))
            {
                _lastAppliedFocusRequestVersion = _shellViewModel.GuaranteeFocusRequestVersion;
                ApplyFocus(pendingArea, pendingRequestId);
                return;
            }

            if (_shellViewModel.GuaranteeFocusRequestVersion > _lastAppliedFocusRequestVersion)
            {
                _lastAppliedFocusRequestVersion = _shellViewModel.GuaranteeFocusRequestVersion;
                ApplyFocus(_shellViewModel.CurrentGuaranteeFocusArea, _shellViewModel.CurrentGuaranteeFocusRequestId);
            }
        }

        private void ApplyFocus(GuaranteeFileFocusArea area, int? requestIdToFocus)
        {
            _ = requestIdToFocus;
            Dispatcher.BeginInvoke(() => ScrollToArea(area), DispatcherPriority.Loaded);
        }
    }
}
