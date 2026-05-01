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
        private readonly ReferenceTablePagerController _pager;

        private readonly ListBox _list = new();
        private readonly TextBox _searchInput = new();
        private readonly Button _allWorkScopeButton = new();
        private readonly Button _pendingRequestsScopeButton = new();
        private readonly Button _expiryFollowUpsScopeButton = new();
        private readonly ComboBox _expiryFollowUpFilter = new();
        private readonly TextBlock _summary = BuildMutedText(12, FontWeights.SemiBold);
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
        private readonly TextBlock _detailPanelHeading = BuildSectionHeading();
        private readonly TextBlock _detailActionsHeading = BuildSectionHeading(12);
        private readonly TextBlock _detailTitle = BuildDetailValue(16, FontWeights.Bold);
        private readonly TextBlock _detailStatusBadge = BuildBadgeText();
        private readonly Border _detailStatusBadgeBorder = new();
        private readonly Image _detailBankLogo = new() { Width = 18, Height = 18 };
        private readonly TextBlock _detailBankText = BuildMutedText(12, FontWeights.SemiBold);
        private readonly TextBlock _detailAmountHeadline = BuildAmountHeadline();
        private readonly TextBlock _detailAmountCaption = BuildMutedText(11, FontWeights.Normal);
        private readonly TextBlock _detailDue = BuildDetailValue(12, FontWeights.SemiBold);
        private readonly TextBlock _detailExpiry = BuildDetailValue(12, FontWeights.SemiBold);
        private readonly TextBlock _detailAction = BuildDetailValue(12, FontWeights.SemiBold);
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
        private bool _isUpdatingFilters;

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
            _pager = new ReferenceTablePagerController("Dashboard", "عنصر عمل", 10, ApplyFilters);

            UiInstrumentation.Identify(this, "Dashboard.Workspace", "اليوم");
            UiInstrumentation.Identify(_searchInput, "Dashboard.SearchBox", "بحث اليوم");
            UiInstrumentation.Identify(_allWorkScopeButton, "Dashboard.Filter.Scope.AllWork", DashboardScopeFilters.AllWork);
            UiInstrumentation.Identify(_pendingRequestsScopeButton, "Dashboard.Filter.Scope.PendingRequests", DashboardScopeFilters.PendingRequests);
            UiInstrumentation.Identify(_expiryFollowUpsScopeButton, "Dashboard.Filter.Scope.ExpiryFollowUps", DashboardScopeFilters.ExpiryFollowUps);
            UiInstrumentation.Identify(_expiryFollowUpFilter, "Dashboard.Filter.ExpiryFollowUpKind", "نوع متابعة الانتهاء");
            UiInstrumentation.Identify(_list, "Dashboard.Table.List", "قائمة أعمال اليوم");

            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
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
            _primaryActionButton.FontSize = 9.5;
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
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            UIElement scopeButtons = BuildScopeButtons();
            Grid.SetColumn(scopeButtons, 0);
            toolbar.Children.Add(scopeButtons);

            _expiryFollowUpFilter.Style = WorkspaceSurfaceChrome.Style("FilterComboBox");
            _expiryFollowUpFilter.Width = 150;
            _expiryFollowUpFilter.Items.Add(DashboardExpiryFollowUpFilters.All);
            _expiryFollowUpFilter.Items.Add(DashboardExpiryFollowUpFilters.Expired);
            _expiryFollowUpFilter.Items.Add(DashboardExpiryFollowUpFilters.ExpiringSoon);
            _expiryFollowUpFilter.SelectedIndex = 0;
            _expiryFollowUpFilter.Visibility = Visibility.Collapsed;
            _expiryFollowUpFilter.SelectionChanged += (_, _) =>
            {
                if (_isUpdatingFilters)
                {
                    return;
                }

                _pager.ResetToFirstPage();
                ApplyFilters();
            };
            Grid.SetColumn(_expiryFollowUpFilter, 2);
            toolbar.Children.Add(_expiryFollowUpFilter);

            _searchInput.TextChanged += (_, _) =>
            {
                _pager.ResetToFirstPage();
                ApplyFilters();
            };
            var searchBox = WorkspaceSurfaceChrome.ToolbarSearchBox(_searchInput, "ابحث باسم المورد أو البنك أو رقم الضمان...");
            Grid.SetColumn(searchBox, 4);
            toolbar.Children.Add(searchBox);
            return toolbar;
        }

        private UIElement BuildScopeButtons()
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.LeftToRight
            };

            ConfigureScopeButton(_allWorkScopeButton, DashboardScopeFilters.AllWork);
            ConfigureScopeButton(_pendingRequestsScopeButton, DashboardScopeFilters.PendingRequests);
            ConfigureScopeButton(_expiryFollowUpsScopeButton, DashboardScopeFilters.ExpiryFollowUps);

            panel.Children.Add(_expiryFollowUpsScopeButton);
            panel.Children.Add(_pendingRequestsScopeButton);
            panel.Children.Add(_allWorkScopeButton);
            WorkspaceSurfaceChrome.ApplyToolbarGroupSpacing(
                _expiryFollowUpsScopeButton,
                _pendingRequestsScopeButton,
                _allWorkScopeButton);
            UpdateScopeButtons();
            return panel;
        }

        private void ConfigureScopeButton(Button button, string scope)
        {
            button.Content = scope;
            button.Tag = scope;
            button.Height = 36;
            button.MinWidth = 138;
            button.FontSize = 11;
            button.FlowDirection = FlowDirection.RightToLeft;
            button.Click += (_, _) => SelectScopeFilter(scope);
            AutomationProperties.SetName(button, scope);
        }

        private void SelectScopeFilter(string scopeFilter, bool resetPage = true, bool apply = true)
        {
            string normalizedScope = DashboardScopeFilters.Normalize(scopeFilter);
            bool changed = !string.Equals(_selectedScopeFilter, normalizedScope, StringComparison.Ordinal);
            _selectedScopeFilter = normalizedScope;
            UpdateScopeButtons();
            UpdateExpiryFollowUpFilterVisibility(_selectedScopeFilter);

            if (resetPage)
            {
                _pager.ResetToFirstPage();
            }

            if (apply && (changed || resetPage))
            {
                ApplyFilters();
            }
        }

        private void UpdateScopeButtons()
        {
            ApplyScopeButtonState(_allWorkScopeButton, DashboardScopeFilters.AllWork);
            ApplyScopeButtonState(_pendingRequestsScopeButton, DashboardScopeFilters.PendingRequests);
            ApplyScopeButtonState(_expiryFollowUpsScopeButton, DashboardScopeFilters.ExpiryFollowUps);
        }

        private void ApplyScopeButtonState(Button button, string scope)
        {
            bool selected = string.Equals(_selectedScopeFilter, scope, StringComparison.Ordinal);
            button.Style = WorkspaceSurfaceChrome.Style(selected ? "PrimaryButton" : "BaseButton");
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
                "#3B82F6",
                _guideTitle,
                _guidePrimary,
                _guideSecondary,
                _guideActionButton,
                () => RunGuidanceAction(_guideActionButton.Tag as DashboardGuidanceCard),
                "Dashboard.Guidance.GuideCard",
                "دليل اليوم الذكي"));
            strip.Children.Add(BuildGuidanceCard(
                "Icon.Lightbulb",
                "#EAB308",
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
            Grid.SetColumn(row, 1);
            grid.Children.Add(row);
            return grid;
        }

        private UIElement BuildDashboardTitleRow()
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0)
            };
            row.Children.Add(CreateIcon("Icon.User", "#94A3B8", 14));
            _detailTitle.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(_detailTitle);
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
            row.Children.Add(_detailBankLogo);
            _detailBankText.Margin = new Thickness(8, 0, 0, 0);
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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.6, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.65, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.6, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.45, GridUnitType.Star) });
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
            string expiryFollowUpFilter = _expiryFollowUpFilter.SelectedItem as string ?? DashboardExpiryFollowUpFilters.All;
            UpdateScopeButtons();
            UpdateExpiryFollowUpFilterVisibility(selectedScope);
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

            IReadOnlyList<DashboardWorkItem> pageItems = _pager.Page(filtered.Items);
            foreach (DashboardWorkItem item in pageItems)
            {
                _list.Items.Add(BuildRow(item, selectedScope));
            }

            if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }

            ApplyMetrics(filtered.Metrics);
            ApplyGuidanceState(_dataService.BuildGuidanceState(_allItems, _guarantees, _pendingRequests));
            _summary.Text = _pager.BuildSummary();
            UpdateDetails();
        }

        private void SelectExpiryFollowUpFilter(string filter, bool apply = true)
        {
            string normalizedFilter = DashboardExpiryFollowUpFilters.Normalize(filter);
            _isUpdatingFilters = true;
            try
            {
                bool matched = false;
                foreach (object item in _expiryFollowUpFilter.Items)
                {
                    if (string.Equals(item as string, normalizedFilter, StringComparison.Ordinal))
                    {
                        _expiryFollowUpFilter.SelectedItem = item;
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    _expiryFollowUpFilter.SelectedItem = DashboardExpiryFollowUpFilters.All;
                }
            }
            finally
            {
                _isUpdatingFilters = false;
            }

            if (apply)
            {
                _pager.ResetToFirstPage();
                ApplyFilters();
            }
        }

        private void UpdateExpiryFollowUpFilterVisibility(string selectedScope)
        {
            bool isExpiryFollowUpScope = IsExpiryFollowUpScope(selectedScope);
            _expiryFollowUpFilter.Visibility = isExpiryFollowUpScope ? Visibility.Visible : Visibility.Collapsed;
            _expiryFollowUpFilter.IsEnabled = isExpiryFollowUpScope;
        }

        private FrameworkElement BuildRow(DashboardWorkItem item, string selectedScope)
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
            actions.Children.Add(CreateRowButton("عرض", "Icon.View", item, OpenRow_Click));
            Grid.SetColumn(actions, 0);
            row.Children.Add(actions);

            if (IsExpiryFollowUpScope(selectedScope))
            {
                row.Children.Add(BuildCell(item.PriorityLabel, 1, "TableCellCenter", item.PriorityBrush));
                row.Children.Add(BuildCell(item.DueDetail, 2, "TableCellCenter", item.PriorityBrush));
                row.Children.Add(BuildCell(item.DueLabel, 3, "TableCellCenter"));
            }
            else
            {
                row.Children.Add(BuildCell(item.CategoryLabel, 1, "TableCellCenter", item.CategoryBrush));
                row.Children.Add(BuildCell(item.PriorityLabel, 2, "TableCellCenter", item.PriorityBrush));
                row.Children.Add(BuildCell(item.DueLabel, 3, "TableCellCenter"));
            }

            row.Children.Add(BuildAmountCell(item.AmountDisplay, 4));
            row.Children.Add(BuildBankCell(item, 5));
            row.Children.Add(BuildCell(item.Title, 6, "TableCellRight"));
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

            AddHeader(_tableHeaderInner, "الإجراءات", 0, false);
            if (IsExpiryFollowUpScope(selectedScope))
            {
                AddHeader(_tableHeaderInner, "المستوى", 1, false);
                AddHeader(_tableHeaderInner, "الأيام", 2, false);
                AddHeader(_tableHeaderInner, "تاريخ الانتهاء", 3, false);
            }
            else
            {
                AddHeader(_tableHeaderInner, "الفئة", 1, false);
                AddHeader(_tableHeaderInner, "الأولوية", 2, false);
                AddHeader(_tableHeaderInner, "الموعد", 3, false);
            }

            AddHeader(_tableHeaderInner, "القيمة", 4, false);
            AddHeader(_tableHeaderInner, "البنك", 5, true);
            AddHeader(_tableHeaderInner, "العنصر", 6, true);
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
                Style = WorkspaceSurfaceChrome.Style("TableCellCenter"),
                FlowDirection = FlowDirection.LeftToRight
            };
            Grid.SetColumn(cell, column);
            return cell;
        }

        private static UIElement BuildBankCell(DashboardWorkItem item, int column)
        {
            var bankCell = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            bankCell.Children.Add(new Image
            {
                Source = item.BankLogo,
                Width = 16,
                Height = 16
            });
            bankCell.Children.Add(new Border { Width = 8 });
            bankCell.Children.Add(new TextBlock
            {
                Text = item.Bank,
                Style = WorkspaceSurfaceChrome.Style("TableCellRight")
            });
            Grid.SetColumn(bankCell, column);
            return bankCell;
        }

        private static Button CreateRowButton(string text, string iconKey, DashboardWorkItem item, RoutedEventHandler handler)
        {
            var button = new Button
            {
                Content = BuildRowButtonContent(text, iconKey),
                Tag = item,
                Style = WorkspaceSurfaceChrome.Style("RowButton")
            };
            UiInstrumentation.Identify(
                button,
                UiInstrumentation.SanitizeAutomationKey($"Dashboard.RowAction.{text}", item.Reference),
                $"{text} | {item.Reference}");
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
                    Child = new Path
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

        private void OpenRow_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
            OpenSelectedPrimaryAction();
        }

        private void SelectRowFromSender(object sender)
        {
            if (sender is not FrameworkElement element || element.Tag is not DashboardWorkItem item)
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
            _pager.ResetToFirstPage();
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
            IReadOnlyList<DashboardMetricCard> cards = GetVisibleMetricCards(metrics);
            _metricsGrid.Columns = cards.Count;
            _metricsGrid.Children.Clear();

            foreach (DashboardMetricCard card in cards)
            {
                _metricsGrid.Children.Add(WorkspaceSurfaceChrome.MetricCard(card.Label, card.Value, card.AccentHex));
            }

            WorkspaceSurfaceChrome.ApplyMetricCardSpacing(_metricsGrid);
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
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
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
                FontSize = 11.3,
                FontWeight = FontWeights.SemiBold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#1F2937"),
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
                FontSize = 10.3,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B"),
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
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#2563EB"),
                Margin = new Thickness(0, 7, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                FlowDirection = FlowDirection.RightToLeft
            };
        }

        private static IReadOnlyList<DashboardMetricCard> GetVisibleMetricCards(DashboardWorkspaceMetrics metrics)
        {
            if (string.Equals(metrics.First.Label, "آخر ضمان", StringComparison.Ordinal))
            {
                return new[] { metrics.Second, metrics.Third, metrics.Fourth };
            }

            return new[] { metrics.First, metrics.Second, metrics.Third, metrics.Fourth };
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
                FontWeight = FontWeights.SemiBold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#94A3C8"),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static TextBlock BuildDetailValue(double size, FontWeight weight)
        {
            return new TextBlock
            {
                FontSize = size,
                FontWeight = weight,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A"),
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap
            };
        }

        private static TextBlock BuildSectionHeading(double size = 16)
        {
            return new TextBlock
            {
                FontSize = size,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text")
            };
        }

        private static TextBlock BuildMutedText(double size, FontWeight weight)
        {
            return new TextBlock
            {
                FontSize = size,
                FontWeight = weight,
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

        private static TextBlock BuildAmountHeadline()
        {
            return new TextBlock
            {
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A"),
                Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                TextAlignment = TextAlignment.Right
            };
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
                    Stroke = WorkspaceSurfaceChrome.BrushFrom(color),
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                }
            };
        }

    }
}
