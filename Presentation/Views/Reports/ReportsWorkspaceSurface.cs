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
        private readonly TextBlock _operationalValue = BuildMetricValue();
        private readonly TextBlock _totalValue = BuildMetricValue();
        private readonly TextBlock _statusValue = BuildMetricValue();
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
        private readonly Action? _closeRequested;

        public ReportsWorkspaceSurface(
            IReadOnlyList<WorkspaceReportCatalog.WorkspaceReportAction> actions,
            ReportsWorkspaceCoordinator coordinator,
            Action? closeRequested,
            string? initialSearchText = null)
        {
            _dataService = new ReportsWorkspaceDataService();
            _coordinator = coordinator;
            _allReports = _dataService.BuildItems(actions);
            _closeRequested = closeRequested;
            UiInstrumentation.Identify(this, "Reports.Workspace", "التقارير");
            UiInstrumentation.Identify(_searchInput, "Reports.SearchBox", "بحث التقارير");
            UiInstrumentation.Identify(_categoryFilter, "Reports.Filter.Category", "فئة التقارير");
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
                BuildToolbar(),
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
            _openButton.Content = "فتح التقرير";
            _openButton.FontSize = 9.5;
            _openButton.Click += (_, _) => OpenLastReport();
            UiInstrumentation.Identify(_openButton, "Reports.Detail.OpenButton", "فتح التقرير");
        }

        private Grid BuildToolbar()
        {
            var toolbar = new Grid { FlowDirection = FlowDirection.LeftToRight };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var runButton = WorkspaceSurfaceChrome.ToolbarButton("إنشاء التقرير", primary: true, automationId: "Reports.Toolbar.Run");
            runButton.Click += (_, _) => RunSelectedReport();
            Grid.SetColumn(runButton, 0);
            toolbar.Children.Add(runButton);

            var resetButton = WorkspaceSurfaceChrome.ToolbarButton("إعادة ضبط", automationId: "Reports.Toolbar.Reset");
            resetButton.Click += (_, _) =>
            {
                _searchInput.Text = string.Empty;
                _categoryFilter.SelectedIndex = 0;
                ApplyFilters();
            };
            Grid.SetColumn(resetButton, 2);
            toolbar.Children.Add(resetButton);

            _categoryFilter.Style = WorkspaceSurfaceChrome.Style("FilterComboBox");
            _categoryFilter.Items.Add("كل التقارير");
            _categoryFilter.Items.Add("تقارير المحفظة");
            _categoryFilter.Items.Add("تقارير تشغيلية");
            _categoryFilter.SelectedIndex = 0;
            _categoryFilter.SelectionChanged += (_, _) => ApplyFilters();
            Grid.SetColumn(_categoryFilter, 4);
            toolbar.Children.Add(_categoryFilter);

            _searchInput.TextChanged += (_, _) => ApplyFilters();
            var searchBox = WorkspaceSurfaceChrome.ToolbarSearchBox(_searchInput, "ابحث بعنوان التقرير أو وصفه أو مفتاحه...");
            Grid.SetColumn(searchBox, 6);
            toolbar.Children.Add(searchBox);

            return toolbar;
        }

        private System.Windows.Controls.Primitives.UniformGrid BuildMetrics()
        {
            var metrics = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 4
            };
            metrics.Children.Add(BuildMetricCard("تقارير المحفظة", _portfolioValue, "#2563EB"));
            metrics.Children.Add(BuildMetricCard("تقارير تشغيلية", _operationalValue, "#E09408"));
            metrics.Children.Add(BuildMetricCard("إجمالي الخيارات", _totalValue, "#0F172A"));
            metrics.Children.Add(BuildMetricCard("الحالة", _statusValue, "#16A34A"));
            return metrics;
        }

        private Border BuildMetricCard(string label, TextBlock value, string accent)
        {
            var card = WorkspaceSurfaceChrome.Card(new Thickness(14, 10, 14, 10));
            card.Margin = new Thickness(0, 0, 10, 0);
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom(accent),
                TextAlignment = TextAlignment.Right
            });
            stack.Children.Add(value);
            card.Child = stack;
            return card;
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
            AddHeader(inner, "الإجراءات", 0, false);
            AddHeader(inner, "الفئة", 1, false);
            AddHeader(inner, "الحالة", 2, false);
            AddHeader(inner, "المفتاح", 3, true);
            AddHeader(inner, "عنوان التقرير", 4, true);
            header.Children.Add(inner);
            return header;
        }

        private Grid BuildTableFooter()
        {
            var footer = new Grid
            {
                Style = WorkspaceSurfaceChrome.Style("ReferenceTablePager")
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            buttons.Children.Add(new Button
            {
                Content = "←",
                Style = WorkspaceSurfaceChrome.Style("ReferenceTablePagerButton")
            });
            buttons.Children.Add(new Button
            {
                Content = "1",
                Margin = new Thickness(6, 0, 0, 0),
                Style = WorkspaceSurfaceChrome.Style("ReferenceTablePagerActiveButton")
            });
            buttons.Children.Add(new Button
            {
                Content = "10",
                MinWidth = 46,
                Margin = new Thickness(12, 0, 0, 0),
                Style = WorkspaceSurfaceChrome.Style("ReferenceTablePagerButton")
            });
            buttons.Children.Add(new TextBlock
            {
                Text = "لكل صفحة",
                FontSize = 11,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Muted"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            });
            footer.Children.Add(buttons);

            _summary.Style = WorkspaceSurfaceChrome.Style("ReferenceTableFooterSummary");
            footer.Children.Add(_summary);
            return footer;
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
                    BuildDetailHeader(),
                    BuildReportTitleRow(),
                    _detailSubtitle,
                    _detailStatusBadgeBorder,
                    new Border { Height = 1, Background = WorkspaceSurfaceChrome.BrushFrom("#EDF2F7"), Margin = new Thickness(0, 13, 0, 12) },
                    WorkspaceSurfaceChrome.InfoLine("المفتاح", _detailKey),
                    WorkspaceSurfaceChrome.InfoLine("الفئة", _detailCategory),
                    WorkspaceSurfaceChrome.InfoLine("الحالة التشغيلية", _detailStatus),
                    WorkspaceSurfaceChrome.InfoLine("الإجراء التالي", _detailAction),
                    BuildInfoBlock("آخر ناتج", _detailOutput)
                }
            };
        }

        private UIElement BuildDetailHeader()
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            grid.Children.Add(new TextBlock
            {
                Text = "تفاصيل التقرير",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text")
            });

            var closeButton = new Button
            {
                Width = 28,
                Height = 28,
                Content = "×",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B")
            };
            closeButton.Click += (_, _) => _closeRequested?.Invoke();
            Grid.SetColumn(closeButton, 1);
            grid.Children.Add(closeButton);
            return grid;
        }

        private UIElement BuildReportTitleRow()
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft
            };
            row.Children.Add(CreateIcon("Icon.Reports", "#64748B", 14));
            _detailTitle.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(_detailTitle);
            return row;
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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });
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
                category,
                _coordinator.Results);

            foreach (ReportWorkspaceItem item in filtered.Items)
            {
                _list.Items.Add(BuildRow(item));
            }

            if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }

            ApplyMetrics(filtered.Metrics);
            _summary.Text = filtered.Summary;
            UpdateSelection();
        }

        private FrameworkElement BuildRow(ReportWorkspaceItem item)
        {
            var row = CreateTableGrid();
            row.Tag = item;
            row.Height = 40;

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            ReportWorkspaceRowState rowState = _dataService.BuildRowState(item, _coordinator.Results, _coordinator.HasOutput(item));
            actions.Children.Add(CreateRowButton("فتح", "Icon.Document", item, OpenRow_Click, rowState.CanOpen));
            actions.Children.Add(CreateRowButton("تشغيل", "Icon.NewTransaction", item, RunRow_Click, true));
            actions.Children.Add(CreateRowButton("عرض", "Icon.View", item, SelectRow_Click, true));
            Grid.SetColumn(actions, 0);
            row.Children.Add(actions);

            row.Children.Add(BuildCell(item.Category, 1, "TableCellCenter", item.CategoryBrush));
            row.Children.Add(BuildCell(rowState.StatusLabel, 2, "TableCellCenter", rowState.StatusBrush));
            row.Children.Add(BuildCell(item.Key, 3, "TableCellRight"));
            row.Children.Add(BuildCell(item.Title, 4, "TableCellRight"));
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

        private static Button CreateRowButton(string text, string iconKey, ReportWorkspaceItem item, RoutedEventHandler handler, bool isEnabled)
        {
            var button = new Button
            {
                Content = BuildRowButtonContent(text, iconKey),
                Tag = item,
                Style = WorkspaceSurfaceChrome.Style("RowButton"),
                IsEnabled = isEnabled
            };
            button.Click += handler;
            return button;
        }

        private static UIElement BuildRowButtonContent(string text, string iconKey)
        {
            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            if (Application.Current.TryFindResource(iconKey) is Geometry geometry)
            {
                stack.Children.Add(new Viewbox
                {
                    Width = 10,
                    Height = 10,
                    Margin = new Thickness(0, 0, 3, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new System.Windows.Shapes.Path
                    {
                        Data = geometry,
                        Stroke = WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                        StrokeThickness = 2,
                        StrokeLineJoin = PenLineJoin.Round,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    }
                });
            }

            stack.Children.Add(new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center
            });
            return stack;
        }

        private void SelectRow_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
        }

        private void RunRow_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
            RunSelectedReport();
        }

        private void OpenRow_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
            OpenLastReport();
        }

        private void SelectRowFromSender(object sender)
        {
            if (sender is not FrameworkElement element || element.Tag is not ReportWorkspaceItem item)
            {
                return;
            }

            foreach (object row in _list.Items)
            {
                if (row is FrameworkElement frameworkElement && ReferenceEquals(frameworkElement.Tag, item))
                {
                    _list.SelectedItem = frameworkElement;
                    frameworkElement.Focus();
                    return;
                }
            }
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
                AutomationProperties.SetHelpText(_runButton, "اختر تقريرًا أولًا ثم أنشئ ناتجه.");
                AutomationProperties.SetItemStatus(_runButton, "---");
                AutomationProperties.SetName(_openButton, "فتح التقرير");
                AutomationProperties.SetHelpText(_openButton, "لا يوجد ناتج جاهز للفتح حتى الآن.");
                AutomationProperties.SetItemStatus(_openButton, "---");
                return;
            }

            AutomationProperties.SetName(_runButton, $"إنشاء تقرير {SelectedItem.Title}");
            AutomationProperties.SetHelpText(_runButton, state.Action);
            AutomationProperties.SetItemStatus(_runButton, SelectedItem.Key);

            string outputLabel = state.CanOpen && !string.IsNullOrWhiteSpace(state.Output)
                ? System.IO.Path.GetFileName(state.Output)
                : "لا يوجد ناتج جاهز";
            AutomationProperties.SetName(_openButton, $"فتح تقرير {SelectedItem.Title}");
            AutomationProperties.SetHelpText(_openButton, state.CanOpen
                ? $"افتح آخر ناتج محفوظ لهذا التقرير: {outputLabel}"
                : "أنشئ التقرير أولًا حتى يصبح له ناتج جاهز للفتح.");
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
            _operationalValue.Text = metrics.Operational;
            _totalValue.Text = metrics.Total;
            _statusValue.Text = metrics.Status;
        }

        private static StackPanel BuildInfoBlock(string label, TextBlock value)
        {
            value.TextWrapping = TextWrapping.Wrap;
            value.Margin = new Thickness(0, 4, 0, 0);
            return new StackPanel
            {
                Margin = new Thickness(0, 4, 0, 8),
                Children =
                {
                    new TextBlock
                    {
                        Text = label,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = WorkspaceSurfaceChrome.BrushFrom("#94A3C8")
                    },
                    value
                }
            };
        }

        private static TextBlock BuildMetricValue()
        {
            return new TextBlock
            {
                Text = "0",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A"),
                Margin = new Thickness(0, 4, 0, 0),
                TextAlignment = TextAlignment.Right
            };
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
