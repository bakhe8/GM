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

        public GuaranteeDetailPanel()
        {
            InitializeComponent();
            DataContextChanged += GuaranteeDetailPanel_DataContextChanged;
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
            }
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
            Dispatcher.BeginInvoke(() => ScrollToArea(area), DispatcherPriority.Loaded);
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
    }
}
