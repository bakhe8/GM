using System;
using System.Collections.Generic;
using System.Windows.Automation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GuaranteeManager
{
    public sealed class SettingsWorkspaceSurface : UserControl
    {
        private readonly SettingsWorkspaceDataService _dataService;
        private readonly SettingsWorkspaceCoordinator _coordinator;
        private readonly List<SettingPathItem> _allItems;
        private readonly ListBox _list = new();
        private readonly TextBox _searchInput = new();
        private readonly Border _allMetricCard = new();
        private readonly Border _dataMetricCard = new();
        private readonly Border _workflowMetricCard = new();
        private readonly TextBlock _summary = BuildMutedText(12, FontWeights.SemiBold);
        private readonly TextBlock _allValue = BuildMetricValue();
        private readonly TextBlock _dataValue = BuildMetricValue();
        private readonly TextBlock _workflowValue = BuildMetricValue();
        private readonly TextBlock _detailTitle = BuildDetailValue(16, FontWeights.Bold);
        private readonly TextBlock _detailSubtitle = BuildMutedText(11, FontWeights.SemiBold);
        private readonly TextBlock _detailStatusBadge = BuildBadgeText();
        private readonly Border _detailStatusBadgeBorder = new();
        private readonly TextBlock _detailState = BuildDetailValue(12, FontWeights.Bold);
        private readonly TextBlock _detailAction = BuildDetailValue(12, FontWeights.SemiBold);
        private readonly TextBlock _detailPath = BuildPathText();
        private readonly TextBlock _detailOpenPath = BuildPathText();
        private readonly Action? _dataResetCompleted;
        private readonly ReferenceTablePagerController _pager;
        private string _selectedCategoryFilter = SettingsPathFilters.All;

        public SettingsWorkspaceSurface(Action? dataResetCompleted, string? initialSearchText = null)
        {
            _dataService = new SettingsWorkspaceDataService();
            _coordinator = new SettingsWorkspaceCoordinator();
            _dataResetCompleted = dataResetCompleted;
            _allItems = _dataService.BuildItems();
            _pager = new ReferenceTablePagerController("Settings", "عنصر", 10, ApplyFilters);
            UiInstrumentation.Identify(this, "Settings.Workspace", "الإعدادات");
            UiInstrumentation.Identify(_searchInput, "Settings.SearchBox", "بحث الإعدادات");
            UiInstrumentation.Identify(_allMetricCard, "Settings.Filter.All", SettingsPathFilters.All);
            UiInstrumentation.Identify(_dataMetricCard, "Settings.Filter.Data", SettingsPathFilters.Data);
            UiInstrumentation.Identify(_workflowMetricCard, "Settings.Filter.Workflow", SettingsPathFilters.Workflow);
            UiInstrumentation.Identify(_list, "Settings.Table.List", "قائمة الإعدادات");

            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = WorkspaceSurfaceChrome.BrushResource("Brush.Canvas");

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

        private void RefreshAfterDataReset()
        {
            ApplyFilters();
            _dataResetCompleted?.Invoke();
        }

        private Grid BuildToolbar()
        {
            var toolbar = new Grid { FlowDirection = FlowDirection.LeftToRight };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Button refreshButton = WorkspaceSurfaceChrome.ToolbarButton("إعادة فحص", primary: true, automationId: "Settings.Toolbar.Refresh");
            refreshButton.Click += (_, _) => ApplyFilters();
            Grid.SetColumn(refreshButton, 0);
            toolbar.Children.Add(refreshButton);

            _searchInput.TextChanged += (_, _) =>
            {
                _pager.ResetToFirstPage();
                ApplyFilters();
            };
            var searchBox = WorkspaceSurfaceChrome.ToolbarSearchBox(_searchInput, "ابحث باسم العنصر أو المسار...");
            Grid.SetColumn(searchBox, 2);
            toolbar.Children.Add(searchBox);
            return toolbar;
        }

        private System.Windows.Controls.Primitives.UniformGrid BuildMetrics()
        {
            var metrics = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 3
            };
            ConfigureMetricFilterCard(_allMetricCard, SettingsPathFilters.All, _allValue, "#0F172A");
            ConfigureMetricFilterCard(_dataMetricCard, SettingsPathFilters.Data, _dataValue, "#2563EB");
            ConfigureMetricFilterCard(_workflowMetricCard, SettingsPathFilters.Workflow, _workflowValue, "#E09408");

            metrics.Children.Add(_allMetricCard);
            metrics.Children.Add(_dataMetricCard);
            metrics.Children.Add(_workflowMetricCard);
            WorkspaceSurfaceChrome.ApplyMetricCardSpacing(metrics);
            UpdateMetricCardStates();
            return metrics;
        }

        private void ConfigureMetricFilterCard(Border card, string label, TextBlock value, string accentHex)
        {
            Border template = WorkspaceSurfaceChrome.MetricCard(label, value, accentHex);
            UIElement content = template.Child;
            template.Child = null;
            card.Style = template.Style;
            card.Child = content;
            card.Padding = template.Padding;
            card.MinHeight = template.MinHeight;
            card.Tag = new SettingsMetricFilter(label, accentHex);
            card.Cursor = Cursors.Hand;
            card.Focusable = true;
            card.ToolTip = $"عرض {label}";
            card.MouseLeftButtonUp += MetricFilterCard_MouseLeftButtonUp;
            card.KeyDown += MetricFilterCard_KeyDown;
            AutomationProperties.SetHelpText(card, $"فلترة قائمة الإعدادات حسب {label}.");
        }

        private void MetricFilterCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border card && card.Tag is SettingsMetricFilter filter)
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

            if (sender is Border card && card.Tag is SettingsMetricFilter filter)
            {
                SelectCategoryFilter(filter.CategoryFilter);
                e.Handled = true;
            }
        }

        private void SelectCategoryFilter(string categoryFilter, bool resetPage = true, bool apply = true)
        {
            string normalized = SettingsPathFilters.Normalize(categoryFilter);
            bool changed = !string.Equals(_selectedCategoryFilter, normalized, StringComparison.Ordinal);
            _selectedCategoryFilter = normalized;
            UpdateMetricCardStates();

            if (resetPage)
            {
                _pager.ResetToFirstPage();
            }

            if (apply && (changed || resetPage))
            {
                ApplyFilters();
            }
        }

        private void UpdateMetricCardStates()
        {
            ApplyMetricCardState(_allMetricCard);
            ApplyMetricCardState(_dataMetricCard);
            ApplyMetricCardState(_workflowMetricCard);
        }

        private void ApplyMetricCardState(Border card)
        {
            if (card.Tag is not SettingsMetricFilter filter)
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
            _list.SelectionChanged += (_, _) => UpdateDetails();
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
            AddHeader(inner, "فتح", 0, false);
            AddHeader(inner, "الحالة", 1, false);
            AddHeader(inner, "الفئة", 2, false);
            AddHeader(inner, "المسار", 3, true);
            AddHeader(inner, "العنصر", 4, true);
            header.Children.Add(inner);
            return header;
        }

        private Grid BuildTableFooter()
        {
            return _pager.BuildFooter(_summary);
        }

        private Border BuildDetailPanel()
        {
#if DEBUG
            const double quickActionsHeight = 176;
#else
            const double quickActionsHeight = 134;
#endif
            return WorkspaceSurfaceChrome.BuildReferenceDetailPanel(
                BuildDetailContent(),
                BuildDetailActions(),
                quickActionsHeight);
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
                    BuildSettingsTitleRow(),
                    _detailSubtitle,
                    new Border { Height = 1, Background = WorkspaceSurfaceChrome.BrushFrom("#EDF2F7"), Margin = new Thickness(0, 13, 0, 12) },
                    BuildReadOnlyDetailLine("الحالة التشغيلية", _detailState, "Icon.Check"),
                    WorkspaceSurfaceChrome.DetailFactLine("الإجراء التالي", _detailAction, "Icon.Extend"),
                    WorkspaceSurfaceChrome.DetailFactBlock("المسار", _detailPath, "Icon.Document", (_, _) => _coordinator.CopyPath(SelectedItem), "Settings.Detail.CopyPath", "نسخ المسار"),
                    WorkspaceSurfaceChrome.DetailFactBlock("مسار الفتح", _detailOpenPath, "Icon.Logout", (_, _) => _coordinator.CopyOpenPath(SelectedItem), "Settings.Detail.CopyOpenPath", "نسخ مسار الفتح")
                }
            };
        }

        private UIElement BuildSettingsTitleRow()
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
            row.Children.Add(CreateIcon("Icon.Settings", "#64748B", 14));
            _detailTitle.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(_detailTitle);
            Grid.SetColumn(row, 1);
            grid.Children.Add(row);
            return grid;
        }

        private Border BuildDetailActions()
        {
            Button createBackupButton = WorkspaceSurfaceChrome.ToolbarButton("نسخ احتياطي", automationId: "Settings.QuickAction.CreateBackup");
            createBackupButton.Click += (_, _) => _coordinator.CreateManualBackup();

            Button restoreBackupButton = WorkspaceSurfaceChrome.ToolbarButton("استرجاع نسخة", automationId: "Settings.QuickAction.RestoreBackup");
            restoreBackupButton.Click += (_, _) => _coordinator.RestoreManualBackup(RefreshAfterDataReset);

            Button createPortableButton = WorkspaceSurfaceChrome.ToolbarButton("حزمة محمولة", automationId: "Settings.QuickAction.CreatePortable");
            createPortableButton.Click += (_, _) => _coordinator.CreatePortableBackup();

            Button restorePortableButton = WorkspaceSurfaceChrome.ToolbarButton("استرجاع حزمة", automationId: "Settings.QuickAction.RestorePortable");
            restorePortableButton.Click += (_, _) => _coordinator.RestorePortableBackup(RefreshAfterDataReset);

            var actionButtons = new List<Button>
            {
                createBackupButton,
                restoreBackupButton,
                createPortableButton,
                restorePortableButton
            };

#if DEBUG
            Button generateGoodDataButton = WorkspaceSurfaceChrome.ToolbarButton("توليد بيانات جيدة", automationId: "Settings.QuickAction.GenerateGoodData");
            generateGoodDataButton.Click += (_, _) => _coordinator.GenerateGoodDevelopmentData(RefreshAfterDataReset);

            Button generateAdditionalDataButton = WorkspaceSurfaceChrome.ToolbarButton("توليد بيانات إضافية", automationId: "Settings.QuickAction.GenerateAdditionalData");
            generateAdditionalDataButton.Click += (_, _) => _coordinator.GenerateAdditionalDevelopmentData(RefreshAfterDataReset);

            actionButtons.Add(generateGoodDataButton);
            actionButtons.Add(generateAdditionalDataButton);
#endif

            foreach (Button button in actionButtons)
            {
                button.Height = 31;
                button.MinWidth = 0;
                button.Margin = new Thickness(3, 0, 3, 6);
                button.Padding = new Thickness(4, 0, 4, 0);
                button.FontSize = 9.5;
            }

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

            var actions = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 2,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Thickness(0, 9, 0, 0)
            };
            actions.Children.Add(createBackupButton);
            actions.Children.Add(restoreBackupButton);
            actions.Children.Add(createPortableButton);
            actions.Children.Add(restorePortableButton);
