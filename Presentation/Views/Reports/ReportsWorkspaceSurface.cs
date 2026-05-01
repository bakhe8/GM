using System;
using System.Collections.Generic;
using System.Windows.Automation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GuaranteeManager.Utils;

namespace GuaranteeManager
{
    public sealed class ReportsWorkspaceSurface : UserControl
    {
        private readonly ReportsWorkspaceDataService _dataService;
        private readonly ReportsWorkspaceCoordinator _coordinator;
        private readonly List<ReportWorkspaceItem> _allReports;
        private readonly ListBox _list = new();
        private readonly TextBox _searchInput = new();
        private readonly ComboBox _categoryFilter = new();
        private readonly TextBlock _summary = BuildMutedText(12, FontWeights.SemiBold);
        private readonly TextBlock _portfolioValue = BuildMetricValue();
        private readonly TextBlock _requestsValue = BuildMetricValue();
        private readonly TextBlock _operationalValue = BuildMetricValue();
        private readonly TextBlock _totalValue = BuildMetricValue();
        private readonly TextBlock _detailTitle = BuildDetailValue(16, FontWeights.Bold);
        private readonly TextBlock _detailSubtitle = BuildMutedText(11, FontWeights.SemiBold);
        private readonly TextBlock _detailStatusBadge = BuildBadgeText();
        private readonly Border _detailStatusBadgeBorder = new();
        private readonly TextBlock _detailKey = BuildDetailValue(11.5, FontWeights.SemiBold);
        private readonly TextBlock _detailCategory = BuildDetailValue(12, FontWeights.Bold);
        private readonly TextBlock _detailStatus = BuildDetailValue(12, FontWeights.Bold);
        private readonly TextBlock _detailAction = BuildDetailValue(12, FontWeights.SemiBold);
        private readonly TextBlock _detailOutput = BuildMutedText(11, FontWeights.Normal);
        private readonly Button _runButton = new();
        private readonly Button _openButton = new();
        private readonly ReferenceTablePagerController _pager;

        public ReportsWorkspaceSurface(
            IReadOnlyList<WorkspaceReportCatalog.WorkspaceReportAction> actions,
            ReportsWorkspaceCoordinator coordinator,
            string? initialSearchText = null)
        {
            _dataService = new ReportsWorkspaceDataService();
            _coordinator = coordinator;
            _allReports = _dataService.BuildItems(actions);
            _pager = new ReferenceTablePagerController("Reports", "تقرير", 10, ApplyFilters);
            UiInstrumentation.Identify(this, "Reports.Workspace", "التقارير");
            UiInstrumentation.Identify(_searchInput, "Reports.SearchBox", "بحث التقارير");
            UiInstrumentation.Identify(_categoryFilter, "Reports.Filter.Category", "نوع التقرير");
            UiInstrumentation.Identify(_list, "Reports.Table.List", "قائمة التقارير");

            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = WorkspaceSurfaceChrome.BrushResource("Brush.Canvas");

            ConfigureButtons();
            Content = BuildLayout();
            ApplyFilters();
            ApplyInitialSearch(initialSearchText);
        }

        private void ApplyInitialSearch(string? initialSearchText)
        {
            if (!string.IsNullOrWhiteSpace(initialSearchText))
            {
                _searchInput.Text = initialSearchText.Trim();
            }
        }

        private Grid BuildLayout()
        {
            return WorkspaceSurfaceChrome.BuildReferenceWorkspace(
                BuildToolbarBlock(),
                BuildMetrics(),
                BuildTableSection(),
                BuildDetailPanel());
        }

        private void ConfigureButtons()
        {
            _runButton.Style = WorkspaceSurfaceChrome.Style("PrimaryButton");
            _runButton.Content = "إنشاء التقرير";
            _runButton.FontSize = 9.5;
            _runButton.Click += (_, _) => RunSelectedReport();
            UiInstrumentation.Identify(_runButton, "Reports.Detail.RunButton", "إنشاء التقرير");

            _openButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            _openButton.Content = "فتح الملف الناتج";
            _openButton.FontSize = 9.5;
            _openButton.Click += (_, _) => OpenLastReport();
            UiInstrumentation.Identify(_openButton, "Reports.Detail.OpenButton", "فتح الملف الناتج");

        }

