using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        private readonly Action _resumeLastFile;
        private readonly Action<int, GuaranteeFileFocusArea, int?> _openGuaranteeContext;
        private readonly Action _showGuarantees;
        private readonly Action _showRequests;
        private readonly Action _showNotifications;
        private readonly Action _showReports;
        private readonly Action? _closeRequested;

        private readonly ListBox _list = new();
        private readonly TextBox _searchInput = new();
        private readonly ComboBox _scopeFilter = new();
        private readonly TextBlock _summary = BuildMutedText(12, FontWeights.SemiBold);
        private readonly TextBlock _guaranteeCountValue = BuildMetricValue(22);
        private readonly TextBlock _portfolioAmountValue = BuildMetricValue();
        private readonly TextBlock _pendingValue = BuildMetricValue();
        private readonly TextBlock _attentionValue = BuildMetricValue();
        private readonly TextBlock _detailTitle = BuildDetailValue(16, FontWeights.Bold);
        private readonly TextBlock _detailSubtitle = BuildMutedText(11, FontWeights.SemiBold);
        private readonly TextBlock _detailStatusBadge = BuildBadgeText();
        private readonly Border _detailStatusBadgeBorder = new();
        private readonly Image _detailBankLogo = new() { Width = 18, Height = 18 };
        private readonly TextBlock _detailBankText = BuildMutedText(12, FontWeights.SemiBold);
        private readonly TextBlock _detailAmountHeadline = BuildAmountHeadline();
        private readonly TextBlock _detailAmountCaption = BuildMutedText(11, FontWeights.Normal);
        private readonly TextBlock _detailCategory = BuildDetailValue(12, FontWeights.Bold);
        private readonly TextBlock _detailPriority = BuildDetailValue(12, FontWeights.Bold);
        private readonly TextBlock _detailReference = BuildDetailValue(12, FontWeights.SemiBold);
        private readonly TextBlock _detailDue = BuildDetailValue(12, FontWeights.SemiBold);
        private readonly TextBlock _detailWorkspace = BuildDetailValue(12, FontWeights.SemiBold);
        private readonly TextBlock _detailAction = BuildDetailValue(12, FontWeights.SemiBold);
        private readonly TextBlock _detailNote = BuildMutedText(11, FontWeights.Normal);
        private readonly Button _primaryActionButton = new();
        private readonly Button _openWorkspaceButton = new();
        private readonly Button _copyReferenceButton = new();
        private readonly Button _resumeLastFileButton = new();

        private List<Guarantee> _guarantees = new();
        private List<WorkflowRequestListItem> _pendingRequests = new();
        private List<DashboardWorkItem> _allItems = new();

        public DashboardWorkspaceSurface(
            Func<IReadOnlyList<Guarantee>> loadGuarantees,
            Func<IReadOnlyList<WorkflowRequestListItem>> loadPendingRequests,
            bool hasLastFile,
            string lastFileGuaranteeNo,
            string lastFileSummary,
            Action resumeLastFile,
            Action<int, GuaranteeFileFocusArea, int?> openGuaranteeContext,
            Action showGuarantees,
            Action showRequests,
            Action showNotifications,
            Action showReports,
            Action? closeRequested,
            string? initialSearchText = null)
        {
            _dataService = new DashboardWorkspaceDataService();
            _coordinator = new DashboardWorkspaceCoordinator();
            _loadGuarantees = loadGuarantees;
            _loadPendingRequests = loadPendingRequests;
            _hasLastFile = hasLastFile;
            _lastFileGuaranteeNo = lastFileGuaranteeNo;
            _lastFileSummary = lastFileSummary;
            _resumeLastFile = resumeLastFile;
            _openGuaranteeContext = openGuaranteeContext;
            _showGuarantees = showGuarantees;
            _showRequests = showRequests;
            _showNotifications = showNotifications;
            _showReports = showReports;
            _closeRequested = closeRequested;

            UiInstrumentation.Identify(this, "Dashboard.Workspace", "لوحة التحكم");
            UiInstrumentation.Identify(_searchInput, "Dashboard.SearchBox", "بحث لوحة التحكم");
            UiInstrumentation.Identify(_scopeFilter, "Dashboard.Filter.Scope", "نطاق لوحة التحكم");
            UiInstrumentation.Identify(_list, "Dashboard.Table.List", "قائمة أعمال لوحة التحكم");

            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = WorkspaceSurfaceChrome.BrushResource("Brush.Canvas");

            ConfigureActionButtons();
            Content = BuildLayout();
            ReloadData();
            ApplyInitialSearch(initialSearchText);
        }

        private void ApplyInitialSearch(string? initialSearchText)
        {
            if (!string.IsNullOrWhiteSpace(initialSearchText))
            {
                _searchInput.Text = initialSearchText.Trim();
            }
        }

        private void ConfigureActionButtons()
        {
            _primaryActionButton.Style = WorkspaceSurfaceChrome.Style("PrimaryButton");
            _primaryActionButton.FontSize = 9.5;
            _primaryActionButton.Click += (_, _) => OpenSelectedPrimaryAction();
            UiInstrumentation.Identify(_primaryActionButton, "Dashboard.Detail.PrimaryActionButton", "الخطوة التالية");

            _openWorkspaceButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            _openWorkspaceButton.FontSize = 9.5;
            _openWorkspaceButton.Click += (_, _) => OpenSelectedWorkspace();
            UiInstrumentation.Identify(_openWorkspaceButton, "Dashboard.Detail.OpenWorkspaceButton", "فتح المساحة");

            _resumeLastFileButton.Style = WorkspaceSurfaceChrome.Style(_hasLastFile ? "PrimaryButton" : "BaseButton");
            _resumeLastFileButton.FontSize = 9.5;
            _resumeLastFileButton.Content = _hasLastFile ? $"استئناف {_lastFileGuaranteeNo}" : "لا يوجد ملف حديث";
            _resumeLastFileButton.IsEnabled = _hasLastFile;
            _resumeLastFileButton.Click += (_, _) => _resumeLastFile();
            UiInstrumentation.Identify(_resumeLastFileButton, "Dashboard.Toolbar.ResumeLastFile", "استئناف آخر ملف");

            _copyReferenceButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            _copyReferenceButton.Content = "نسخ المرجع";
            _copyReferenceButton.FontSize = 9.5;
            _copyReferenceButton.Click += (_, _) => _coordinator.CopyReference(SelectedItem);
            UiInstrumentation.Identify(_copyReferenceButton, "Dashboard.Detail.CopyReferenceButton", "نسخ المرجع");
        }

        private Grid BuildLayout()
        {
            return WorkspaceSurfaceChrome.BuildReferenceWorkspace(
                BuildToolbar(),
                BuildMetrics(),
                BuildTableSection(),
                BuildDetailPanel());
        }

        private Grid BuildToolbar()
        {
            var toolbar = new Grid { FlowDirection = FlowDirection.LeftToRight };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetColumn(_resumeLastFileButton, 0);
            toolbar.Children.Add(_resumeLastFileButton);

            var openGuaranteesButton = WorkspaceSurfaceChrome.ToolbarButton("فتح الضمانات", primary: true, automationId: "Dashboard.Toolbar.OpenGuarantees");
            openGuaranteesButton.Click += (_, _) => _showGuarantees();
            Grid.SetColumn(openGuaranteesButton, 2);
            toolbar.Children.Add(openGuaranteesButton);

            var refreshButton = WorkspaceSurfaceChrome.ToolbarButton("تحديث", automationId: "Dashboard.Toolbar.Refresh");
            refreshButton.Click += (_, _) => ReloadData();
            Grid.SetColumn(refreshButton, 4);
            toolbar.Children.Add(refreshButton);

            _scopeFilter.Style = WorkspaceSurfaceChrome.Style("FilterComboBox");
            _scopeFilter.Items.Add("أعمال اليوم");
            _scopeFilter.Items.Add("طلبات معلقة");
            _scopeFilter.Items.Add("منتهية تحتاج متابعة");
            _scopeFilter.Items.Add("قريبة الانتهاء");
            _scopeFilter.SelectedIndex = 0;
            _scopeFilter.SelectionChanged += (_, _) => ApplyFilters();
            Grid.SetColumn(_scopeFilter, 6);
            toolbar.Children.Add(_scopeFilter);

            _searchInput.TextChanged += (_, _) => ApplyFilters();
            var searchBox = WorkspaceSurfaceChrome.ToolbarSearchBox(_searchInput, "ابحث باسم المستفيد أو البنك أو المرجع...");
            Grid.SetColumn(searchBox, 8);
            toolbar.Children.Add(searchBox);
            return toolbar;
        }

        private System.Windows.Controls.Primitives.UniformGrid BuildMetrics()
        {
            var metrics = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 4
            };
            metrics.Children.Add(BuildMetricCard("آخر ملف", _guaranteeCountValue, "#2563EB"));
            metrics.Children.Add(BuildMetricCard("أعمال حرجة", _portfolioAmountValue, "#EF4444"));
            metrics.Children.Add(BuildMetricCard("طلبات معلقة", _pendingValue, "#E09408"));
            metrics.Children.Add(BuildMetricCard("قريب الانتهاء", _attentionValue, "#0F172A"));
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
            AddHeader(inner, "الإجراءات", 0, false);
            AddHeader(inner, "الفئة", 1, false);
            AddHeader(inner, "الأولوية", 2, false);
            AddHeader(inner, "الموعد", 3, false);
            AddHeader(inner, "القيمة", 4, false);
            AddHeader(inner, "البنك", 5, true);
            AddHeader(inner, "العنصر", 6, true);
            AddHeader(inner, "المرجع", 7, true);
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
            _detailStatusBadgeBorder.Margin = new Thickness(0, 0, 0, 12);
            _detailStatusBadgeBorder.Child = _detailStatusBadge;

            return new StackPanel
            {
                Margin = new Thickness(16, 14, 16, 14),
                Children =
                {
                    BuildDetailHeader(),
                    BuildDashboardTitleRow(),
                    _detailSubtitle,
                    _detailStatusBadgeBorder,
                    BuildBankRow(),
                    _detailAmountHeadline,
                    _detailAmountCaption,
                    new Border { Height = 1, Background = WorkspaceSurfaceChrome.BrushFrom("#EDF2F7"), Margin = new Thickness(0, 13, 0, 12) },
                    WorkspaceSurfaceChrome.InfoLine("الفئة", _detailCategory),
                    WorkspaceSurfaceChrome.InfoLine("الأولوية", _detailPriority),
                    WorkspaceSurfaceChrome.InfoLine("المرجع", _detailReference),
                    WorkspaceSurfaceChrome.InfoLine("الموعد", _detailDue),
                    WorkspaceSurfaceChrome.InfoLine("المساحة", _detailWorkspace),
                    WorkspaceSurfaceChrome.InfoLine("الإجراء التالي", _detailAction),
                    BuildInfoBlock("ملاحظة تشغيلية", _detailNote)
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
                Text = "لوحة التحكم",
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
            UiInstrumentation.Identify(closeButton, "Dashboard.Detail.CloseButton", "إغلاق لوحة التحكم");
            Grid.SetColumn(closeButton, 1);
            grid.Children.Add(closeButton);
            return grid;
        }

        private UIElement BuildDashboardTitleRow()
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft
            };
            row.Children.Add(CreateIcon("Icon.Dashboard", "#64748B", 14));
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
            row.Children.Add(_detailBankText);
            _detailBankLogo.Margin = new Thickness(7, 0, 0, 0);
            row.Children.Add(_detailBankLogo);
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
                Text = "الخطوة التالية",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text")
            });

            var actions = new Grid { FlowDirection = FlowDirection.LeftToRight, Margin = new Thickness(0, 9, 0, 0) };
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.Children.Add(_primaryActionButton);
            Grid.SetColumn(_copyReferenceButton, 2);
            actions.Children.Add(_copyReferenceButton);
            Grid.SetColumn(_openWorkspaceButton, 4);
            actions.Children.Add(_openWorkspaceButton);
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
            string selectedScope = _scopeFilter.SelectedItem as string ?? "أعمال اليوم";
            DashboardWorkspaceFilterResult filtered = _dataService.BuildFilteredItems(
                _allItems,
                _searchInput.Text,
                selectedScope,
                _hasLastFile,
                _lastFileGuaranteeNo,
                _guarantees,
                _pendingRequests);

            foreach (DashboardWorkItem item in filtered.Items)
            {
                _list.Items.Add(BuildRow(item));
            }

            if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }

            ApplyMetrics(filtered.Metrics);
            _summary.Text = filtered.Summary;
            UpdateDetails();
        }

        private FrameworkElement BuildRow(DashboardWorkItem item)
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
            actions.Children.Add(CreateRowButton("نسخ", "Icon.Document", item, CopyReference_Click));
            actions.Children.Add(CreateRowButton("عرض", "Icon.View", item, SelectRow_Click));
            actions.Children.Add(CreateRowButton("انتقال", item.WorkspaceIconKey, item, OpenWorkspace_Click));
            Grid.SetColumn(actions, 0);
            row.Children.Add(actions);

            row.Children.Add(BuildCell(item.CategoryLabel, 1, "TableCellCenter", item.CategoryBrush));
            row.Children.Add(BuildCell(item.PriorityLabel, 2, "TableCellCenter", item.PriorityBrush));
            row.Children.Add(BuildCell(item.DueLabel, 3, "TableCellCenter"));
            row.Children.Add(BuildAmountCell(item.AmountDisplay, 4));
            row.Children.Add(BuildBankCell(item, 5));
            row.Children.Add(BuildCell(item.Title, 6, "TableCellRight"));
            row.Children.Add(BuildCell(item.Reference, 7, "TableCellRight"));
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

        private void SelectRow_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
        }

        private void CopyReference_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
            _coordinator.CopyReference(SelectedItem);
        }

        private void OpenWorkspace_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
            OpenSelectedWorkspace();
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

        private void OpenSelectedWorkspace()
        {
            _coordinator.OpenSelectedWorkspace(
                SelectedItem,
                _showGuarantees,
                _showRequests,
                _showNotifications,
                _showReports);
        }

        private void OpenSelectedPrimaryAction()
        {
            _coordinator.RunPrimaryAction(
                SelectedItem,
                _openGuaranteeContext,
                _showGuarantees);
        }

        private void UpdateDetails()
        {
            ApplyDetailState(_dataService.BuildDetailState(
                SelectedItem,
                _hasLastFile,
                _lastFileGuaranteeNo,
                _lastFileSummary));
        }

        private void ApplyMetrics(DashboardWorkspaceMetrics metrics)
        {
            _guaranteeCountValue.Text = metrics.LastFile;
            _portfolioAmountValue.Text = metrics.CriticalWork;
            _pendingValue.Text = metrics.PendingRequests;
            _attentionValue.Text = metrics.ExpiringSoon;
        }

        private void ApplyDetailState(DashboardWorkspaceDetailState state)
        {
            _detailTitle.Text = state.Title;
            _detailSubtitle.Text = state.Subtitle;
            _detailStatusBadge.Text = state.BadgeText;
            _detailStatusBadge.Foreground = state.BadgeForeground;
            _detailStatusBadgeBorder.Background = state.BadgeBackground;
            _detailStatusBadgeBorder.BorderBrush = state.BadgeBorder;
            _detailBankLogo.Source = state.BankLogo;
            _detailBankText.Text = state.BankText;
            _detailAmountHeadline.Text = state.AmountHeadline;
            _detailAmountCaption.Text = state.AmountCaption;
            _detailCategory.Text = state.Category;
            _detailPriority.Text = state.Priority;
            _detailReference.Text = state.Reference;
            _detailDue.Text = state.Due;
            _detailWorkspace.Text = state.Workspace;
            _detailAction.Text = state.Action;
            _detailNote.Text = state.Note;
            _primaryActionButton.Content = state.PrimaryActionButtonLabel;
            _openWorkspaceButton.Content = state.WorkspaceButtonLabel;
            _primaryActionButton.IsEnabled = state.CanRunPrimaryAction;
            _openWorkspaceButton.IsEnabled = state.CanOpenWorkspace;
            _copyReferenceButton.IsEnabled = state.CanRunPrimaryAction;
        }

        private static FrameworkElement BuildInfoBlock(string title, TextBlock value)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#94A3C8")
            });
            value.Margin = new Thickness(0, 5, 0, 0);
            panel.Children.Add(value);
            return panel;
        }

        private static TextBlock BuildMetricValue(double fontSize = 27)
        {
            return new TextBlock
            {
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A"),
                Margin = new Thickness(0, 4, 0, 0),
                TextAlignment = TextAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis
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
                FlowDirection = FlowDirection.LeftToRight
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
