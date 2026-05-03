using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private readonly Border _portfolioMetricCard = new();
        private readonly Border _requestsMetricCard = new();
        private readonly Border _operationalMetricCard = new();
        private readonly Border _totalMetricCard = new();
        private readonly TextBlock _summary = BuildMutedText(12, FontWeights.Medium);
        private readonly TextBlock _portfolioValue = BuildMetricValue();
        private readonly TextBlock _requestsValue = BuildMetricValue();
        private readonly TextBlock _operationalValue = BuildMetricValue();
        private readonly TextBlock _totalValue = BuildMetricValue();
        private readonly TextBlock _detailTitle = BuildDetailValue(18, FontWeights.Bold);
        private readonly TextBlock _detailSubtitle = BuildMutedText(11, FontWeights.Medium);
        private readonly TextBlock _detailStatusBadge = BuildBadgeText();
        private readonly Border _detailStatusBadgeBorder = new();
        private readonly TextBlock _detailKey = BuildDetailValue(11, FontWeights.Medium);
        private readonly TextBlock _detailCategory = BuildDetailValue(12, FontWeights.Medium);
        private readonly TextBlock _detailStatus = BuildDetailValue(12, FontWeights.Medium);
        private readonly TextBlock _detailAction = BuildDetailValue(12, FontWeights.Medium);
        private readonly TextBlock _detailOutput = BuildMutedText(11, FontWeights.Normal);
        private readonly Button _runButton = new();
        private readonly Button _openButton = new();
        private string _selectedCategoryFilter = ReportWorkspaceItem.AllFilterLabel;

        public ReportsWorkspaceSurface(
            IReadOnlyList<WorkspaceReportCatalog.WorkspaceReportAction> actions,
            ReportsWorkspaceCoordinator coordinator,
            string? initialSearchText = null)
        {
            _dataService = new ReportsWorkspaceDataService();
            _coordinator = coordinator;
            _allReports = _dataService.BuildItems(actions);
            UiInstrumentation.Identify(this, "Reports.Workspace", "التقارير");
            UiInstrumentation.Identify(_searchInput, "Reports.SearchBox", "بحث التقارير");
            UiInstrumentation.Identify(_portfolioMetricCard, "Reports.Filter.Category.Portfolio", ReportWorkspaceItem.PortfolioFilterLabel);
            UiInstrumentation.Identify(_requestsMetricCard, "Reports.Filter.Category.Requests", ReportWorkspaceItem.RequestsFilterLabel);
            UiInstrumentation.Identify(_operationalMetricCard, "Reports.Filter.Category.Operational", ReportWorkspaceItem.OperationalFilterLabel);
            UiInstrumentation.Identify(_totalMetricCard, "Reports.Filter.Category.All", ReportWorkspaceItem.AllFilterLabel);
            UiInstrumentation.Identify(_list, "Reports.Table.List", "قائمة التقارير");

            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = UiTypography.DefaultFontFamily;
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
            _runButton.FontSize = 10;
            _runButton.Click += (_, _) => RunSelectedReport();
            UiInstrumentation.Identify(_runButton, "Reports.Detail.RunButton", "إنشاء التقرير");

            _openButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            _openButton.Content = "فتح الملف الناتج";
            _openButton.FontSize = 10;
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
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _searchInput.TextChanged += (_, _) =>
            {
                ApplyFilters();
            };
            var searchBox = WorkspaceSurfaceChrome.ToolbarSearchBox(_searchInput, "ابحث بعنوان التقرير أو وصفه أو مفتاحه...");
            Grid.SetColumn(searchBox, 0);
            toolbar.Children.Add(searchBox);

            return toolbar;
        }

        private System.Windows.Controls.Primitives.UniformGrid BuildMetrics()
        {
            var metrics = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 4
            };
            ConfigureMetricFilterCard(
                _portfolioMetricCard,
                ReportWorkspaceItem.PortfolioFilterLabel,
                _portfolioValue,
                "#2563EB");
            ConfigureMetricFilterCard(
                _requestsMetricCard,
                ReportWorkspaceItem.RequestsFilterLabel,
                _requestsValue,
                "#E09408");
            ConfigureMetricFilterCard(
                _operationalMetricCard,
                ReportWorkspaceItem.OperationalFilterLabel,
                _operationalValue,
                "#16A34A");
            ConfigureMetricFilterCard(
                _totalMetricCard,
                "إجمالي التقارير",
                _totalValue,
                "#0F172A",
                ReportWorkspaceItem.AllFilterLabel);

            metrics.Children.Add(_portfolioMetricCard);
            metrics.Children.Add(_requestsMetricCard);
            metrics.Children.Add(_operationalMetricCard);
            metrics.Children.Add(_totalMetricCard);
            WorkspaceSurfaceChrome.ApplyMetricCardSpacing(metrics);
            UpdateMetricCardStates();
            return metrics;
        }

        private void ConfigureMetricFilterCard(
            Border card,
            string label,
            TextBlock value,
            string accentHex,
            string? categoryFilter = null)
        {
            string filter = categoryFilter ?? label;
            Border template = WorkspaceSurfaceChrome.MetricCard(label, value, accentHex);
            UIElement content = template.Child;
            template.Child = null;
            card.Style = template.Style;
            card.Child = content;
            card.Padding = template.Padding;
            card.MinHeight = template.MinHeight;
            card.Tag = new ReportMetricFilter(filter, accentHex);
            card.Cursor = Cursors.Hand;
            card.Focusable = true;
            card.ToolTip = $"عرض {filter}";
            card.MouseLeftButtonUp += MetricFilterCard_MouseLeftButtonUp;
            card.KeyDown += MetricFilterCard_KeyDown;
            AutomationProperties.SetHelpText(card, $"فلترة قائمة التقارير حسب {filter}.");
        }

        private void MetricFilterCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border card && card.Tag is ReportMetricFilter filter)
            {
                SelectCategoryFilter(filter.CategoryFilter);
                e.Handled = true;
            }
        }

        private void MetricFilterCard_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Space)
            {
                return;
            }

            if (sender is Border card && card.Tag is ReportMetricFilter filter)
            {
                SelectCategoryFilter(filter.CategoryFilter);
                e.Handled = true;
            }
        }

        private void SelectCategoryFilter(string categoryFilter, bool resetPage = true, bool apply = true)
        {
            string normalized = NormalizeCategoryFilter(categoryFilter);
            bool changed = !string.Equals(_selectedCategoryFilter, normalized, StringComparison.Ordinal);
            _selectedCategoryFilter = normalized;
            UpdateMetricCardStates();

            if (apply && (changed || resetPage))
            {
                ApplyFilters();
            }
        }

        private void UpdateMetricCardStates()
        {
            ApplyMetricCardState(_portfolioMetricCard);
            ApplyMetricCardState(_requestsMetricCard);
            ApplyMetricCardState(_operationalMetricCard);
            ApplyMetricCardState(_totalMetricCard);
        }

        private void ApplyMetricCardState(Border card)
        {
            if (card.Tag is not ReportMetricFilter filter)
            {
                return;
            }

            bool selected = string.Equals(_selectedCategoryFilter, filter.CategoryFilter, StringComparison.Ordinal);
            card.Background = WorkspaceSurfaceChrome.BrushFrom(selected ? "#F8FBFF" : "#FFFFFF");
            card.BorderBrush = WorkspaceSurfaceChrome.BrushFrom(selected ? filter.AccentHex : "#E3E9F2");
            card.BorderThickness = new Thickness(selected ? 2 : 1);
            AutomationProperties.SetName(card, selected
                ? $"{filter.CategoryFilter} محدد"
                : $"فلتر {filter.CategoryFilter}");
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
            return WorkspaceSurfaceChrome.BuildReferenceTableSummaryFooter(_summary);
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
            _detailStatusBadgeBorder.Margin = new Thickness(0, 0, 0, 12);
            _detailStatusBadgeBorder.Child = _detailStatusBadge;

            return new StackPanel
            {
                Margin = new Thickness(16, 14, 16, 14),
                Children =
                {
                    BuildReportTitleRow(),
                    _detailSubtitle,
                    new Border { Height = 1, Background = WorkspaceSurfaceChrome.BrushFrom("#EDF2F7"), Margin = new Thickness(0, 13, 0, 12) },
                    WorkspaceSurfaceChrome.DetailFactBlock("المفتاح التشغيلي", _detailKey, "Icon.Badge"),
                    WorkspaceSurfaceChrome.DetailFactBlock("نوع التقرير", _detailCategory, "Icon.Reports"),
                    WorkspaceSurfaceChrome.DetailFactBlock("جاهزية التقرير", _detailStatus, "Icon.Check"),
                    WorkspaceSurfaceChrome.DetailFactBlock("الخطوة التالية", _detailAction, "Icon.Extend"),
                    WorkspaceSurfaceChrome.DetailFactBlock("آخر ملف ناتج", _detailOutput, "Icon.Document")
                }
            };
        }

        private UIElement BuildReportTitleRow()
        {
            var grid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 12),
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
            row.Children.Add(CreateIcon("Icon.Reports", "Brush.Text.Secondary", 14));
            _detailTitle.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(_detailTitle);
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
                FontWeight = FontWeights.Medium,
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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.85, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.75, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.75, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3.15, GridUnitType.Star) });
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

        private void ApplyFilters(string? preferredReportKey = null)
        {
            string? reportKeyToRestore = string.IsNullOrWhiteSpace(preferredReportKey)
                ? SelectedItem?.Key
                : preferredReportKey;
            FrameworkElement? rowToRestore = null;

            _list.Items.Clear();
            string category = NormalizeCategoryFilter(_selectedCategoryFilter);
            UpdateMetricCardStates();
            ReportsWorkspaceFilterResult filtered = _dataService.BuildFilteredItems(
                _allReports,
                _searchInput.Text,
                category);

            foreach (ReportWorkspaceItem item in filtered.Items)
            {
                FrameworkElement row = BuildRow(item);
                _list.Items.Add(row);

                if (rowToRestore == null
                    && !string.IsNullOrWhiteSpace(reportKeyToRestore)
                    && string.Equals(item.Key, reportKeyToRestore, StringComparison.OrdinalIgnoreCase))
                {
                    rowToRestore = row;
                }
            }

            if (rowToRestore != null)
            {
                _list.SelectedItem = rowToRestore;
            }
            else if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }

            ApplyMetrics(filtered.Metrics);
            _summary.Text = WorkspaceSurfaceChrome.BuildReferenceTableSummary(filtered.Items.Count, "تقرير");
            UpdateSelection();
        }

        private FrameworkElement BuildRow(ReportWorkspaceItem item)
        {
            var row = CreateTableGrid();
            row.Tag = item;
            row.Height = 40;

            ReportWorkspaceRowState rowState = _dataService.BuildRowState(
                item,
                _coordinator.Results,
                _coordinator.HasOutput(item),
                _coordinator.IsRunningReport(item));
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
                SelectedItem != null && _coordinator.HasOutput(SelectedItem),
                _coordinator.IsRunningReport(SelectedItem),
                _coordinator.IsRunning);
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

        private async void RunSelectedReport()
        {
            ReportWorkspaceItem? selected = SelectedItem;
            if (selected == null)
            {
                return;
            }

            UpdateSelection();
            bool started = await _coordinator.TryRunAsync(selected);
            if (!started)
            {
                UpdateSelection();
                return;
            }

            ApplyFilters(selected?.Key);
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
            UpdateMetricCardStates();
        }

        private static string NormalizeCategoryFilter(string? categoryFilter)
        {
            return categoryFilter switch
            {
                ReportWorkspaceItem.PortfolioFilterLabel => ReportWorkspaceItem.PortfolioFilterLabel,
                ReportWorkspaceItem.RequestsFilterLabel => ReportWorkspaceItem.RequestsFilterLabel,
                ReportWorkspaceItem.OperationalFilterLabel => ReportWorkspaceItem.OperationalFilterLabel,
                _ => ReportWorkspaceItem.AllFilterLabel
            };
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
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text.Primary"),
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
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text.Secondary"),
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap
            };
        }

        private static TextBlock BuildBadgeText()
        {
            return new TextBlock
            {
                FontSize = 10,
                FontWeight = FontWeights.Medium
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
                    Stroke = WorkspaceSurfaceChrome.ResolveBrush(strokeColor),
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                }
            };
        }

        private sealed record ReportMetricFilter(string CategoryFilter, string AccentHex);

    }
}
