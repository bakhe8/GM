using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GuaranteeManager.Models;
using GuaranteeManager.Services;

namespace GuaranteeManager
{
    public sealed class RequestsWorkspaceSurface : UserControl
    {
        private readonly Func<IReadOnlyList<WorkflowRequestListItem>> _loadRequests;
        private readonly Action? _closeRequested;
        private readonly RequestsWorkspaceDataService _dataService;
        private readonly RequestsWorkspaceCoordinator _coordinator;
        private readonly ListBox _list = new();
        private readonly TextBox _searchInput = new();
        private readonly ComboBox _statusFilter = new();
        private readonly TextBlock _totalValue = BuildMetricValue();
        private readonly TextBlock _pendingValue = BuildMetricValue();
        private readonly TextBlock _missingResponseValue = BuildMetricValue();
        private readonly TextBlock _closedValue = BuildMetricValue();
        private readonly TextBlock _summary = BuildMutedText(12, FontWeights.SemiBold);
        private readonly TextBlock _detailTitle = BuildDetailValue(16, FontWeights.Bold);
        private readonly TextBlock _detailSubtitle = BuildMutedText(11, FontWeights.SemiBold);
        private readonly TextBlock _detailStatusBadge = BuildBadgeText();
        private readonly Border _detailStatusBadgeBorder = new();
        private readonly Image _detailBankLogo = new() { Width = 17, Height = 17 };
        private readonly TextBlock _detailBankText = BuildMutedText(12, FontWeights.SemiBold);
        private readonly TextBlock _detailReference = BuildDetailValue(11.5, FontWeights.SemiBold);
        private readonly TextBlock _detailStatus = BuildDetailValue(12, FontWeights.Bold);
        private readonly TextBlock _detailCurrent = BuildDetailValue(12, FontWeights.SemiBold);
        private readonly TextBlock _detailRequested = BuildDetailValue(12, FontWeights.SemiBold);
        private readonly TextBlock _detailDates = BuildMutedText(11, FontWeights.SemiBold);
        private readonly TextBlock _detailNotes = BuildMutedText(11, FontWeights.Normal);
        private readonly TextBlock _detailResponse = BuildMutedText(11, FontWeights.Normal);
        private readonly Button _openGuaranteeButton = new();
        private readonly Button _letterButton = new();
        private readonly Button _primaryActionButton = new();
        private IReadOnlyList<WorkflowRequestListItem> _allRequests = Array.Empty<WorkflowRequestListItem>();

