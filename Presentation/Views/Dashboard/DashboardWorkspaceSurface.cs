using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Automation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using GuaranteeManager.Models;

namespace GuaranteeManager
{
    public sealed class DashboardWorkspaceSurface : UserControl
    {
        private readonly DashboardWorkspaceDataService _dataService;
        private readonly DashboardWorkspaceCoordinator _coordinator;
        private readonly Func<IReadOnlyList<Guarantee>> _loadGuarantees;
        private readonly Func<IReadOnlyList<WorkflowRequestListItem>> _loadPendingRequests;
        private readonly bool _hasLastFile;
        private readonly string _lastFileGuaranteeNo;
        private readonly string _lastFileSummary;
        private readonly Action<int, GuaranteeFocusArea, int?> _openGuaranteeContext;
        private readonly Action _showGuarantees;

        private readonly ListBox _list = new();
        private readonly TextBox _searchInput = new();
        private readonly TextBlock _summary = BuildMutedText(12, FontWeights.Medium);
        private readonly Grid _tableHeaderInner = new();
        private readonly System.Windows.Controls.Primitives.UniformGrid _metricsGrid = new();
        private readonly TextBlock _guideTitle = BuildInsightTitle();
        private readonly TextBlock _guidePrimary = BuildInsightPrimary();
        private readonly TextBlock _guideSecondary = BuildInsightSecondary();
        private readonly Button _guideActionButton = BuildInsightActionButton();
        private readonly TextBlock _recommendationTitle = BuildInsightTitle();
        private readonly TextBlock _recommendationPrimary = BuildInsightPrimary();
        private readonly TextBlock _recommendationSecondary = BuildInsightSecondary();
        private readonly Button _recommendationActionButton = BuildInsightActionButton();
        private readonly TextBlock _detailPanelHeading = BuildPrimaryHeaderTitle();
        private readonly TextBlock _detailActionsHeading = BuildSectionHeading(12);
        private readonly TextBlock _detailTitle = BuildDashboardSupplierText();
        private readonly TextBlock _detailStatusBadge = BuildBadgeText();
        private readonly Border _detailStatusBadgeBorder = new();
        private readonly Image _detailBankLogo = new() { Width = 17, Height = 17 };
        private readonly TextBlock _detailBankText = BuildMutedText(12, FontWeights.Medium);
        private readonly TextBlock _detailAmountHeadline = BuildAmountHeadline();
        private readonly TextBlock _detailAmountCaption = BuildMutedText(11, FontWeights.Normal);
        private readonly TextBlock _detailDue = BuildDetailValue(12, FontWeights.Medium);
        private readonly TextBlock _detailExpiry = BuildDetailValue(12, FontWeights.Medium);
        private readonly TextBlock _detailAction = BuildDetailValue(12, FontWeights.Medium);
        private readonly TextBlock _detailNote = BuildMutedText(11, FontWeights.Normal);
        private readonly TextBlock _detailDueLabel = BuildInfoLabel("الموعد");
        private readonly TextBlock _detailExpiryLabel = BuildInfoLabel("تاريخ الانتهاء");
        private readonly TextBlock _detailActionLabel = BuildInfoLabel("الإجراء التالي");
        private readonly TextBlock _detailNoteLabel = BuildInfoLabel("ملاحظة تشغيلية");
        private readonly Button _primaryActionButton = new();

        private List<Guarantee> _guarantees = new();
        private List<WorkflowRequestListItem> _pendingRequests = new();
        private List<DashboardWorkItem> _allItems = new();
        private DashboardGuidanceState? _guidanceState;
        private FrameworkElement? _detailExpiryLine;
        private string _selectedScopeFilter = DashboardScopeFilters.AllWork;
        private string _selectedExpiryFollowUpFilter = DashboardExpiryFollowUpFilters.All;