        private UIElement BuildToolbarBlock()
        {
            return BuildToolbar();
        }

        private Grid BuildToolbar()
        {
            var toolbar = new Grid { FlowDirection = FlowDirection.LeftToRight };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _categoryFilter.Style = WorkspaceSurfaceChrome.Style("FilterComboBox");
            _categoryFilter.Items.Add("كل التقارير");
            _categoryFilter.Items.Add("تقارير المحفظة");
            _categoryFilter.Items.Add("تقارير الطلبات");
            _categoryFilter.Items.Add("تقارير تشغيلية");
            _categoryFilter.SelectedIndex = 0;
            _categoryFilter.SelectionChanged += (_, _) =>
            {
                _pager.ResetToFirstPage();
                ApplyFilters();
            };
            Grid.SetColumn(_categoryFilter, 0);
            toolbar.Children.Add(_categoryFilter);

            _searchInput.TextChanged += (_, _) =>
            {
                _pager.ResetToFirstPage();
                ApplyFilters();
            };
            var searchBox = WorkspaceSurfaceChrome.ToolbarSearchBox(_searchInput, "ابحث بعنوان التقرير أو وصفه أو مفتاحه...");
            Grid.SetColumn(searchBox, 2);
            toolbar.Children.Add(searchBox);

            return toolbar;
        }