        public RequestsWorkspaceSurface(
            Func<IReadOnlyList<WorkflowRequestListItem>> loadRequests,
            IDatabaseService database,
            IWorkflowService workflow,
            IExcelService excel,
            Action<int>? onChanged,
            Action? closeRequested = null,
            string? initialSearchText = null)
        {
            _loadRequests = loadRequests;
            _closeRequested = closeRequested;
            _dataService = new RequestsWorkspaceDataService();
            _coordinator = new RequestsWorkspaceCoordinator(database, workflow, excel, onChanged);
            UiInstrumentation.Identify(this, "Requests.Workspace", "الطلبات");
            UiInstrumentation.Identify(_searchInput, "Requests.SearchBox", "بحث الطلبات");
            UiInstrumentation.Identify(_statusFilter, "Requests.Filter.Status", "حالة الطلبات");
            UiInstrumentation.Identify(_list, "Requests.Table.List", "قائمة الطلبات");

            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = WorkspaceSurfaceChrome.BrushResource("Brush.Canvas");
            RenderOptions.SetBitmapScalingMode(_detailBankLogo, BitmapScalingMode.HighQuality);

            ConfigureButtons();
            Content = BuildLayout();
            ReloadRequests();
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
            _openGuaranteeButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            _openGuaranteeButton.Content = "فتح ملف الضمان";
            _openGuaranteeButton.FontSize = 9.5;
            _openGuaranteeButton.Click += (_, _) => OpenCurrentGuarantee();
            UiInstrumentation.Identify(_openGuaranteeButton, "Requests.Detail.OpenGuaranteeButton", "فتح الضمان");

            _primaryActionButton.Style = WorkspaceSurfaceChrome.Style("PrimaryButton");
            _primaryActionButton.Content = "الإجراء التالي";
            _primaryActionButton.FontSize = 9.5;
            _primaryActionButton.Click += (_, _) => UseResponseAction();
            UiInstrumentation.Identify(_primaryActionButton, "Requests.Detail.PrimaryActionButton", "الإجراء التالي");

            _letterButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            _letterButton.Content = "فتح الخطاب";
            _letterButton.FontSize = 9.5;
            _letterButton.Click += (_, _) => OpenLetter();
            UiInstrumentation.Identify(_letterButton, "Requests.Detail.OpenLetterButton", "فتح الخطاب");
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
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var registerButton = WorkspaceSurfaceChrome.ToolbarButton("تسجيل رد البنك", primary: true, automationId: "Requests.Toolbar.RegisterResponse");
            registerButton.Click += (_, _) => RegisterSelectedResponse();
            Grid.SetColumn(registerButton, 0);
            toolbar.Children.Add(registerButton);

            var refreshButton = WorkspaceSurfaceChrome.ToolbarButton("تحديث", automationId: "Requests.Toolbar.Refresh");
            refreshButton.Click += (_, _) => ReloadRequests(SelectedRequest?.Id);
            Grid.SetColumn(refreshButton, 2);
            toolbar.Children.Add(refreshButton);

            var moreButton = CreateToolbarMenuButton("أدوات إضافية", "Requests.Toolbar.MoreTools");
            moreButton.ContextMenu = BuildOverflowMenu();
            Grid.SetColumn(moreButton, 4);
            toolbar.Children.Add(moreButton);

            _statusFilter.Style = WorkspaceSurfaceChrome.Style("FilterComboBox");
            _statusFilter.Items.Add(new RequestStatusFilterOption("كل الحالات", null));
            _statusFilter.Items.Add(new RequestStatusFilterOption("قيد الانتظار", RequestStatus.Pending));
            _statusFilter.Items.Add(new RequestStatusFilterOption("منفذ", RequestStatus.Executed));
            _statusFilter.Items.Add(new RequestStatusFilterOption("مرفوض", RequestStatus.Rejected));
            _statusFilter.Items.Add(new RequestStatusFilterOption("ملغى", RequestStatus.Cancelled));
            _statusFilter.Items.Add(new RequestStatusFilterOption("مسقط آليًا", RequestStatus.Superseded));
            _statusFilter.DisplayMemberPath = nameof(RequestStatusFilterOption.Label);
            _statusFilter.SelectedIndex = 0;
            _statusFilter.SelectionChanged += (_, _) => ApplyFilters();
            Grid.SetColumn(_statusFilter, 6);
            toolbar.Children.Add(_statusFilter);

            _searchInput.TextChanged += (_, _) => ApplyFilters();
            var searchBox = WorkspaceSurfaceChrome.ToolbarSearchBox(_searchInput, "ابحث برقم الضمان، البنك، المورد، أو نوع الطلب...");
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
            metrics.Children.Add(BuildMetricCard("إجمالي الطلبات", _totalValue, "#2563EB"));
            metrics.Children.Add(BuildMetricCard("قيد الانتظار", _pendingValue, "#E09408"));
            metrics.Children.Add(BuildMetricCard("بدون مستند رد", _missingResponseValue, "#3B82F6"));
            metrics.Children.Add(BuildMetricCard("مغلق", _closedValue, "#16A34A"));
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
            AddHeader(inner, "الحالة", 1, false);
            AddHeader(inner, "نوع الطلب", 2, false);
            AddHeader(inner, "القيمة المطلوبة", 3, false);
            AddHeader(inner, "القيمة الحالية", 4, false);
            AddHeader(inner, "تاريخ الطلب", 5, false);
            AddHeader(inner, "البنك", 6, true);
            AddHeader(inner, "المورد", 7, true);
            AddHeader(inner, "رقم الضمان", 8, true);
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
                    BuildRequestTitleRow(),
                    _detailStatusBadgeBorder,
                    BuildSupplierRow(),
                    BuildBankRow(),
                    new Border { Height = 1, Background = WorkspaceSurfaceChrome.BrushFrom("#EDF2F7"), Margin = new Thickness(0, 13, 0, 12) },
                    BuildInfoLine("المرجع", _detailReference),
                    BuildInfoLine("الإصدار", _detailStatus),
                    BuildInfoLine("القيمة الحالية", _detailCurrent),
                    BuildInfoLine("القيمة المطلوبة", _detailRequested),
                    BuildInfoLine("التواريخ", _detailDates),
                    BuildInfoBlock("ملاحظات الطلب", _detailNotes),
                    BuildInfoBlock("رد البنك", _detailResponse)
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
                Text = "تفاصيل الطلب",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text")
            });