        public DashboardWorkspaceSurface(
            Func<IReadOnlyList<Guarantee>> loadGuarantees,
            Func<IReadOnlyList<WorkflowRequestListItem>> loadPendingRequests,
            bool hasLastFile,
            string lastFileGuaranteeNo,
            string lastFileSummary,
            Action<int, GuaranteeFocusArea, int?> openGuaranteeContext,
            Action showGuarantees,
            string? initialSearchText = null,
            string? initialScopeFilter = null)
        {
            _dataService = new DashboardWorkspaceDataService();
            _coordinator = new DashboardWorkspaceCoordinator();
            _loadGuarantees = loadGuarantees;
            _loadPendingRequests = loadPendingRequests;
            _hasLastFile = hasLastFile;
            _lastFileGuaranteeNo = lastFileGuaranteeNo;
            _lastFileSummary = lastFileSummary;
            _openGuaranteeContext = openGuaranteeContext;
            _showGuarantees = showGuarantees;

            UiInstrumentation.Identify(this, "Dashboard.Workspace", "اليوم");
            UiInstrumentation.Identify(_searchInput, "Dashboard.SearchBox", "بحث اليوم");
            UiInstrumentation.Identify(_list, "Dashboard.Table.List", "قائمة أعمال اليوم");

            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = UiTypography.DefaultFontFamily;
            Background = WorkspaceSurfaceChrome.BrushResource("Brush.Canvas");

            ConfigureActionButtons();
            Content = BuildLayout();
            ReloadData();
            ApplyInitialState(initialSearchText, initialScopeFilter);
        }

        private void ApplyInitialState(string? initialSearchText, string? initialScopeFilter)
        {
            string normalizedScopeFilter = DashboardScopeFilters.Normalize(initialScopeFilter);
            SelectExpiryFollowUpFilter(DashboardExpiryFollowUpFilters.FromScope(initialScopeFilter), apply: false);

            if (!string.IsNullOrWhiteSpace(initialScopeFilter))
            {
                SelectScopeFilter(normalizedScopeFilter, resetPage: false, apply: false);
            }

            if (!string.IsNullOrWhiteSpace(initialSearchText))
            {
                _searchInput.Text = initialSearchText.Trim();
                return;
            }

            if (!string.IsNullOrWhiteSpace(initialScopeFilter))
            {
                ApplyFilters();
            }
        }

        private void ConfigureActionButtons()
        {
            _primaryActionButton.Style = WorkspaceSurfaceChrome.Style("PrimaryButton");
            _primaryActionButton.FontSize = 10;
            _primaryActionButton.Click += (_, _) => OpenSelectedPrimaryAction();
            UiInstrumentation.Identify(_primaryActionButton, "Dashboard.Detail.PrimaryActionButton", "الخطوة التالية");

        }

        private Grid BuildLayout()
        {
            return WorkspaceSurfaceChrome.BuildReferenceWorkspace(
                BuildToolbar(),
                BuildMetrics(),
                BuildTableSection(),
                BuildDetailPanel(),
                BuildGuidanceStrip());
        }

        private Grid BuildToolbar()
        {
            var toolbar = new Grid { FlowDirection = FlowDirection.LeftToRight };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _searchInput.TextChanged += (_, _) =>
            {
                ApplyFilters();
            };
            var searchBox = WorkspaceSurfaceChrome.ToolbarSearchBox(_searchInput, "ابحث باسم المورد أو البنك أو رقم الضمان...");
            Grid.SetColumn(searchBox, 0);
            toolbar.Children.Add(searchBox);
            return toolbar;
        }

        private void SelectScopeFilter(string scopeFilter, bool resetPage = true, bool apply = true)
        {
            string normalizedScope = DashboardScopeFilters.Normalize(scopeFilter);
            bool changed = !string.Equals(_selectedScopeFilter, normalizedScope, StringComparison.Ordinal);
            _selectedScopeFilter = normalizedScope;

            if (apply && (changed || resetPage))
            {
                ApplyFilters();
            }
        }

        private System.Windows.Controls.Primitives.UniformGrid BuildMetrics()
        {
            _metricsGrid.Columns = 3;
            return _metricsGrid;
        }

        private System.Windows.Controls.Primitives.UniformGrid BuildGuidanceStrip()
        {
            _guideActionButton.Click += RunGuidanceButton_Click;
            _recommendationActionButton.Click += RunGuidanceButton_Click;
            UiInstrumentation.Identify(_guideActionButton, "Dashboard.Guidance.GuideAction", "إجراء دليل اليوم الذكي");
            UiInstrumentation.Identify(_recommendationActionButton, "Dashboard.Guidance.RecommendationAction", "إجراء التوصيات التشغيلية");

            var strip = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 2,
                MinHeight = 92
            };

