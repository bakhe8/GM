using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
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
        private readonly Action<int, GuaranteeFileFocusArea, int?> _openGuaranteeContext;
        private readonly Action? _selectionChanged;
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
        private readonly TextBlock _letterAttachmentTitle = BuildDetailValue(11, FontWeights.SemiBold);
        private readonly TextBlock _letterAttachmentMeta = BuildMutedText(10, FontWeights.Normal);
        private readonly TextBlock _responseAttachmentTitle = BuildDetailValue(11, FontWeights.SemiBold);
        private readonly TextBlock _responseAttachmentMeta = BuildMutedText(10, FontWeights.Normal);
        private readonly TextBlock _responseAttachHint = BuildMutedText(10.2, FontWeights.Normal);
        private readonly Button _openGuaranteeButton = new();
        private readonly Button _letterButton = new();
        private readonly Button _responseDocumentButton = new();
        private readonly Button _primaryActionButton = new();
        private readonly ReferenceTablePagerController _pager;
        private IReadOnlyList<WorkflowRequestListItem> _allRequests = Array.Empty<WorkflowRequestListItem>();

        public RequestsWorkspaceSurface(
            Func<IReadOnlyList<WorkflowRequestListItem>> loadRequests,
            IDatabaseService database,
            IWorkflowService workflow,
            Action<int>? onChanged,
            Action<int, GuaranteeFileFocusArea, int?> openGuaranteeContext,
            Action? selectionChanged = null,
            string? initialSearchText = null,
            int? initialRequestId = null)
        {
            _loadRequests = loadRequests;
            _openGuaranteeContext = openGuaranteeContext;
            _selectionChanged = selectionChanged;
            _dataService = new RequestsWorkspaceDataService();
            _coordinator = new RequestsWorkspaceCoordinator(database, workflow, App.CurrentApp.GetRequiredService<IShellStatusService>(), onChanged);
            _pager = new ReferenceTablePagerController("Requests", "طلب", 10, () => ApplyFilters());
            UiInstrumentation.Identify(this, "Requests.Workspace", "مركز الطلبات");
            UiInstrumentation.Identify(_searchInput, "Requests.SearchBox", "بحث الطلبات");
            UiInstrumentation.Identify(_statusFilter, "Requests.Filter.Status", "حالة الطلبات");
            UiInstrumentation.Identify(_list, "Requests.Table.List", "قائمة الطلبات");

            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = WorkspaceSurfaceChrome.BrushResource("Brush.Canvas");
            RenderOptions.SetBitmapScalingMode(_detailBankLogo, BitmapScalingMode.HighQuality);

            ConfigureButtons();
            Content = BuildLayout();
            ApplyInitialSearch(initialSearchText);
            ReloadRequests(initialRequestId);
        }

        public RequestListDisplayItem? SelectedDiagnosticsItem => SelectedItem;

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
                null);
        }

        private void ConfigureButtons()
        {
            _openGuaranteeButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            _openGuaranteeButton.Content = "طلبات الضمان";
            _openGuaranteeButton.FontSize = 9.5;
            _openGuaranteeButton.Click += (_, _) => ShowSelectedGuaranteeRequests();
            UiInstrumentation.Identify(_openGuaranteeButton, "Requests.Detail.OpenGuaranteeButton", "طلبات الضمان");

            _primaryActionButton.Style = WorkspaceSurfaceChrome.Style("PrimaryButton");
            _primaryActionButton.Content = "إرفاق رد البنك";
            _primaryActionButton.FontSize = 9.5;
            _primaryActionButton.Click += (_, _) => UseResponseAction();
            UiInstrumentation.Identify(_primaryActionButton, "Requests.Detail.AttachResponseButton", "إرفاق رد البنك");

            _letterButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            _letterButton.Content = "فتح الخطاب";
            _letterButton.FontSize = 9.5;
            _letterButton.Click += (_, _) => OpenLetter();
            UiInstrumentation.Identify(_letterButton, "Requests.Detail.OpenLetterButton", "فتح الخطاب");

            _responseDocumentButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            _responseDocumentButton.Content = "فتح رد البنك";
            _responseDocumentButton.FontSize = 9.5;
            _responseDocumentButton.Click += (_, _) => OpenResponse();
            UiInstrumentation.Identify(_responseDocumentButton, "Requests.Detail.OpenResponseButton", "فتح رد البنك");
        }

        private UIElement BuildToolbarBlock()
        {
            return BuildToolbar();
        }

        private Grid BuildToolbar()
        {
            var toolbar = new Grid { FlowDirection = FlowDirection.LeftToRight };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var createButton = CreateToolbarMenuButton("إنشاء الطلبات", "Requests.Toolbar.CreateRequests", primary: true);
            createButton.ContextMenu = BuildCreateRequestsMenu();
            createButton.ToolTip = "يفتح مسارات إنشاء الطلبات من الضمانات المؤهلة.";
            Grid.SetColumn(createButton, 0);
            toolbar.Children.Add(createButton);

            _statusFilter.Style = WorkspaceSurfaceChrome.Style("FilterComboBox");
            _statusFilter.Items.Add(new RequestStatusFilterOption("كل الحالات", null));
            _statusFilter.Items.Add(new RequestStatusFilterOption("قيد الانتظار", RequestStatus.Pending));
            _statusFilter.Items.Add(new RequestStatusFilterOption("منفذ", RequestStatus.Executed));
            _statusFilter.Items.Add(new RequestStatusFilterOption("مرفوض", RequestStatus.Rejected));
            _statusFilter.Items.Add(new RequestStatusFilterOption("ملغى", RequestStatus.Cancelled));
            _statusFilter.Items.Add(new RequestStatusFilterOption("مسقط آليًا", RequestStatus.Superseded));
            _statusFilter.DisplayMemberPath = nameof(RequestStatusFilterOption.Label);
            _statusFilter.SelectedIndex = 0;
            _statusFilter.SelectionChanged += (_, _) =>
            {
                _pager.ResetToFirstPage();
                ApplyFilters();
            };
            Grid.SetColumn(_statusFilter, 2);
            toolbar.Children.Add(_statusFilter);

            _searchInput.TextChanged += (_, _) =>
            {
                _pager.ResetToFirstPage();
                ApplyFilters();
            };
            var searchBox = WorkspaceSurfaceChrome.ToolbarSearchBox(_searchInput, "ابحث برقم الضمان، البنك، المورد، أو نوع الطلب...");
            Grid.SetColumn(searchBox, 4);
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
            metrics.Children.Add(BuildMetricCard("بانتظار التنفيذ", _pendingValue, "#E09408"));
            metrics.Children.Add(BuildMetricCard("تحتاج مستند رد", _missingResponseValue, "#3B82F6"));
            metrics.Children.Add(BuildMetricCard("مغلقة", _closedValue, "#16A34A"));
            WorkspaceSurfaceChrome.ApplyMetricCardSpacing(metrics);
            return metrics;
        }

        private Border BuildMetricCard(string label, TextBlock value, string accent)
        {
            var card = WorkspaceSurfaceChrome.Card(new Thickness(14, 10, 14, 10));
            card.Margin = new Thickness(0);

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
            _list.SelectionChanged += (_, _) =>
            {
                _selectionChanged?.Invoke();
            };
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
            AddHeader(inner, "عمر الطلب", 5, false);
            AddHeader(inner, "تاريخ الطلب", 6, false);
            AddHeader(inner, "البنك", 7, true);
            AddHeader(inner, "المورد", 8, true);
            AddHeader(inner, "الإصدار", 9, false);
            AddHeader(inner, "رقم الضمان", 10, true);
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
            _detailStatusBadgeBorder.Margin = new Thickness(0, 0, 0, 12);
            _detailStatusBadgeBorder.Child = _detailStatusBadge;

            return new StackPanel
            {
                Margin = new Thickness(16, 14, 16, 14),
                Children =
                {
                    BuildRequestTitleRow(),
                    BuildSupplierRow(),
                    BuildBankRow(),
                    new Border { Height = 1, Background = WorkspaceSurfaceChrome.BrushFrom("#EDF2F7"), Margin = new Thickness(0, 13, 0, 12) },
                    BuildRequestAttachmentsCard(),
                    BuildResponseAttachCard()
                }
            };
        }

        private UIElement BuildRequestTitleRow()
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
            _detailTitle.Margin = new Thickness(0);
            row.Children.Add(_detailTitle);
            row.Children.Add(new Border { Width = 3 });
            row.Children.Add(WorkspaceSurfaceChrome.DetailHeaderCopyButton(
                "نسخ رقم الضمان",
                "Requests.Detail.Header.CopyGuaranteeNo",
                (_, _) => _coordinator.CopyGuaranteeNo(SelectedItem)));
            Grid.SetColumn(row, 1);
            grid.Children.Add(row);
            return grid;
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
            row.Children.Add(WorkspaceSurfaceChrome.DetailHeaderCopyButton(
                "نسخ اسم المورد",
                "Requests.Detail.Header.CopySupplier",
                (_, _) => _coordinator.CopySupplier(SelectedItem)));
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
            _detailBankText.Margin = new Thickness(7, 0, 0, 0);
            row.Children.Add(_detailBankText);
            return row;
        }

        private Border BuildRequestAttachmentsCard()
        {
            var card = WorkspaceSurfaceChrome.Card(new Thickness(12));
            card.Margin = new Thickness(0, 0, 0, 10);

            var stack = new StackPanel();
            stack.Children.Add(BuildDetailCardHeader("الأدلة والمرفقات", "Icon.Paperclip", "مرفقات الطلب"));
            stack.Children.Add(new Border
            {
                Height = 1,
                Background = WorkspaceSurfaceChrome.BrushFrom("#EDF2F7"),
                Margin = new Thickness(0, 0, 0, 10)
            });
            stack.Children.Add(BuildAttachmentLine(
                "خطاب الطلب",
                _letterAttachmentTitle,
                _letterAttachmentMeta,
                _letterButton));
            stack.Children.Add(new Border
            {
                Height = 1,
                Background = WorkspaceSurfaceChrome.BrushFrom("#EDF2F7"),
                Margin = new Thickness(0, 9, 0, 9)
            });
            stack.Children.Add(BuildAttachmentLine(
                "رد البنك",
                _responseAttachmentTitle,
                _responseAttachmentMeta,
                _responseDocumentButton));

            card.Child = stack;
            return card;
        }

        private Border BuildResponseAttachCard()
        {
            var card = WorkspaceSurfaceChrome.Card(new Thickness(12));
            card.Margin = new Thickness(0, 0, 0, 0);

            _responseAttachHint.Margin = new Thickness(0, 7, 0, 10);
            _primaryActionButton.HorizontalAlignment = HorizontalAlignment.Stretch;

            var stack = new StackPanel();
            stack.Children.Add(BuildDetailCardHeader("إرفاق رد البنك", "Icon.Document", "من اللوحة"));
            stack.Children.Add(_responseAttachHint);
            stack.Children.Add(_primaryActionButton);

            card.Child = stack;
            return card;
        }

        private static UIElement BuildDetailCardHeader(string title, string iconKey, string summary)
        {
            var grid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 8),
                FlowDirection = FlowDirection.LeftToRight
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var pill = new Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(7, 2, 7, 2),
                Background = WorkspaceSurfaceChrome.BrushFrom("#F8FAFC"),
                BorderBrush = WorkspaceSurfaceChrome.BrushFrom("#E2E8F0"),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = summary,
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B")
                }
            };
            grid.Children.Add(pill);

            var titleRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleRow.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text"),
                VerticalAlignment = VerticalAlignment.Center
            });
            titleRow.Children.Add(new Border { Width = 6 });
            titleRow.Children.Add(CreateIcon(iconKey, "#111827", 14));
            Grid.SetColumn(titleRow, 1);
            grid.Children.Add(titleRow);

            return grid;
        }

        private static UIElement BuildAttachmentLine(string label, TextBlock title, TextBlock meta, Button actionButton)
        {
            var grid = new Grid { FlowDirection = FlowDirection.LeftToRight };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            actionButton.MinWidth = 86;
            actionButton.Height = 30;
            actionButton.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(actionButton);

            var textStack = new StackPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                VerticalAlignment = VerticalAlignment.Center
            };
            var labelRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft
            };
            labelRow.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#94A3C8")
            });
            labelRow.Children.Add(new Border { Width = 6 });
            labelRow.Children.Add(CreateIcon("Icon.Document", "#94A3B8", 12));
            textStack.Children.Add(labelRow);
            title.Margin = new Thickness(0, 3, 0, 0);
            meta.Margin = new Thickness(0, 2, 0, 0);
            textStack.Children.Add(title);
            textStack.Children.Add(meta);

            Grid.SetColumn(textStack, 2);
            grid.Children.Add(textStack);
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
                Text = "العمل الحالي",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text")
            });

            _openGuaranteeButton.Margin = new Thickness(0, 9, 0, 0);
            _openGuaranteeButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetRow(_openGuaranteeButton, 1);
            grid.Children.Add(_openGuaranteeButton);

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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.25, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.45, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.72, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
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

            if (requestIdToSelect.HasValue)
            {
                _pager.MoveToItemIndex(IndexOfRequest(filtered.Items, requestIdToSelect.Value));
            }

            IReadOnlyList<RequestListDisplayItem> pageItems = _pager.Page(filtered.Items);
            foreach (RequestListDisplayItem item in pageItems)
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

            _summary.Text = _pager.BuildSummary();
            UpdateDetails();
        }

        private static int IndexOfRequest(IReadOnlyList<RequestListDisplayItem> items, int requestId)
        {
            for (int index = 0; index < items.Count; index++)
            {
                if (items[index].Item.Request.Id == requestId)
                {
                    return index;
                }
            }

            return -1;
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
            actions.Children.Add(CreateRowButton("خطاب", "Icon.Document", item, OpenLetterFromRow_Click, item.CanOpenLetter, item.CanOpenLetter ? "فتح خطاب الطلب." : "لا يوجد خطاب محفوظ لهذا الطلب."));
            actions.Children.Add(CreateRowButton("الضمان", "Icon.View", item, OpenGuaranteeFromRow_Click, item.Item.RootGuaranteeId > 0, "فتح الضمان في واجهة الضمانات عند السجل الزمني."));
            Grid.SetColumn(actions, 0);
            row.Children.Add(actions);

            row.Children.Add(BuildCell(item.RequestStatus, 1, "TableCellCenter", item.RequestStatusBrush));
            row.Children.Add(BuildCell(item.RequestType, 2, "TableCellCenter"));
            row.Children.Add(BuildCell(item.RequestedValue, 3, "TableCellCenter"));
            row.Children.Add(BuildCell(item.CurrentValue, 4, "TableCellCenter"));
            row.Children.Add(BuildCell(item.RequestAge, 5, "TableCellCenter", item.RequestAgeBrush));
            row.Children.Add(BuildCell(item.RequestDate, 6, "TableCellCenter"));
            row.Children.Add(BuildBankCell(item, 7));
            row.Children.Add(BuildCell(item.Supplier, 8, "TableCellRight"));
            row.Children.Add(BuildCell(item.VersionLabel, 9, "TableCellCenter"));
            row.Children.Add(BuildCell(item.GuaranteeNo, 10, "TableCellRight"));

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
                UiInstrumentation.SanitizeAutomationKey($"Requests.RowAction.{text}.{item.Item.Request.Id}", item.GuaranteeNo),
                $"{text} | {item.GuaranteeNo}");
            button.Click += handler;
            return button;
        }

        private Button CreateToolbarMenuButton(string text, string automationId, bool primary = false)
        {
            var button = WorkspaceSurfaceChrome.ToolbarButton(text, primary, automationId);
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

        private ContextMenu BuildCreateRequestsMenu()
        {
            var menu = BuildToolbarContextMenu();
            menu.Items.Add(BuildMenuItem("طلب تمديد", "يفتح قائمة الضمانات المؤهلة لإنشاء طلب تمديد جديد.", (_, _) => _coordinator.CreateExtensionFromEligible(ReloadAndFocusNewRequest)));
            menu.Items.Add(BuildMenuItem("طلب تخفيض", "يفتح قائمة الضمانات المؤهلة لإنشاء طلب تخفيض جديد.", (_, _) => _coordinator.CreateReductionFromEligible(ReloadAndFocusNewRequest)));
            menu.Items.Add(BuildMenuItem("طلب إفراج", "يفتح قائمة الضمانات المؤهلة لإنشاء طلب إفراج جديد.", (_, _) => _coordinator.CreateReleaseFromEligible(ReloadAndFocusNewRequest)));
            menu.Items.Add(BuildMenuItem("طلب تسييل", "يفتح قائمة الضمانات المؤهلة لإنشاء طلب تسييل جديد.", (_, _) => _coordinator.CreateLiquidationFromEligible(ReloadAndFocusNewRequest)));
            menu.Items.Add(BuildMenuItem("طلب تحقق", "يفتح قائمة الضمانات المؤهلة لإنشاء طلب تحقق جديد.", (_, _) => _coordinator.CreateVerificationFromEligible(ReloadAndFocusNewRequest)));
            menu.Items.Add(BuildMenuItem("طلب استبدال", "يفتح قائمة الضمانات المؤهلة لإنشاء طلب استبدال جديد.", (_, _) => _coordinator.CreateReplacementFromEligible(ReloadAndFocusNewRequest)));
            return menu;
        }

        private static ContextMenu BuildToolbarContextMenu()
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

        private void OpenResponseFromRow_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
            UseResponseAction();
        }

        private void OpenLetterFromRow_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
            OpenLetter();
        }

        private void OpenGuaranteeFromRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.Tag is not RequestListDisplayItem item)
            {
                return;
            }

            SelectRowFromSender(sender);
            _openGuaranteeContext(item.Item.RootGuaranteeId, GuaranteeFileFocusArea.Series, null);
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
            _letterAttachmentTitle.Text = state.LetterAttachmentTitle;
            _letterAttachmentMeta.Text = state.LetterAttachmentMeta;
            _responseAttachmentTitle.Text = state.ResponseAttachmentTitle;
            _responseAttachmentMeta.Text = state.ResponseAttachmentMeta;
            _responseAttachHint.Text = state.ResponseAttachHint;
            _openGuaranteeButton.IsEnabled = state.CanOpenGuarantee;
            _openGuaranteeButton.ToolTip = state.CanOpenGuarantee
                ? "يعرض الطلبات المرتبطة بهذا الضمان فقط داخل مركز الطلبات."
                : "اختر طلبًا أولًا لفلترة الطلبات حسب الضمان.";
            AutomationProperties.SetName(_openGuaranteeButton, "طلبات الضمان");
            AutomationProperties.SetHelpText(_openGuaranteeButton, "يعرض الطلبات المرتبطة بهذا الضمان فقط داخل مركز الطلبات.");
            _letterButton.IsEnabled = state.CanOpenLetter;
            _letterButton.ToolTip = state.LetterAttachmentMeta;
            _responseDocumentButton.IsEnabled = state.CanOpenResponse;
            _responseDocumentButton.ToolTip = state.ResponseAttachmentMeta;
            _primaryActionButton.IsEnabled = state.CanUseResponseAttachAction;
            _primaryActionButton.Content = state.ResponseAttachActionLabel;
            _primaryActionButton.ToolTip = state.ResponseAttachActionHint;
            AutomationProperties.SetName(_primaryActionButton, state.ResponseAttachActionLabel);
            AutomationProperties.SetHelpText(_primaryActionButton, state.ResponseAttachActionHint);
            AutomationProperties.SetItemStatus(_primaryActionButton, state.ResponseAttachActionHint);

        }

        private void ReloadAndFocusNewRequest(int? requestId)
        {
            _statusFilter.SelectedIndex = 0;
            ReloadRequests(requestId);
        }

        private void OpenLetter()
        {
            _coordinator.OpenLetter(SelectedItem);
        }

        private void OpenResponse()
        {
            _coordinator.OpenResponse(SelectedItem);
        }

        private void ShowSelectedGuaranteeRequests()
        {
            ShowGuaranteeRequestsFor(SelectedItem);
        }

        private void ShowGuaranteeRequestsFor(RequestListDisplayItem? item)
        {
            if (item == null)
            {
                return;
            }

            _statusFilter.SelectedIndex = 0;
            _searchInput.Text = item.GuaranteeNo;
            ApplyFilters(item.Item.Request.Id);
        }

        private void UseResponseAction()
        {
            _coordinator.HandleResponseAction(SelectedItem, ReloadRequests);
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

}