#if DEBUG
            actions.Children.Add(generateGoodDataButton);
            actions.Children.Add(generateAdditionalDataButton);
#endif
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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3.5, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.8, GridUnitType.Star) });
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

        private SettingPathItem? SelectedItem
            => (_list.SelectedItem as FrameworkElement)?.Tag as SettingPathItem;

        private void ApplyFilters()
        {
            _list.Items.Clear();
            string category = SettingsPathFilters.Normalize(_selectedCategoryFilter);
            UpdateMetricCardStates();
            SettingsWorkspaceFilterResult filtered = _dataService.BuildFilteredItems(
                _allItems,
                _searchInput.Text,
                category);
            IReadOnlyList<SettingPathItem> pageItems = _pager.Page(filtered.Items);
            foreach (SettingPathItem item in pageItems)
            {
                _list.Items.Add(BuildRow(item));
            }

            if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }

            ApplyMetrics(filtered.Metrics);
            _summary.Text = _pager.BuildSummary();
            UpdateDetails();
        }

        private FrameworkElement BuildRow(SettingPathItem item)
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
            actions.Children.Add(CreateRowButton("فتح", "Icon.View", item, OpenPath_Click));
            Grid.SetColumn(actions, 0);
            row.Children.Add(actions);

            row.Children.Add(BuildCell(item.StateLabel, 1, "TableCellCenter", item.StateBrush));
            row.Children.Add(BuildCell(item.Category, 2, "TableCellCenter", item.CategoryBrush));
            row.Children.Add(BuildPathCell(item.Path, 3));
            row.Children.Add(BuildCell(item.Label, 4, "TableCellRight"));
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

        private static TextBlock BuildPathCell(string text, int column)
        {
            var cell = BuildPathText();
            cell.Text = text;
            cell.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetColumn(cell, column);
            return cell;
        }

        private static Button CreateRowButton(string text, string iconKey, SettingPathItem item, RoutedEventHandler handler)
        {
            var button = new Button
            {
                Content = BuildRowButtonContent(text, iconKey),
                Tag = item,
                Style = WorkspaceSurfaceChrome.Style("RowButton")
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

        private void OpenPath_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
            _coordinator.OpenPath(SelectedItem);
        }

        private void SelectRowFromSender(object sender)
        {
            if (sender is not FrameworkElement element || element.Tag is not SettingPathItem item)
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

        private void UpdateDetails()
        {
            ApplyDetailState(_dataService.BuildDetailState(SelectedItem));
        }

        private void ApplyMetrics(SettingsWorkspaceMetrics metrics)
        {
            _allValue.Text = metrics.Total;
            _dataValue.Text = metrics.Data;
            _workflowValue.Text = metrics.Workflow;
            UpdateMetricCardStates();
        }

        private void ApplyDetailState(SettingsWorkspaceDetailState state)
        {
            _detailTitle.Text = state.Title;
            _detailSubtitle.Text = state.Subtitle;
            _detailStatusBadge.Text = state.BadgeText;
            _detailStatusBadge.Foreground = state.BadgeForeground;
            _detailStatusBadgeBorder.Background = state.BadgeBackground;
            _detailStatusBadgeBorder.BorderBrush = state.BadgeBorder;
            _detailState.Text = state.State;
            _detailState.Foreground = state.StateBrush;
            _detailAction.Text = state.Action;
            _detailPath.Text = state.Path;
            _detailOpenPath.Text = state.OpenPath;
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

        private static TextBlock BuildPathText()
        {
            return new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeights.Normal,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                TextAlignment = TextAlignment.Left,
                FlowDirection = FlowDirection.LeftToRight,
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

        private static Grid BuildReadOnlyDetailLine(string label, TextBlock value, string iconKey)
        {
            var grid = new Grid
            {
                MinHeight = 28,
                FlowDirection = FlowDirection.RightToLeft
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            labelPanel.Children.Add(CreateIcon(iconKey, "#94A3B8", 12));
            labelPanel.Children.Add(new Border { Width = 7 });
            labelPanel.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#94A3C8"),
                VerticalAlignment = VerticalAlignment.Center
            });
            grid.Children.Add(labelPanel);

            value.VerticalAlignment = VerticalAlignment.Center;
            value.TextAlignment = TextAlignment.Right;
            value.TextWrapping = TextWrapping.NoWrap;
            value.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(value, 2);
            grid.Children.Add(value);
            return grid;
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

        private sealed record SettingsMetricFilter(string CategoryFilter, string AccentHex);
    }
}