            strip.Children.Add(BuildGuidanceCard(
                "Icon.ShieldLock",
                "#2563EB",
                _guideTitle,
                _guidePrimary,
                _guideSecondary,
                _guideActionButton,
                () => RunGuidanceAction(_guideActionButton.Tag as DashboardGuidanceCard),
                "Dashboard.Guidance.GuideCard",
                "دليل اليوم الذكي"));
            strip.Children.Add(BuildGuidanceCard(
                "Icon.Lightbulb",
                "#E09408",
                _recommendationTitle,
                _recommendationPrimary,
                _recommendationSecondary,
                _recommendationActionButton,
                () => RunGuidanceAction(_recommendationActionButton.Tag as DashboardGuidanceCard),
                "Dashboard.Guidance.RecommendationCard",
                "توصيات تشغيلية"));
            WorkspaceSurfaceChrome.ApplyMetricCardSpacing(strip);
            return strip;
        }

        private Border BuildGuidanceCard(
            string iconKey,
            string accent,
            TextBlock title,
            TextBlock primary,
            TextBlock secondary,
            Button actionButton,
            Action action,
            string automationId,
            string automationName)
        {
            var card = WorkspaceSurfaceChrome.Card(new Thickness(14, 10, 14, 10));
            card.Margin = new Thickness(0);
            card.Cursor = Cursors.Hand;
            card.MouseLeftButtonUp += (_, e) =>
            {
                if (IsInsideButton(e.OriginalSource as DependencyObject))
                {
                    return;
                }

                action();
            };
            UiInstrumentation.Identify(card, automationId, automationName);

            var grid = new Grid
            {
                FlowDirection = FlowDirection.LeftToRight
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconFrame = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(19),
                BorderBrush = WorkspaceSurfaceChrome.BrushFrom(accent),
                BorderThickness = new Thickness(1.4),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = CreateIcon(iconKey, accent, 20)
            };
            Grid.SetColumn(iconFrame, 0);
            grid.Children.Add(iconFrame);

            var content = new Grid
            {
                FlowDirection = FlowDirection.LeftToRight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(54, 0, 0, 0)
            };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(title, 0);
            content.Children.Add(title);
            Grid.SetRow(primary, 1);
            content.Children.Add(primary);
            Grid.SetRow(secondary, 2);
            content.Children.Add(secondary);
            Grid.SetRow(actionButton, 3);
            content.Children.Add(actionButton);
            Grid.SetColumn(content, 0);
            Grid.SetColumnSpan(content, 2);
            grid.Children.Add(content);

            card.Child = grid;
            return card;
        }

        private static bool IsInsideButton(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is Button)
                {
                    return true;
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
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

            RefreshTableHeader(DashboardScopeFilters.AllWork);
            header.Children.Add(_tableHeaderInner);
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
            _detailAmountCaption.HorizontalAlignment = HorizontalAlignment.Left;
            _detailAmountCaption.TextAlignment = TextAlignment.Right;

            _detailExpiryLine = WorkspaceSurfaceChrome.DetailFactLine(_detailExpiryLabel, _detailExpiry, "Icon.History");

            return new StackPanel
            {
                Margin = new Thickness(16, 14, 16, 14),
                Children =
                {
                    BuildDetailHeader(),
                    BuildDashboardTitleRow(),
                    BuildBankRow(),
                    _detailAmountHeadline,
                    _detailAmountCaption,
                    new Border { Height = 1, Background = WorkspaceSurfaceChrome.BrushFrom("#EDF2F7"), Margin = new Thickness(0, 13, 0, 12) },
                    WorkspaceSurfaceChrome.DetailFactLine(_detailDueLabel, _detailDue, "Icon.Calendar"),
                    _detailExpiryLine,
                    WorkspaceSurfaceChrome.DetailFactLine(_detailActionLabel, _detailAction, "Icon.Extend"),
                    WorkspaceSurfaceChrome.DetailFactBlock(_detailNoteLabel, _detailNote, "Icon.Document")
                }
            };
        }

        private UIElement BuildDetailHeader()
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

