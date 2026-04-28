using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager
{
    public partial class GuaranteesDashboardView : UserControl
    {
        public GuaranteesDashboardView()
        {
            InitializeComponent();
        }

        private void RowMoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.ContextMenu == null)
            {
                return;
            }

            button.ContextMenu.DataContext = button.DataContext;
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void RowContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu contextMenu
                || contextMenu.DataContext is not GuaranteeRow row
                || contextMenu.PlacementTarget is not FrameworkElement { Tag: ShellViewModel viewModel })
            {
                return;
            }

            MenuItem? inquiryHost = contextMenu.Items
                .OfType<MenuItem>()
                .FirstOrDefault(item => Equals(item.Tag, "InquiryActionsHost"));

            if (inquiryHost == null)
            {
                return;
            }

            inquiryHost.Items.Clear();
            IReadOnlyList<ContextActionSection> sections = GuaranteeInquiryActionSupport.BuildSections(
                App.CurrentApp.GetRequiredService<IContextActionService>());

            foreach (ContextActionSection section in sections)
            {
                var sectionMenu = new MenuItem
                {
                    Header = section.Header,
                    ToolTip = section.Description
                };
                UiInstrumentation.Identify(
                    sectionMenu,
                    UiInstrumentation.SanitizeAutomationKey("Guarantees.RowMenu.InquirySection", section.Header),
                    $"{section.Header} | {row.GuaranteeNo}");
                AutomationProperties.SetHelpText(sectionMenu, section.Description);

                foreach (ContextActionDefinition action in section.Items.Where(item => item.IsLeaf))
                {
                    ContextActionAvailability availability = viewModel.GetInquiryAvailability(row, action.Id!);
                    string tooltip = availability.IsEnabled
                        ? action.PolicyTooltip
                        : string.IsNullOrWhiteSpace(availability.DisabledReason)
                            ? action.PolicyTooltip
                            : $"{action.PolicyTooltip}{System.Environment.NewLine}{availability.DisabledReason}";

                    var actionMenu = new MenuItem
                    {
                        Header = action.Header,
                        IsEnabled = availability.IsEnabled,
                        ToolTip = tooltip
                    };
                    UiInstrumentation.Identify(
                        actionMenu,
                        UiInstrumentation.SanitizeAutomationKey("Guarantees.RowMenu.InquiryAction", action.Id!),
                        $"{action.Header} | {row.GuaranteeNo}");
                    AutomationProperties.SetHelpText(actionMenu, tooltip);
                    AutomationProperties.SetItemStatus(actionMenu, row.GuaranteeNo);
                    ToolTipService.SetShowOnDisabled(actionMenu, true);

                    actionMenu.Icon = new Viewbox
                    {
                        Width = 13,
                        Height = 13,
                        Child = new System.Windows.Shapes.Path
                        {
                            Data = (Geometry)Application.Current.FindResource("Icon.Search"),
                            Stroke = (Brush)new BrushConverter().ConvertFromString("#64748B")!,
                            StrokeThickness = 2,
                            StrokeLineJoin = PenLineJoin.Round,
                            StrokeStartLineCap = PenLineCap.Round,
                            StrokeEndLineCap = PenLineCap.Round
                        }
                    };

                    actionMenu.Click += (_, _) => viewModel.RunInquiryAction(action.Id!, row);
                    sectionMenu.Items.Add(actionMenu);
                }

                if (sectionMenu.Items.Count > 0)
                {
                    inquiryHost.Items.Add(sectionMenu);
                }
            }

            if (inquiryHost.Items.Count == 0)
            {
                inquiryHost.Items.Add(new MenuItem
                {
                    Header = "لا توجد استعلامات متاحة",
                    IsEnabled = false
                });
            }
        }
    }
}
