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
        private ShellViewModel? _shellViewModel;
        private int _lastAppliedFocusRequestVersion;

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
                TryApplyCurrentFocus();
            }
        }

        private void GuaranteeDetailPanel_Loaded(object sender, RoutedEventArgs e)
        {
            TryApplyCurrentFocus();
        }

        private void GuaranteeDetailPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_shellViewModel != null)
            {
                _shellViewModel.GuaranteeFocusRequested -= OnGuaranteeFocusRequested;
            }
        }

        private void OnGuaranteeFocusRequested(GuaranteeFocusArea area, int? requestIdToFocus)
        {
            if (_shellViewModel != null)
            {
                _lastAppliedFocusRequestVersion = _shellViewModel.GuaranteeFocusRequestVersion;
            }

            _ = requestIdToFocus;
            ApplyFocus(area);
        }

        private FrameworkElement ResolveAreaElement(GuaranteeFocusArea area)
        {
            return area switch
            {
                GuaranteeFocusArea.Attachments => TimelineAnchor,
                GuaranteeFocusArea.Outputs => TimelineAnchor,
                GuaranteeFocusArea.Actions => ActionsAnchor,
                _ => TimelineAnchor
            };
        }

        private void ApplyAreaFocus(GuaranteeFocusArea area)
        {
            FrameworkElement target = ResolveAreaElement(area);
            if (area != GuaranteeFocusArea.Actions && RootScrollViewer.Content is Visual content)
            {
                try
                {
                    Point point = target.TransformToAncestor(content).Transform(new Point(0, 0));
                    RootScrollViewer.ScrollToVerticalOffset(Math.Max(0, point.Y - 10));
                }
                catch (InvalidOperationException)
                {
                }
            }

            target.Focus();
        }

        private void TryApplyCurrentFocus()
        {
            if (_shellViewModel?.SelectedGuarantee == null)
            {
                return;
            }

            if (_shellViewModel.GuaranteeFocusRequestVersion > _lastAppliedFocusRequestVersion)
            {
                _lastAppliedFocusRequestVersion = _shellViewModel.GuaranteeFocusRequestVersion;
                ApplyFocus(_shellViewModel.CurrentGuaranteeFocusArea);
            }
        }

        private void ApplyFocus(GuaranteeFocusArea area)
        {
            Dispatcher.BeginInvoke(() => ApplyAreaFocus(area), DispatcherPriority.Loaded);
        }
    }
}