            row.Children.Add(_detailPanelHeading);
            row.Children.Add(new Border { Width = 3 });
            row.Children.Add(BuildInlineCopyButton(
                "نسخ رقم الضمان",
                "Dashboard.Detail.CopyReference",
                () => _detailPanelHeading.Text));
            Grid.SetColumn(row, 1);
            grid.Children.Add(row);
            return grid;
        }

        private UIElement BuildDashboardTitleRow()
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft
            };
            row.Children.Add(CreateIcon("Icon.User", "Brush.Text.Muted", 14));
            row.Children.Add(_detailTitle);
            row.Children.Add(new Border { Width = 3 });
            row.Children.Add(BuildInlineCopyButton(
                "نسخ اسم المورد",
                "Dashboard.Detail.CopyTitle",
                () => _detailTitle.Text));
            return row;
        }

        private UIElement BuildBankRow()
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = new Thickness(0, 5, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _detailBankLogo.VerticalAlignment = VerticalAlignment.Center;
            RenderOptions.SetBitmapScalingMode(_detailBankLogo, BitmapScalingMode.HighQuality);
            row.Children.Add(_detailBankLogo);
            _detailBankText.Margin = new Thickness(7, 0, 0, 0);
            _detailBankText.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(_detailBankText);
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
            grid.Children.Add(_detailActionsHeading);

            _primaryActionButton.Margin = new Thickness(0, 9, 0, 0);
            _primaryActionButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetRow(_primaryActionButton, 1);
            grid.Children.Add(_primaryActionButton);

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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.75, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.18, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.55, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.35, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
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

        private DashboardWorkItem? SelectedItem
            => (_list.SelectedItem as FrameworkElement)?.Tag as DashboardWorkItem;

        private void ReloadData()
        {
            _guarantees = _loadGuarantees().ToList();
            _pendingRequests = _loadPendingRequests().ToList();
            _allItems = _dataService.BuildItems(_guarantees, _pendingRequests);
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            _list.Items.Clear();
            string selectedScope = _selectedScopeFilter;
            string expiryFollowUpFilter = _selectedExpiryFollowUpFilter;
            RefreshTableHeader(selectedScope);
            DashboardWorkspaceFilterResult filtered = _dataService.BuildFilteredItems(
                _allItems,
                _searchInput.Text,
                selectedScope,
                _hasLastFile,
                _lastFileGuaranteeNo,
                _guarantees,
                _pendingRequests,
                expiryFollowUpFilter);

            foreach (DashboardWorkItem item in filtered.Items)
            {
                _list.Items.Add(BuildRow(item, selectedScope));
            }

            if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }

            ApplyMetrics(filtered.Metrics);
            ApplyGuidanceState(_dataService.BuildGuidanceState(_allItems, _guarantees, _pendingRequests));
            _summary.Text = WorkspaceSurfaceChrome.BuildReferenceTableSummary(filtered.Items.Count, "عنصر عمل");
            UpdateDetails();
        }

        private void SelectExpiryFollowUpFilter(string filter, bool apply = true)
        {
            string normalizedFilter = DashboardExpiryFollowUpFilters.Normalize(filter);
            _selectedExpiryFollowUpFilter = normalizedFilter;

            if (apply)
            {
                ApplyFilters();
            }
        }

        private FrameworkElement BuildRow(DashboardWorkItem item, string selectedScope)
        {
            var row = CreateTableGrid();
            row.Tag = item;
            row.Height = 40;

            if (IsExpiryFollowUpScope(selectedScope))
            {
                row.Children.Add(BuildCell(item.PriorityLabel, 0, "TableCellCenter", item.PriorityBrush));
                row.Children.Add(BuildCell(item.DueDetail, 1, "TableCellCenter", item.PriorityBrush));
                row.Children.Add(BuildCell(item.DueLabel, 2, "TableCellCenter"));
            }
            else
            {
                row.Children.Add(BuildCell(item.CategoryLabel, 0, "TableCellCenter", item.CategoryBrush));
                row.Children.Add(BuildCell(item.PriorityLabel, 1, "TableCellCenter", item.PriorityBrush));
                row.Children.Add(BuildCell(item.DueLabel, 2, "TableCellCenter"));
            }

            row.Children.Add(BuildAmountCell(item.AmountDisplay, 3));
            row.Children.Add(BuildBankCell(item, 4));
            row.Children.Add(BuildCell(item.RequiredLabel, 5, "TableCellRight"));
            row.Children.Add(BuildCell(item.Supplier, 6, "TableCellRight"));
            row.Children.Add(BuildCell(item.Reference, 7, "TableCellRight"));
            return row;
        }

        private void RefreshTableHeader(string selectedScope)
        {
            _tableHeaderInner.Children.Clear();
            _tableHeaderInner.ColumnDefinitions.Clear();
            _tableHeaderInner.Margin = new Thickness(9, 0, 9, 0);
            _tableHeaderInner.FlowDirection = FlowDirection.LeftToRight;
            foreach (ColumnDefinition definition in CreateTableGrid().ColumnDefinitions)
            {
                _tableHeaderInner.ColumnDefinitions.Add(new ColumnDefinition { Width = definition.Width });
            }

            if (IsExpiryFollowUpScope(selectedScope))
            {
                AddHeader(_tableHeaderInner, "المستوى", 0, false);
                AddHeader(_tableHeaderInner, "الأيام", 1, false);
                AddHeader(_tableHeaderInner, "تاريخ الانتهاء", 2, false);
            }
            else
            {
                AddHeader(_tableHeaderInner, "الفئة", 0, false);
                AddHeader(_tableHeaderInner, "الأولوية", 1, false);
                AddHeader(_tableHeaderInner, "الموعد", 2, false);
            }

            AddHeader(_tableHeaderInner, "المبلغ", 3, false);
            AddHeader(_tableHeaderInner, "البنك", 4, true);
            AddHeader(_tableHeaderInner, "المطلوب", 5, true);
            AddHeader(_tableHeaderInner, "المورد", 6, true);
            AddHeader(_tableHeaderInner, "رقم الضمان", 7, true);
        }

        private static bool IsExpiryFollowUpScope(string selectedScope)
            => string.Equals(
                DashboardScopeFilters.Normalize(selectedScope),
                DashboardScopeFilters.ExpiryFollowUps,
                StringComparison.Ordinal);

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

        private static TextBlock BuildAmountCell(string text, int column)
        {
            var cell = new TextBlock
            {
                Text = text,
                Style = WorkspaceSurfaceChrome.Style("TableAmountCell")
            };
            Grid.SetColumn(cell, column);
            return cell;
        }

        private static UIElement BuildBankCell(DashboardWorkItem item, int column)
        {
            return WorkspaceSurfaceChrome.BankTableCell(item.Bank, item.BankLogo, column, textMaxWidth: 118);
        }

        private void OpenSelectedPrimaryAction()
        {
            _coordinator.RunPrimaryAction(
                SelectedItem,
                _openGuaranteeContext,
                _showGuarantees);
        }

        private void RunGuidanceButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            RunGuidanceAction((sender as FrameworkElement)?.Tag as DashboardGuidanceCard);
        }

        private void RunGuidanceAction(DashboardGuidanceCard? card)
        {
            if (card == null)
            {
                return;
            }

            switch (card.ActionKind)
            {
                case DashboardGuidanceActionKind.OpenTopPriority:
                    _coordinator.RunPrimaryAction(card.TargetItem, _openGuaranteeContext, _showGuarantees);
                    break;
                case DashboardGuidanceActionKind.FilterPendingRequests:
                    ApplyGuidanceFilter(DashboardScopeFilters.PendingRequests, DashboardExpiryFollowUpFilters.All);
                    break;
                case DashboardGuidanceActionKind.FilterExpiredFollowUps:
                    ApplyGuidanceFilter(DashboardScopeFilters.ExpiryFollowUps, DashboardExpiryFollowUpFilters.Expired);
                    break;
                case DashboardGuidanceActionKind.FilterExpiringSoon:
                    ApplyGuidanceFilter(DashboardScopeFilters.ExpiryFollowUps, DashboardExpiryFollowUpFilters.ExpiringSoon);
                    break;
                case DashboardGuidanceActionKind.FilterAllWork:
                    ApplyGuidanceFilter(DashboardScopeFilters.AllWork, DashboardExpiryFollowUpFilters.All);
                    break;
                case DashboardGuidanceActionKind.OpenGuarantees:
                    _showGuarantees();
                    break;
            }
        }

        private void ApplyGuidanceFilter(string scopeFilter, string expiryFilter)
        {
            SelectExpiryFollowUpFilter(expiryFilter, apply: false);
            SelectScopeFilter(scopeFilter, resetPage: false, apply: false);
            ApplyFilters();
        }

        private void UpdateDetails()
        {
            ApplyDetailState(_dataService.BuildDetailState(
                SelectedItem,
                _selectedScopeFilter,
                _hasLastFile,
                _lastFileGuaranteeNo,
                _lastFileSummary));
        }

        private void ApplyMetrics(DashboardWorkspaceMetrics metrics)
        {
            IReadOnlyList<DashboardMetricCard> cards = metrics.Cards;
            _metricsGrid.Columns = cards.Count;
            _metricsGrid.Children.Clear();

            foreach (DashboardMetricCard card in cards)
            {
                _metricsGrid.Children.Add(BuildMetricFilterCard(card));
            }

            WorkspaceSurfaceChrome.ApplyMetricCardSpacing(_metricsGrid);
        }

        private Border BuildMetricFilterCard(DashboardMetricCard card)
        {
            Border border = WorkspaceSurfaceChrome.MetricCard(card.Label, card.Value, card.AccentHex);
            bool hasFilterTarget = !string.IsNullOrWhiteSpace(card.ScopeFilter);
            if (!hasFilterTarget)
            {
                return border;
            }

            string targetScope = DashboardScopeFilters.Normalize(card.ScopeFilter);
            string targetExpiry = DashboardExpiryFollowUpFilters.Normalize(card.ExpiryFilter);
            bool selected = string.Equals(_selectedScopeFilter, targetScope, StringComparison.Ordinal)
                && (!string.Equals(targetScope, DashboardScopeFilters.ExpiryFollowUps, StringComparison.Ordinal)
                    || string.Equals(_selectedExpiryFollowUpFilter, targetExpiry, StringComparison.Ordinal)
                    || string.Equals(targetExpiry, DashboardExpiryFollowUpFilters.All, StringComparison.Ordinal));

            border.Cursor = Cursors.Hand;
            border.Background = WorkspaceSurfaceChrome.BrushFrom(selected ? "#F8FBFF" : "#FFFFFF");
            border.BorderBrush = WorkspaceSurfaceChrome.BrushFrom(selected ? card.AccentHex : "#E3E9F2");
            border.BorderThickness = new Thickness(selected ? 2 : 1);
            border.MouseLeftButtonUp += (_, _) => ApplyGuidanceFilter(targetScope, targetExpiry);
            UiInstrumentation.Identify(
                border,
                UiInstrumentation.SanitizeAutomationKey("Dashboard.MetricFilter", card.Label),
                selected ? $"{card.Label} محدد" : $"فلتر {card.Label}");
            return border;
        }

        private void ApplyGuidanceState(DashboardGuidanceState state)
        {
            _guidanceState = state;
            ApplyGuidanceCard(_guideTitle, _guidePrimary, _guideSecondary, _guideActionButton, state.Guide);
            ApplyGuidanceCard(
                _recommendationTitle,
                _recommendationPrimary,
                _recommendationSecondary,
                _recommendationActionButton,
                state.Recommendation);
        }

        private static void ApplyGuidanceCard(
            TextBlock title,
            TextBlock primary,
            TextBlock secondary,
            Button actionButton,
            DashboardGuidanceCard card)
        {
            title.Text = card.Title;
            primary.Text = card.PrimaryText;
            secondary.Text = card.SecondaryText;
            actionButton.Content = card.ActionLabel;
            actionButton.Tag = card;
            AutomationProperties.SetName(actionButton, card.ActionLabel);
            AutomationProperties.SetHelpText(actionButton, card.SecondaryText);
        }

        private void ApplyDetailState(DashboardWorkspaceDetailState state)
        {
            _detailTitle.Text = state.Title;
            _detailStatusBadge.Text = state.BadgeText;
            _detailStatusBadge.Foreground = state.BadgeForeground;
            _detailStatusBadgeBorder.Background = state.BadgeBackground;
            _detailStatusBadgeBorder.BorderBrush = state.BadgeBorder;
            _detailBankLogo.Source = state.BankLogo;
            _detailBankText.Text = state.BankText;
            _detailAmountHeadline.Text = state.AmountHeadline;
            _detailAmountCaption.Text = state.AmountCaption;
            _detailDue.Text = state.Due;
            _detailExpiry.Text = state.Expiry;
            _detailAction.Text = state.Action;
            _detailNote.Text = state.Note;
            ApplyDetailLabels(state.DetailProfile);
            _detailPanelHeading.Text = state.Reference;
            _primaryActionButton.Content = state.PrimaryActionButtonLabel;
            AutomationProperties.SetName(_primaryActionButton, state.PrimaryActionButtonLabel);
            AutomationProperties.SetHelpText(_primaryActionButton, state.Action);
            AutomationProperties.SetItemStatus(_primaryActionButton, state.Reference);
            _primaryActionButton.IsEnabled = state.CanRunPrimaryAction;
        }

        private static TextBlock BuildInsightTitle()
        {
            return new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text"),
                HorizontalAlignment = HorizontalAlignment.Right,
                FlowDirection = FlowDirection.RightToLeft,
                TextAlignment = TextAlignment.Right
            };
        }

        private static TextBlock BuildInsightPrimary()
        {
            return new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text.Primary"),
                Margin = new Thickness(0, 5, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                FlowDirection = FlowDirection.RightToLeft,
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap
            };
        }

        private static TextBlock BuildInsightSecondary()
        {
            return new TextBlock
            {
                FontSize = 10,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text.Secondary"),
                Margin = new Thickness(0, 3, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                FlowDirection = FlowDirection.RightToLeft,
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap
            };
        }

        private static Button BuildInsightActionButton()
        {
            return new Button
            {
                Style = WorkspaceSurfaceChrome.Style("PlainLinkButton"),
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Link"),
                Margin = new Thickness(0, 7, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                FlowDirection = FlowDirection.RightToLeft
            };
        }

        private void ApplyDetailLabels(DashboardDetailProfile detailProfile)
        {
            if (detailProfile == DashboardDetailProfile.FollowUp)
            {
                _detailActionsHeading.Text = "قرار المتابعة";
                _detailDueLabel.Text = "المدة";
                _detailExpiryLabel.Text = "تاريخ الانتهاء";
                _detailActionLabel.Text = "الإجراء المقترح";
                _detailNoteLabel.Text = "لماذا ظهر اليوم؟";
                if (_detailExpiryLine != null)
                {
                    _detailExpiryLine.Visibility = Visibility.Visible;
                }

                return;
            }

            if (_detailExpiryLine != null)
            {
                _detailExpiryLine.Visibility = Visibility.Collapsed;
            }

            if (detailProfile == DashboardDetailProfile.PendingRequest)
            {
                _detailActionsHeading.Text = "خطوة الطلب التالية";
                _detailDueLabel.Text = "عمر الطلب";
                _detailActionLabel.Text = "الإجراء المقترح";
                _detailNoteLabel.Text = "لماذا ظهر اليوم؟";
                return;
            }

            _detailActionsHeading.Text = "الخطوة التالية";
            _detailDueLabel.Text = "الموعد";
            _detailActionLabel.Text = "الإجراء التالي";
            _detailNoteLabel.Text = "ملاحظة تشغيلية";
        }

        private static TextBlock BuildInfoLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text.Muted"),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static TextBlock BuildDetailValue(double size, FontWeight weight)
        {
            return new TextBlock
            {
                FontSize = size,
                FontWeight = weight,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text.Primary"),
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap
            };
        }

        private static TextBlock BuildSectionHeading(double size = 16)
        {
            return new TextBlock
            {
                FontSize = size,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text")
            };
        }

        private static TextBlock BuildPrimaryHeaderTitle()
        {
            return new TextBlock
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text"),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static TextBlock BuildDashboardSupplierText()
        {
            return new TextBlock
            {
                FontSize = 16,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text"),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static Button BuildInlineCopyButton(
            string tooltip,
            string automationId,
            Func<string> valueFactory)
        {
            var button = new Button
            {
                Style = WorkspaceSurfaceChrome.Style("IconOnlyButton"),
                Margin = new Thickness(0),
                ToolTip = tooltip,
                Content = CreateIcon("Icon.Copy", "Brush.Text.Secondary", 12)
            };
            button.Click += (_, e) =>
            {
                string value = valueFactory().Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    Clipboard.SetText(value);
                }

                e.Handled = true;
            };
            UiInstrumentation.Identify(button, automationId, tooltip);
            return button;
        }

        private static TextBlock BuildMutedText(double size, FontWeight weight)
        {
            return new TextBlock
            {
                FontSize = size,
                FontWeight = weight,
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

        private static TextBlock BuildAmountHeadline()
        {
            return new TextBlock { Style = WorkspaceSurfaceChrome.Style("FinancialAmountHeadline") };
        }

        private static Viewbox CreateIcon(string iconKey, string color, double size)
        {
            return new Viewbox
            {
                Width = size,
                Height = size,
                Child = new Path
                {
                    Data = (Geometry)Application.Current.FindResource(iconKey),
                    Stroke = WorkspaceSurfaceChrome.ResolveBrush(color),
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                }
            };
        }

    }
}
