using System;
using System.Collections.Generic;
using System.Windows.Automation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GuaranteeManager.Models;

namespace GuaranteeManager
{
    public sealed class NotificationsWorkspaceSurface : UserControl
    {
        private readonly NotificationsWorkspaceDataService _dataService;
        private readonly NotificationsWorkspaceCoordinator _coordinator;
        private readonly List<NotificationWorkspaceItem> _allItems;
        private readonly ListBox _list = new();
        private readonly TextBox _searchInput = new();
        private readonly ComboBox _levelFilter = new();
        private readonly TextBlock _summary = BuildMutedText(12, FontWeights.SemiBold);
        private readonly TextBlock _expiringValue = BuildMetricValue();
        private readonly TextBlock _expiredValue = BuildMetricValue();
        private readonly TextBlock _amountValue = BuildMetricValue();
        private readonly TextBlock _dateValue = BuildMetricValue();
        private readonly TextBlock _detailTitle = BuildDetailValue(16, FontWeights.Bold);
        private readonly TextBlock _detailSubtitle = BuildMutedText(11, FontWeights.SemiBold);
        private readonly TextBlock _detailStatusBadge = BuildBadgeText();
        private readonly Border _detailStatusBadgeBorder = new();
        private readonly Image _detailBankLogo = new() { Width = 17, Height = 17 };
        private readonly TextBlock _detailBankText = BuildMutedText(12, FontWeights.SemiBold);
        private readonly TextBlock _detailReference = BuildDetailValue(11.5, FontWeights.SemiBold);
        private readonly TextBlock _detailDuration = BuildDetailValue(12, FontWeights.Bold);
        private readonly TextBlock _detailExpiry = BuildDetailValue(12, FontWeights.SemiBold);
        private readonly TextBlock _detailAction = BuildDetailValue(12, FontWeights.SemiBold);
        private readonly TextBlock _detailAmountHeadline = BuildAmountHeadline();
        private readonly TextBlock _detailAmountCaption = BuildMutedText(11, FontWeights.Normal);
        private readonly TextBlock _detailNote = BuildMutedText(11, FontWeights.Normal);
        private readonly Button _openGuaranteeButton = new();
        private readonly Action? _closeRequested;
        private readonly Action<int, GuaranteeFileFocusArea, int?> _openGuaranteeContext;
        private readonly Action _showGuarantees;