        private System.Windows.Controls.Primitives.UniformGrid BuildMetrics()
        {
            var metrics = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 4
            };
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("تقارير المحفظة", _portfolioValue, "#2563EB"));
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("تقارير الطلبات", _requestsValue, "#E09408"));
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("تقارير تشغيلية", _operationalValue, "#16A34A"));
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("إجمالي التقارير", _totalValue, "#0F172A"));
            WorkspaceSurfaceChrome.ApplyMetricCardSpacing(metrics);
            return metrics;
        }

        private UIElement BuildTableSection()
        {
            _list.SelectionChanged += (_, _) => UpdateSelection();
            return WorkspaceSurfaceChrome.BuildReferenceTableShell(
                BuildTableHeader(),
                _list,
                BuildTableFooter());
        }

        private Grid BuildTableHeader()
        {
            var header = new Grid
            {
                Style = WorkspaceSurfaceChrome.Style("ReferenceTableHeaderBand")
            };
            header.Children.Add(new Border
            {
                Style = WorkspaceSurfaceChrome.Style("ReferenceTableHeaderDivider")
            });

            var inner = CreateTableGrid();
            AddHeader(inner, "الفئة", 0, false);
            AddHeader(inner, "الحالة", 1, false);
            AddHeader(inner, "المفتاح", 2, true);
            AddHeader(inner, "عنوان التقرير", 3, true);
            header.Children.Add(inner);
            return header;
        }

        private Grid BuildTableFooter()
        {
            return _pager.BuildFooter(_summary);
        }

        private Border BuildDetailPanel()
        {
            return WorkspaceSurfaceChrome.BuildReferenceDetailPanel(
                BuildDetailContent(),
                BuildDetailActions());
        }

        private UIElement BuildDetailContent()
        {
            _detailStatusBadgeBorder.Style = WorkspaceSurfaceChrome.Style("StatusPill");
            _detailStatusBadgeBorder.HorizontalAlignment = HorizontalAlignment.Left;
            _detailStatusBadgeBorder.Margin = new Thickness(0, 8, 0, 12);
            _detailStatusBadgeBorder.Child = _detailStatusBadge;

            return new StackPanel
            {
                Margin = new Thickness(16, 14, 16, 14),
                Children =
                {
                    BuildReportTitleRow(),
                    _detailSubtitle,
                    new Border { Height = 1, Background = WorkspaceSurfaceChrome.BrushFrom("#EDF2F7"), Margin = new Thickness(0, 13, 0, 12) },
                    WorkspaceSurfaceChrome.DetailFactLine("المفتاح التشغيلي", _detailKey, "Icon.Badge"),
                    WorkspaceSurfaceChrome.DetailFactLine("نوع التقرير", _detailCategory, "Icon.Reports"),
                    WorkspaceSurfaceChrome.DetailFactLine("جاهزية التقرير", _detailStatus, "Icon.Check"),
                    WorkspaceSurfaceChrome.DetailFactLine("الخطوة التالية", _detailAction, "Icon.Extend"),
                    WorkspaceSurfaceChrome.DetailFactBlock("آخر ملف ناتج", _detailOutput, "Icon.Document")
                }
            };
        }

        private UIElement BuildReportTitleRow()
        {
            var grid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 8),
                FlowDirection = FlowDirection.LeftToRight
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _detailStatusBadgeBorder.Margin = new Thickness(0);
            _detailStatusBadgeBorder.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(_detailStatusBadgeBorder);

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(CreateIcon("Icon.Reports", "#64748B", 14));
            _detailTitle.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(_detailTitle);
            row.Children.Add(WorkspaceSurfaceChrome.DetailHeaderCopyButton(
                "نسخ اسم التقرير",
                "Reports.Detail.Header.CopyTitle",
                (_, _) => WorkspaceSurfaceChrome.CopyDetailFactValue("اسم التقرير", _detailTitle.Text, "التقارير")));
            Grid.SetColumn(row, 1);
            grid.Children.Add(row);
            return grid;
        }

        private Border BuildDetailActions()
        {
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = WorkspaceSurfaceChrome.BrushFrom("#E3E9F2"),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(12)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(new TextBlock
            {
                Text = "إجراءات سريعة",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text")
            });

            var actions = new Grid { FlowDirection = FlowDirection.LeftToRight, Margin = new Thickness(0, 9, 0, 0) };
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.Children.Add(_openButton);
            Grid.SetColumn(_runButton, 2);
            actions.Children.Add(_runButton);
            Grid.SetRow(actions, 1);
            grid.Children.Add(actions);

            border.Child = grid;
            return border;
        }

        private static Grid CreateTableGrid()
        {
            var grid = new Grid
            {
                Margin = new Thickness(9, 0, 9, 0),
                FlowDirection = FlowDirection.LeftToRight
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.55, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.7, GridUnitType.Star) });
            return grid;
        }

        private static void AddHeader(Grid grid, string text, int column, bool rightAligned)
        {
            var header = new TextBlock
            {
                Text = text,
                Style = WorkspaceSurfaceChrome.Style(rightAligned ? "TableHeaderRight" : "TableHeaderText")
            };
            Grid.SetColumn(header, column);
            grid.Children.Add(header);
        }

        private ReportWorkspaceItem? SelectedItem
            => (_list.SelectedItem as FrameworkElement)?.Tag as ReportWorkspaceItem;

        private void ApplyFilters()
        {
            _list.Items.Clear();
            string category = _categoryFilter.SelectedItem as string ?? "كل التقارير";
            ReportsWorkspaceFilterResult filtered = _dataService.BuildFilteredItems(
                _allReports,
                _searchInput.Text,
                category);

            IReadOnlyList<ReportWorkspaceItem> pageItems = _pager.Page(filtered.Items);
            foreach (ReportWorkspaceItem item in pageItems)
            {
                _list.Items.Add(BuildRow(item));
            }

            if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }

            ApplyMetrics(filtered.Metrics);
            _summary.Text = _pager.BuildSummary();
            UpdateSelection();
        }

        private FrameworkElement BuildRow(ReportWorkspaceItem item)
        {
            var row = CreateTableGrid();
            row.Tag = item;
            row.Height = 40;

            ReportWorkspaceRowState rowState = _dataService.BuildRowState(item, _coordinator.Results, _coordinator.HasOutput(item));
            row.Children.Add(BuildCell(item.Category, 0, "TableCellCenter", item.CategoryBrush));
            row.Children.Add(BuildCell(rowState.StatusLabel, 1, "TableCellCenter", rowState.StatusBrush));
            row.Children.Add(BuildCell(item.Key, 2, "TableCellRight"));
            row.Children.Add(BuildCell(item.Title, 3, "TableCellRight"));
            return row;
        }

        private static TextBlock BuildCell(string text, int column, string styleKey, Brush? foreground = null)
        {
            var cell = new TextBlock
            {
                Text = text,
                Style = WorkspaceSurfaceChrome.Style(styleKey)
            };

            if (foreground != null)
            {
                cell.Foreground = foreground;
            }

            Grid.SetColumn(cell, column);
            return cell;
        }

        private void UpdateSelection()
        {
            ReportsWorkspaceDetailState state = _dataService.BuildDetailState(
                SelectedItem,
                _coordinator.Results,
                SelectedItem != null && _coordinator.HasOutput(SelectedItem));
            _detailTitle.Text = state.Title;
            _detailSubtitle.Text = state.Subtitle;
            _detailStatusBadge.Text = state.BadgeText;
            _detailStatusBadge.Foreground = state.BadgeForeground;
            _detailStatusBadgeBorder.Background = state.BadgeBackground;
            _detailStatusBadgeBorder.BorderBrush = state.BadgeBorder;
            _detailKey.Text = state.Key;
            _detailCategory.Text = state.Category;
            _detailCategory.Foreground = state.CategoryBrush;
            _detailStatus.Text = state.Status;
            _detailStatus.Foreground = state.StatusBrush;
            _detailAction.Text = state.Action;
            _detailOutput.Text = state.Output;
            _runButton.IsEnabled = state.CanRun;
            _openButton.IsEnabled = state.CanOpen;

            if (SelectedItem == null)
            {
                AutomationProperties.SetName(_runButton, "إنشاء التقرير");
                AutomationProperties.SetHelpText(_runButton, "اختر تقريرًا أولًا ثم أنشئ ملفه الناتج.");
                AutomationProperties.SetItemStatus(_runButton, "---");
                AutomationProperties.SetName(_openButton, "فتح الملف الناتج");
                AutomationProperties.SetHelpText(_openButton, "لا يوجد ملف ناتج جاهز للفتح حتى الآن.");
                AutomationProperties.SetItemStatus(_openButton, "---");
                return;
            }

            AutomationProperties.SetName(_runButton, $"إنشاء تقرير {SelectedItem.Title}");
            AutomationProperties.SetHelpText(_runButton, state.Action);
            AutomationProperties.SetItemStatus(_runButton, SelectedItem.Key);

            string outputLabel = state.CanOpen && !string.IsNullOrWhiteSpace(state.Output)
                ? System.IO.Path.GetFileName(state.Output)
                : "لا يوجد ناتج جاهز";
            AutomationProperties.SetName(_openButton, $"فتح ناتج {SelectedItem.Title}");
            AutomationProperties.SetHelpText(_openButton, state.CanOpen
                ? $"افتح آخر ملف ناتج محفوظ لهذا التقرير: {outputLabel}"
                : "أنشئ التقرير أولًا حتى يصبح له ملف ناتج جاهز للفتح.");
            AutomationProperties.SetItemStatus(_openButton, outputLabel);
        }

        private void RunSelectedReport()
        {
            if (!_coordinator.TryRun(SelectedItem))
            {
                return;
            }

            ApplyFilters();
        }

        private void OpenLastReport()
        {
            _coordinator.OpenLastReport(SelectedItem);
        }

        private void ApplyMetrics(ReportsWorkspaceMetrics metrics)
        {
            _portfolioValue.Text = metrics.Portfolio;
            _requestsValue.Text = metrics.Requests;
            _operationalValue.Text = metrics.Operational;
            _totalValue.Text = metrics.Total;
        }

        private static TextBlock BuildMetricValue()
        {
            return WorkspaceSurfaceChrome.MetricValueText();
        }

        private static TextBlock BuildDetailValue(double fontSize, FontWeight fontWeight)
        {
            return new TextBlock
            {
                FontSize = fontSize,
                FontWeight = fontWeight,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#111827"),
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap
            };
        }

        private static TextBlock BuildMutedText(double fontSize, FontWeight fontWeight)
        {
            return new TextBlock
            {
                FontSize = fontSize,
                FontWeight = fontWeight,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap
            };
        }

        private static TextBlock BuildBadgeText()
        {
            return new TextBlock
            {
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
        }

        private static UIElement CreateIcon(string resourceKey, string strokeColor, double size)
        {
            if (Application.Current.TryFindResource(resourceKey) is not Geometry geometry)
            {
                return new Border { Width = size, Height = size };
            }

            return new Viewbox
            {
                Width = size,
                Height = size,
                VerticalAlignment = VerticalAlignment.Center,
                    Child = new System.Windows.Shapes.Path
                {
                    Data = geometry,
                    Stroke = WorkspaceSurfaceChrome.BrushFrom(strokeColor),
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                }
            };
        }

    }
}