            var closeButton = new Button
            {
                Width = 28,
                Height = 28,
                Content = "×",
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B")
            };
            closeButton.Click += (_, _) => _closeRequested?.Invoke();
            Grid.SetColumn(closeButton, 1);
            grid.Children.Add(closeButton);
            return grid;
        }

        private UIElement BuildRequestTitleRow()
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft
            };
            row.Children.Add(CreateIcon("Icon.Badge", "#64748B", 14));
            _detailTitle.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(_detailTitle);
            return row;
        }

        private UIElement BuildSupplierRow()
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft
            };
            row.Children.Add(CreateIcon("Icon.User", "#94A3B8", 14));
            _detailSubtitle.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(_detailSubtitle);
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

            Grid.SetRow(actions, 1);
            actions.Children.Add(_primaryActionButton);
            Grid.SetColumn(_letterButton, 2);
            actions.Children.Add(_letterButton);
            Grid.SetColumn(_openGuaranteeButton, 4);
            actions.Children.Add(_openGuaranteeButton);
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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3.55, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.45, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.45, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.45, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.55, GridUnitType.Star) });
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

        private RequestListDisplayItem? SelectedItem
            => (_list.SelectedItem as FrameworkElement)?.Tag as RequestListDisplayItem;

        private WorkflowRequest? SelectedRequest => SelectedItem?.Item.Request;

        private void ReloadRequests(int? requestIdToSelect = null)
        {
            _allRequests = _loadRequests();
            ApplyMetrics(_dataService.BuildMetrics(_allRequests));
            ApplyFilters(requestIdToSelect);
        }

        private void ApplyFilters(int? requestIdToSelect = null)
        {
            _list.Items.Clear();
            RequestStatus? status = (_statusFilter.SelectedItem as RequestStatusFilterOption)?.Status;
            RequestsWorkspaceFilterResult filtered = _dataService.BuildFilteredItems(
                _allRequests,
                _searchInput.Text,
                status);
            FrameworkElement? rowToSelect = null;

            foreach (RequestListDisplayItem item in filtered.Items)
            {
                FrameworkElement row = BuildRequestRow(item);
                _list.Items.Add(row);
                if (requestIdToSelect.HasValue && item.Item.Request.Id == requestIdToSelect.Value)
                {
                    rowToSelect = row;
                }
            }

            if (rowToSelect != null)
            {
                _list.SelectedItem = rowToSelect;
            }
            else if (_list.Items.Count > 0)
            {
                _list.SelectedIndex = 0;
            }

            _summary.Text = filtered.Summary;
            UpdateDetails();
        }

        private FrameworkElement BuildRequestRow(RequestListDisplayItem item)
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
            actions.Children.Add(CreateRowButton(item.QueueActionLabel, "Icon.Document", item, OpenResponseFromRow_Click, item.CanRunQueueAction, item.QueueActionHint));
            actions.Children.Add(CreateRowButton("خطاب", "Icon.Document", item, OpenLetterFromRow_Click, item.CanOpenLetter));
            actions.Children.Add(CreateRowButton("تفاصيل", "Icon.View", item, SelectRowFromButton_Click, true, "يعرض تفاصيل الطلب الحالي في اللوحة اليمنى."));
            actions.Children.Add(CreateMoreButton(item));
            Grid.SetColumn(actions, 0);
            row.Children.Add(actions);

            row.Children.Add(BuildCell(item.RequestStatus, 1, "TableCellCenter", item.RequestStatusBrush));
            row.Children.Add(BuildCell(item.RequestType, 2, "TableCellCenter"));
            row.Children.Add(BuildCell(item.RequestedValue, 3, "TableCellCenter"));
            row.Children.Add(BuildCell(item.CurrentValue, 4, "TableCellCenter"));
            row.Children.Add(BuildCell(item.RequestDate, 5, "TableCellCenter"));
            row.Children.Add(BuildBankCell(item, 6));
            row.Children.Add(BuildCell(item.Supplier, 7, "TableCellRight"));
            row.Children.Add(BuildCell(item.GuaranteeNo, 8, "TableCellRight"));

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

        private static UIElement BuildBankCell(RequestListDisplayItem item, int column)
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
                Height = 16,
                RenderTransformOrigin = new Point(0.5, 0.5)
            });
            bankCell.Children.Add(new Border { Width = 8 });
            bankCell.Children.Add(new TextBlock
            {
                Text = item.Bank,
                Style = WorkspaceSurfaceChrome.Style("TableCellText"),
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(bankCell, column);
            return bankCell;
        }

        private static Button CreateRowButton(string text, string iconKey, RequestListDisplayItem item, RoutedEventHandler handler, bool isEnabled, string? toolTip = null)
        {
            var button = new Button
            {
                Content = BuildRowButtonContent(text, iconKey),
                Tag = item,
                Style = WorkspaceSurfaceChrome.Style("RowButton"),
                IsEnabled = isEnabled,
                ToolTip = toolTip
            };
            ToolTipService.SetShowOnDisabled(button, true);
            UiInstrumentation.Identify(
                button,
                UiInstrumentation.SanitizeAutomationKey($"Requests.RowAction.{text}", item.GuaranteeNo),
                $"{text} | {item.GuaranteeNo}");
            button.Click += handler;
            return button;
        }

        private Button CreateMoreButton(RequestListDisplayItem item)
        {
            var button = new Button
            {
                Content = BuildRowButtonContent(string.Empty, "Icon.ChevronDown"),
                Tag = item,
                Style = WorkspaceSurfaceChrome.Style("RowButton"),
                MinWidth = 26,
                ToolTip = "إجراءات إضافية"
            };
            UiInstrumentation.Identify(
                button,
                UiInstrumentation.SanitizeAutomationKey("Requests.RowAction.More", item.GuaranteeNo),
                $"إجراءات إضافية | {item.GuaranteeNo}");

            ContextMenu menu = BuildRowContextMenu(item);
            button.ContextMenu = menu;
            button.Click += (_, e) =>
            {
                SelectRowFromSender(button);
                menu.PlacementTarget = button;
                menu.IsOpen = true;
                e.Handled = true;
            };
            return button;
        }

        private ContextMenu BuildRowContextMenu(RequestListDisplayItem item)
        {
            var menu = new ContextMenu
            {
                MinWidth = 172,
                FlowDirection = FlowDirection.RightToLeft,
                Background = Brushes.White,
                BorderBrush = WorkspaceSurfaceChrome.BrushFrom("#D8E1EE"),
                BorderThickness = new Thickness(1)
            };

            menu.Resources.Add(typeof(MenuItem), new Style(typeof(MenuItem))
            {
                Setters =
                {
                    new Setter(MenuItem.FontSizeProperty, 11d),
                    new Setter(MenuItem.FontWeightProperty, FontWeights.SemiBold),
                    new Setter(MenuItem.ForegroundProperty, WorkspaceSurfaceChrome.BrushFrom("#1F2937")),
                    new Setter(MenuItem.PaddingProperty, new Thickness(10, 5, 10, 5))
                }
            });

            menu.Items.Add(BuildMenuItem("سجل الضمان", "يفتح سجل الإصدارات والطلبات للضمان المرتبط بهذا الطلب.", (_, _) => _coordinator.OpenHistory(item)));
            menu.Items.Add(BuildMenuItem("فتح الضمان الحالي", "يفتح ملف الضمان الحالي المرتبط بهذا الطلب مع التركيز على قسم الطلبات.", (_, _) => _coordinator.OpenCurrentGuarantee(item)));
            menu.Items.Add(BuildMenuItem(
                item.QueueActionLabel,
                item.QueueActionHint,
                (_, _) =>
                {
                    SelectItem(item);
                    UseResponseAction();
                },
                item.CanRunQueueAction));
            menu.Items.Add(BuildMenuItem(
                "خطاب الطلب",
                item.CanOpenLetter ? "يفتح خطاب الطلب الخارجي لهذا السجل." : "لا يوجد خطاب طلب محفوظ لهذا السجل.",
                (_, _) =>
                {
                    SelectItem(item);
                    OpenLetter();
                },
                item.CanOpenLetter));
            menu.Items.Add(new Separator());
            menu.Items.Add(BuildMenuItem("تصدير المعلقات من نفس النوع", "يصدر كل الطلبات المعلقة من نفس نوع الطلب المحدد حاليًا.", (_, _) => _coordinator.ExportPendingSameType(item, _allRequests)));
            menu.Items.Add(BuildMenuItem("تصدير التمديدات المعلقة", "يصدر جميع طلبات التمديد المعلقة الحالية.", (_, _) => _coordinator.ExportPendingExtensions(_allRequests)));
            menu.Items.Add(BuildMenuItem("تصدير التخفيضات المعلقة", "يصدر جميع طلبات التخفيض المعلقة الحالية.", (_, _) => _coordinator.ExportPendingReductions(_allRequests)));
            menu.Items.Add(BuildMenuItem("تصدير الإفراجات المعلقة", "يصدر جميع طلبات الإفراج المعلقة الحالية.", (_, _) => _coordinator.ExportPendingReleases(_allRequests)));
            menu.Items.Add(BuildMenuItem("تصدير طلبات التسييل المعلقة", "يصدر جميع طلبات التسييل المعلقة الحالية.", (_, _) => _coordinator.ExportPendingLiquidations(_allRequests)));
            menu.Items.Add(BuildMenuItem("تصدير طلبات التحقق المعلقة", "يصدر جميع طلبات التحقق المعلقة الحالية.", (_, _) => _coordinator.ExportPendingVerifications(_allRequests)));
            menu.Items.Add(BuildMenuItem("تصدير طلبات الاستبدال المعلقة", "يصدر جميع طلبات الاستبدال المعلقة الحالية.", (_, _) => _coordinator.ExportPendingReplacements(_allRequests)));
            menu.Items.Add(new Separator());
            menu.Items.Add(BuildMenuItem("نسخ رقم الضمان", "ينسخ رقم الضمان لهذا الطلب إلى الحافظة.", (_, _) => _coordinator.CopyGuaranteeNo(item)));
            menu.Items.Add(BuildMenuItem("نسخ اسم المورد", "ينسخ اسم المورد لهذا الطلب إلى الحافظة.", (_, _) => _coordinator.CopySupplier(item)));
            menu.Items.Add(BuildMenuItem("نسخ المرجع", "ينسخ المرجع المرتبط بالضمان الحالي لهذا الطلب.", (_, _) => _coordinator.CopyReference(item)));

            return menu;
        }

        private Button CreateToolbarMenuButton(string text, string automationId)
        {
            var button = WorkspaceSurfaceChrome.ToolbarButton(text, automationId: automationId);
            button.Padding = new Thickness(12, 0, 12, 0);
            button.Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new TextBlock
                    {
                        Text = text,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "⌄",
                        FontSize = 12,
                        Margin = new Thickness(6, -2, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            };
            button.Click += (_, e) =>
            {
                if (button.ContextMenu == null)
                {
                    return;
                }

                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
                e.Handled = true;
            };
            return button;
        }

        private ContextMenu BuildOverflowMenu()
        {
            var menu = new ContextMenu
            {
                MinWidth = 220,
                FlowDirection = FlowDirection.RightToLeft,
                Background = Brushes.White,
                BorderBrush = WorkspaceSurfaceChrome.BrushFrom("#D8E1EE"),
                BorderThickness = new Thickness(1)
            };

            menu.Resources.Add(typeof(MenuItem), new Style(typeof(MenuItem))
            {
                Setters =
                {
                    new Setter(MenuItem.FontSizeProperty, 11d),
                    new Setter(MenuItem.FontWeightProperty, FontWeights.SemiBold),
                    new Setter(MenuItem.ForegroundProperty, WorkspaceSurfaceChrome.BrushFrom("#1F2937")),
                    new Setter(MenuItem.PaddingProperty, new Thickness(10, 5, 10, 5))
                }
            });

            menu.Items.Add(BuildSubmenu("إنشاء طلب", "مسارات إنشاء الطلبات من الضمانات المؤهلة.",
                BuildMenuItem("طلب تمديد", "يفتح قائمة الضمانات المؤهلة لإنشاء طلب تمديد جديد.", (_, _) => _coordinator.CreateExtensionFromEligible(ReloadAndFocusNewRequest)),
                BuildMenuItem("طلب تخفيض", "يفتح قائمة الضمانات المؤهلة لإنشاء طلب تخفيض جديد.", (_, _) => _coordinator.CreateReductionFromEligible(ReloadAndFocusNewRequest)),
                BuildMenuItem("طلب إفراج", "يفتح قائمة الضمانات المؤهلة لإنشاء طلب إفراج جديد.", (_, _) => _coordinator.CreateReleaseFromEligible(ReloadAndFocusNewRequest)),
                BuildMenuItem("طلب تسييل", "يفتح قائمة الضمانات المؤهلة لإنشاء طلب تسييل جديد.", (_, _) => _coordinator.CreateLiquidationFromEligible(ReloadAndFocusNewRequest)),
                BuildMenuItem("طلب تحقق", "يفتح قائمة الضمانات المؤهلة لإنشاء طلب تحقق جديد.", (_, _) => _coordinator.CreateVerificationFromEligible(ReloadAndFocusNewRequest)),
                BuildMenuItem("طلب استبدال", "يفتح قائمة الضمانات المؤهلة لإنشاء طلب استبدال جديد.", (_, _) => _coordinator.CreateReplacementFromEligible(ReloadAndFocusNewRequest)),
                BuildMenuItem("طلب نقض", "يفتح قائمة الضمانات المؤهلة لإنشاء طلب نقض جديد.", (_, _) => _coordinator.CreateAnnulmentFromEligible(ReloadAndFocusNewRequest))));
            menu.Items.Add(BuildSubmenu("تصدير الطلبات", "أدوات تصدير المعلقات حسب النوع.",
                BuildMenuItem("تصدير التمديدات المعلقة", "يصدر جميع طلبات التمديد المعلقة الحالية.", (_, _) => _coordinator.ExportPendingExtensions(_allRequests)),
                BuildMenuItem("تصدير التخفيضات المعلقة", "يصدر جميع طلبات التخفيض المعلقة الحالية.", (_, _) => _coordinator.ExportPendingReductions(_allRequests)),
                BuildMenuItem("تصدير الإفراجات المعلقة", "يصدر جميع طلبات الإفراج المعلقة الحالية.", (_, _) => _coordinator.ExportPendingReleases(_allRequests)),
                BuildMenuItem("تصدير طلبات التسييل المعلقة", "يصدر جميع طلبات التسييل المعلقة الحالية.", (_, _) => _coordinator.ExportPendingLiquidations(_allRequests)),
                BuildMenuItem("تصدير طلبات التحقق المعلقة", "يصدر جميع طلبات التحقق المعلقة الحالية.", (_, _) => _coordinator.ExportPendingVerifications(_allRequests)),
                BuildMenuItem("تصدير طلبات الاستبدال المعلقة", "يصدر جميع طلبات الاستبدال المعلقة الحالية.", (_, _) => _coordinator.ExportPendingReplacements(_allRequests))));
            return menu;
        }

        private static MenuItem BuildMenuItem(string header, string tooltip, RoutedEventHandler handler, bool isEnabled = true)
        {
            var item = new MenuItem
            {
                Header = header,
                ToolTip = tooltip,
                IsEnabled = isEnabled
            };
            ToolTipService.SetShowOnDisabled(item, true);
            item.Click += handler;
            return item;
        }

        private static MenuItem BuildSubmenu(string header, string tooltip, params MenuItem[] items)
        {
            var parent = new MenuItem
            {
                Header = header,
                ToolTip = tooltip
            };

            foreach (MenuItem item in items)
            {
                parent.Items.Add(item);
            }

            return parent;
        }

        private static UIElement BuildRowButtonContent(string text, string iconKey)
        {
            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            if (Application.Current.TryFindResource(iconKey) is Geometry geometry)
            {
                var icon = new Viewbox
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
                };
                stack.Children.Add(icon);
            }

            stack.Children.Add(new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center
            });
            return stack;
        }

        private void SelectRowFromButton_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
        }

        private void OpenLetterFromRow_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
            OpenLetter();
        }

        private void OpenResponseFromRow_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
            UseResponseAction();
        }

        private void SelectRowFromSender(object sender)
        {
            if (sender is not FrameworkElement element || element.Tag is not RequestListDisplayItem item)
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

        private void SelectItem(RequestListDisplayItem item)
        {
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

        private void ApplyMetrics(RequestsWorkspaceMetrics metrics)
        {
            _totalValue.Text = metrics.Total;
            _pendingValue.Text = metrics.Pending;
            _missingResponseValue.Text = metrics.MissingResponse;
            _closedValue.Text = metrics.Closed;
        }

        private void UpdateDetails()
        {
            ApplyDetailState(_dataService.BuildDetailState(SelectedItem));
        }

        private void ApplyDetailState(RequestsWorkspaceDetailState state)
        {
            _detailTitle.Text = state.Title;
            _detailSubtitle.Text = state.Subtitle;
            _detailBankText.Text = state.BankText;
            _detailBankLogo.Source = state.BankLogo;
            _detailStatusBadge.Text = state.BadgeText;
            _detailStatusBadge.Foreground = state.BadgeForeground;
            _detailStatusBadgeBorder.Background = state.BadgeBackground;
            _detailStatusBadgeBorder.BorderBrush = state.BadgeBorder;
            _detailReference.Text = state.Reference;
            _detailStatus.Text = state.Status;
            _detailStatus.Foreground = WorkspaceSurfaceChrome.BrushFrom("#111827");
            _detailCurrent.Text = state.Current;
            _detailRequested.Text = state.Requested;
            _detailDates.Text = state.Dates;
            _detailNotes.Text = state.Notes;
            _detailResponse.Text = state.Response;
            _openGuaranteeButton.IsEnabled = state.CanOpenGuarantee;
            _letterButton.IsEnabled = state.CanOpenLetter;
            _primaryActionButton.IsEnabled = state.CanRunPrimaryAction;
            _primaryActionButton.Content = state.PrimaryActionLabel;
            _primaryActionButton.ToolTip = state.PrimaryActionHint;
        }

        private void RegisterSelectedResponse()
        {
            _coordinator.RegisterSelectedResponse(SelectedItem, ReloadRequests);
        }

        private void ReloadAndFocusNewRequest(int? requestId)
        {
            _statusFilter.SelectedIndex = 0;
            ReloadRequests(requestId);
        }

        private void OpenLetter()
        {
            _coordinator.OpenLetter(SelectedRequest);
        }

        private void OpenCurrentGuarantee()
        {
            _coordinator.OpenCurrentGuarantee(SelectedItem);
        }

        private void UseResponseAction()
        {
            _coordinator.HandleResponseAction(SelectedItem, ReloadRequests);
        }

        private static Grid BuildInfoLine(string label, TextBlock value)
        {
            return WorkspaceSurfaceChrome.InfoLine(label, value);
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
                Child = new Path
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

        private sealed record RequestStatusFilterOption(string Label, RequestStatus? Status)
        {
            public override string ToString() => Label;
        }
    }

    public sealed class RequestsWorkspaceDialog : Window
    {
        private RequestsWorkspaceDialog(
            Func<IReadOnlyList<WorkflowRequestListItem>> loadRequests,
            IDatabaseService database,
            IWorkflowService workflow,
            IExcelService excel,
            Action<int>? onChanged)
        {
            Title = "الطلبات";
            Width = 980;
            Height = 640;
            MinWidth = 860;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = WorkspaceSurfaceChrome.BrushFrom("#F7F9FC");
            Content = new RequestsWorkspaceSurface(loadRequests, database, workflow, excel, onChanged, Close);
        }

        public static void ShowFor(
            Func<IReadOnlyList<WorkflowRequestListItem>> loadRequests,
            IWorkflowService workflow,
            IDatabaseService database,
            IExcelService excel,
            Action<int>? onChanged = null)
        {
            App.CurrentApp.GetRequiredService<SecondaryWindowManager>().ShowDialog(
                "requests-workspace-dialog",
                () => new RequestsWorkspaceDialog(loadRequests, database, workflow, excel, onChanged),
                "الطلبات",
                "نافذة الطلبات مفتوحة بالفعل.");
        }
    }
}