        public NotificationsWorkspaceSurface(
            IReadOnlyList<Guarantee> expiring,
            IReadOnlyList<Guarantee> expired,
            Action<int, GuaranteeFileFocusArea, int?> openGuaranteeContext,
            Action showGuarantees,
            Action? closeRequested,
            string? initialSearchText = null)
        {
            _dataService = new NotificationsWorkspaceDataService();
            _coordinator = new NotificationsWorkspaceCoordinator();
            _allItems = _dataService.BuildItems(expiring, expired);
            _openGuaranteeContext = openGuaranteeContext;
            _showGuarantees = showGuarantees;
            _closeRequested = closeRequested;
            UiInstrumentation.Identify(this, "Notifications.Workspace", "التنبيهات");
            UiInstrumentation.Identify(_searchInput, "Notifications.SearchBox", "بحث التنبيهات");
            UiInstrumentation.Identify(_levelFilter, "Notifications.Filter.Level", "مستوى التنبيهات");
            UiInstrumentation.Identify(_list, "Notifications.Table.List", "قائمة التنبيهات");

            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = WorkspaceSurfaceChrome.BrushResource("Brush.Canvas");
            RenderOptions.SetBitmapScalingMode(_detailBankLogo, BitmapScalingMode.HighQuality);

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

        private Grid BuildToolbar()
        {
            var toolbar = new Grid { FlowDirection = FlowDirection.LeftToRight };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var resetButton = WorkspaceSurfaceChrome.ToolbarButton("إعادة ضبط", primary: true, automationId: "Notifications.Toolbar.Reset");
            resetButton.Click += (_, _) =>
            {
                _searchInput.Text = string.Empty;
                _levelFilter.SelectedIndex = 0;
                ApplyFilters();
            };
            Grid.SetColumn(resetButton, 0);
            toolbar.Children.Add(resetButton);

            _levelFilter.Style = WorkspaceSurfaceChrome.Style("FilterComboBox");
            _levelFilter.Items.Add("كل التنبيهات");
            _levelFilter.Items.Add("قريب الانتهاء");
            _levelFilter.Items.Add("منتهي");
            _levelFilter.SelectedIndex = 0;
            _levelFilter.SelectionChanged += (_, _) => ApplyFilters();
            Grid.SetColumn(_levelFilter, 2);
            toolbar.Children.Add(_levelFilter);

            _searchInput.TextChanged += (_, _) => ApplyFilters();
            var searchBox = WorkspaceSurfaceChrome.ToolbarSearchBox(_searchInput, "ابحث برقم الضمان أو البنك أو المستفيد...");
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
            metrics.Children.Add(BuildMetricCard("قريب الانتهاء", _expiringValue, "#E09408"));
            metrics.Children.Add(BuildMetricCard("منتهي", _expiredValue, "#EF4444"));
            metrics.Children.Add(BuildMetricCard("إجمالي القيمة", _amountValue, "#2563EB"));
            metrics.Children.Add(BuildMetricCard("أقرب تاريخ", _dateValue, "#0F172A"));
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
            AddHeader(inner, "المستوى", 1, false);
            AddHeader(inner, "الأيام", 2, false);
            AddHeader(inner, "تاريخ الانتهاء", 3, false);
            AddHeader(inner, "القيمة", 4, false);
            AddHeader(inner, "البنك", 5, true);
            AddHeader(inner, "المستفيد", 6, true);
            AddHeader(inner, "رقم الضمان", 7, true);
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
                    BuildNotificationTitleRow(),
                    _detailStatusBadgeBorder,
                    BuildBeneficiaryRow(),
                    BuildBankRow(),
                    _detailAmountHeadline,
                    _detailAmountCaption,
                    new Border { Height = 1, Background = WorkspaceSurfaceChrome.BrushFrom("#EDF2F7"), Margin = new Thickness(0, 13, 0, 12) },
                    BuildInfoLine("المرجع", _detailReference),
                    BuildInfoLine("المدة", _detailDuration),
                    BuildInfoLine("تاريخ الانتهاء", _detailExpiry),
                    BuildInfoLine("الإجراء المقترح", _detailAction),
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
                Text = "تفاصيل التنبيه",
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

        private UIElement BuildNotificationTitleRow()
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

        private UIElement BuildBeneficiaryRow()
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
            _openGuaranteeButton.Content = "فتح الضمان";
            _openGuaranteeButton.Style = WorkspaceSurfaceChrome.Style("PrimaryButton");
            _openGuaranteeButton.FontSize = 9.5;
            _openGuaranteeButton.Click += (_, _) =>
            {
                _coordinator.OpenGuaranteeContext(SelectedItem, _openGuaranteeContext, _showGuarantees);
            };
            UiInstrumentation.Identify(_openGuaranteeButton, "Notifications.Detail.OpenGuaranteeButton", "فتح الضمان");

            var copyGuaranteeButton = new Button
            {
                Content = "نسخ الرقم",
                Style = WorkspaceSurfaceChrome.Style("BaseButton"),
                FontSize = 9.5
            };
            copyGuaranteeButton.Click += (_, _) =>
            {
                _coordinator.CopyGuarantee(SelectedItem);
            };

            var copyBankButton = new Button
            {
                Content = "نسخ البنك",
                Style = WorkspaceSurfaceChrome.Style("BaseButton"),
                FontSize = 9.5
            };
            copyBankButton.Click += (_, _) =>
            {
                _coordinator.CopyBank(SelectedItem);
            };

            var copyAmountButton = new Button
            {
                Content = "نسخ القيمة",
                Style = WorkspaceSurfaceChrome.Style("PrimaryButton"),
                FontSize = 9.5
            };
            copyAmountButton.Click += (_, _) =>
            {
                _coordinator.CopyAmount(SelectedItem);
            };

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

            var actions = new WrapPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Thickness(0, 9, 0, 0),
                ItemWidth = 112,
                ItemHeight = 34
            };
            actions.Children.Add(_openGuaranteeButton);
            actions.Children.Add(copyGuaranteeButton);
            actions.Children.Add(copyBankButton);
            actions.Children.Add(copyAmountButton);
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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.75, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.8, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
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

        private NotificationWorkspaceItem? SelectedItem
            => (_list.SelectedItem as FrameworkElement)?.Tag as NotificationWorkspaceItem;

        private void ApplyFilters()
        {
            _list.Items.Clear();
            string selectedLevel = _levelFilter.SelectedItem as string ?? "كل التنبيهات";
            NotificationsWorkspaceFilterResult filtered = _dataService.BuildFilteredItems(
                _allItems,
                _searchInput.Text,
                selectedLevel);

            foreach (NotificationWorkspaceItem item in filtered.Items)
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

        private FrameworkElement BuildRow(NotificationWorkspaceItem item)
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
            actions.Children.Add(CreateRowButton("نسخ", "Icon.Document", item, CopyGuarantee_Click));
            actions.Children.Add(CreateRowButton("عرض", "Icon.View", item, SelectRow_Click));
            Grid.SetColumn(actions, 0);
            row.Children.Add(actions);

            row.Children.Add(BuildCell(item.Level, 1, "TableCellCenter", item.LevelBrush));
            row.Children.Add(BuildCell(item.DaysLabel, 2, "TableCellCenter", item.LevelBrush));
            row.Children.Add(BuildCell(item.ExpiryDate, 3, "TableCellCenter"));
            row.Children.Add(BuildAmountCell(item.AmountDisplay, 4));
            row.Children.Add(BuildBankCell(item, 5));
            row.Children.Add(BuildCell(item.Beneficiary, 6, "TableCellRight"));
            row.Children.Add(BuildCell(item.GuaranteeNo, 7, "TableCellRight"));
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

        private static UIElement BuildBankCell(NotificationWorkspaceItem item, int column)
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
                Style = WorkspaceSurfaceChrome.Style("TableCellText"),
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(bankCell, column);
            return bankCell;
        }

        private static TextBlock BuildAmountCell(string text, int column)
        {
            var cell = new TextBlock
            {
                Text = text,
                Style = WorkspaceSurfaceChrome.Style("TableCellCenter"),
                FlowDirection = FlowDirection.LeftToRight,
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(cell, column);
            return cell;
        }

        private static Button CreateRowButton(string text, string iconKey, NotificationWorkspaceItem item, RoutedEventHandler handler)
        {
            var button = new Button
            {
                Content = BuildRowButtonContent(text, iconKey),
                Tag = item,
                Style = WorkspaceSurfaceChrome.Style("RowButton")
            };
            UiInstrumentation.Identify(
                button,
                UiInstrumentation.SanitizeAutomationKey($"Notifications.RowAction.{text}", item.GuaranteeNo),
                $"{text} | {item.GuaranteeNo}");
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

        private void CopyGuarantee_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
            _coordinator.CopyGuarantee(SelectedItem);
        }

        private void SelectRowFromSender(object sender)
        {
            if (sender is not FrameworkElement element || element.Tag is not NotificationWorkspaceItem item)
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
            NotificationWorkspaceItem? selectedItem = SelectedItem;
            ApplyDetailState(_dataService.BuildDetailState(selectedItem));
            bool hasSelection = selectedItem != null;
            _openGuaranteeButton.IsEnabled = hasSelection;

            if (!hasSelection)
            {
                AutomationProperties.SetName(_openGuaranteeButton, "فتح الضمان");
                AutomationProperties.SetHelpText(_openGuaranteeButton, "اختر تنبيهًا أولًا لفتح ملف الضمان المرتبط به.");
                AutomationProperties.SetItemStatus(_openGuaranteeButton, "---");
                return;
            }

            AutomationProperties.SetName(_openGuaranteeButton, $"فتح الضمان {selectedItem!.GuaranteeNo}");
            AutomationProperties.SetHelpText(_openGuaranteeButton, selectedItem.FollowUpAction);
            AutomationProperties.SetItemStatus(_openGuaranteeButton, selectedItem.GuaranteeNo);
        }

        private void ApplyMetrics(NotificationsWorkspaceMetrics metrics)
        {
            _expiringValue.Text = metrics.Expiring;
            _expiredValue.Text = metrics.Expired;
            _amountValue.Text = metrics.Amount;
            _dateValue.Text = metrics.ClosestDate;
        }

        private void ApplyDetailState(NotificationsWorkspaceDetailState state)
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
            _detailDuration.Text = state.Duration;
            _detailDuration.Foreground = state.DurationBrush;
            _detailExpiry.Text = state.Expiry;
            _detailAction.Text = state.Action;
            _detailAction.Foreground = WorkspaceSurfaceChrome.BrushFrom("#111827");
            _detailAmountHeadline.Text = state.AmountHeadline;
            _detailAmountHeadline.Foreground = state.AmountBrush;
            _detailAmountCaption.Text = state.AmountCaption;
            _detailNote.Text = state.Note;
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
                TextAlignment = TextAlignment.Right,
                FlowDirection = FlowDirection.LeftToRight
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

        private static TextBlock BuildAmountHeadline()
        {
            return new TextBlock
            {
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A"),
                Margin = new Thickness(0, 12, 0, 0),
                TextAlignment = TextAlignment.Right,
                FlowDirection = FlowDirection.LeftToRight
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

    }
}
