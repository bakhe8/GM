using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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
        private readonly ComboBox _categoryFilter = new();
        private readonly TextBlock _summary = BuildMutedText(12, FontWeights.SemiBold);
        private readonly TextBlock _databaseValue = BuildMetricValue();
        private readonly TextBlock _attachmentsValue = BuildMetricValue();
        private readonly TextBlock _lettersValue = BuildMetricValue();
        private readonly TextBlock _responsesValue = BuildMetricValue();
        private readonly TextBlock _detailTitle = BuildDetailValue(16, FontWeights.Bold);
        private readonly TextBlock _detailSubtitle = BuildMutedText(11, FontWeights.SemiBold);
        private readonly TextBlock _detailStatusBadge = BuildBadgeText();
        private readonly Border _detailStatusBadgeBorder = new();
        private readonly TextBlock _detailState = BuildDetailValue(12, FontWeights.Bold);
        private readonly TextBlock _detailAction = BuildDetailValue(12, FontWeights.SemiBold);
        private readonly TextBlock _detailPath = BuildPathText();
        private readonly TextBlock _detailOpenPath = BuildPathText();
        private readonly Action? _closeRequested;

        public SettingsWorkspaceSurface(Action? closeRequested, string? initialSearchText = null)
        {
            _dataService = new SettingsWorkspaceDataService();
            _coordinator = new SettingsWorkspaceCoordinator();
            _closeRequested = closeRequested;
            _allItems = _dataService.BuildItems();
            UiInstrumentation.Identify(this, "Settings.Workspace", "الإعدادات");
            UiInstrumentation.Identify(_searchInput, "Settings.SearchBox", "بحث الإعدادات");
            UiInstrumentation.Identify(_categoryFilter, "Settings.Filter.Category", "فئة الإعدادات");
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

            var refreshButton = WorkspaceSurfaceChrome.ToolbarButton("إعادة فحص", primary: true, automationId: "Settings.Toolbar.Refresh");
            refreshButton.Click += (_, _) => ApplyFilters();
            Grid.SetColumn(refreshButton, 0);
            toolbar.Children.Add(refreshButton);

            var backupMenuButton = CreateToolbarMenuButton("النسخ الاحتياطي", "Settings.Toolbar.BackupMenu");
            backupMenuButton.ContextMenu = BuildToolbarMenu(
                new MenuItemSpec("إنشاء نسخة احتياطية", (_, _) => _coordinator.CreateManualBackup()),
                new MenuItemSpec("استرجاع نسخة احتياطية", (_, _) => _coordinator.RestoreManualBackup(ApplyFilters)),
                new MenuItemSpec("إنشاء حزمة محمولة", (_, _) => _coordinator.CreatePortableBackup()),
                new MenuItemSpec("استرجاع حزمة محمولة", (_, _) => _coordinator.RestorePortableBackup(ApplyFilters)));
            Grid.SetColumn(backupMenuButton, 2);
            toolbar.Children.Add(backupMenuButton);

            var toolsMenuButton = CreateToolbarMenuButton("أدوات", "Settings.Toolbar.ToolsMenu");
            var toolItems = new List<MenuItemSpec>
            {
                new("نسخ ملخص المسارات", (_, _) => _coordinator.CopyOperationalPathsSummary())
            };
#if DEBUG
            toolItems.Add(new MenuItemSpec("توليد بيانات تجريبية", (_, _) => _coordinator.SeedDevelopmentData(ApplyFilters)));
#endif
            toolsMenuButton.ContextMenu = BuildToolbarMenu(toolItems.ToArray());
            Grid.SetColumn(toolsMenuButton, 4);
            toolbar.Children.Add(toolsMenuButton);

            _categoryFilter.Style = WorkspaceSurfaceChrome.Style("FilterComboBox");
            _categoryFilter.Items.Add("كل المسارات");
            _categoryFilter.Items.Add("بيانات");
            _categoryFilter.Items.Add("سير العمل");
            _categoryFilter.SelectedIndex = 0;
            _categoryFilter.SelectionChanged += (_, _) => ApplyFilters();
            Grid.SetColumn(_categoryFilter, 6);
            toolbar.Children.Add(_categoryFilter);

            _searchInput.TextChanged += (_, _) => ApplyFilters();
            var searchBox = WorkspaceSurfaceChrome.ToolbarSearchBox(_searchInput, "ابحث باسم العنصر أو المسار...");
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
            metrics.Children.Add(BuildMetricCard("قاعدة البيانات", _databaseValue, "#16A34A"));
            metrics.Children.Add(BuildMetricCard("المرفقات", _attachmentsValue, "#2563EB"));
            metrics.Children.Add(BuildMetricCard("الخطابات", _lettersValue, "#E09408"));
            metrics.Children.Add(BuildMetricCard("ردود البنوك", _responsesValue, "#0F172A"));
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
            AddHeader(inner, "الفئة", 2, false);
            AddHeader(inner, "المسار", 3, true);
            AddHeader(inner, "العنصر", 4, true);
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
                    BuildSettingsTitleRow(),
                    _detailSubtitle,
                    _detailStatusBadgeBorder,
                    new Border { Height = 1, Background = WorkspaceSurfaceChrome.BrushFrom("#EDF2F7"), Margin = new Thickness(0, 13, 0, 12) },
                    WorkspaceSurfaceChrome.InfoLine("الحالة التشغيلية", _detailState),
                    WorkspaceSurfaceChrome.InfoLine("الإجراء التالي", _detailAction),
                    BuildInfoBlock("المسار", _detailPath),
                    BuildInfoBlock("مسار الفتح", _detailOpenPath)
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
                Text = "تفاصيل المسار",
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

        private UIElement BuildSettingsTitleRow()
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft
            };
            row.Children.Add(CreateIcon("Icon.Settings", "#64748B", 14));
            _detailTitle.Margin = new Thickness(8, 0, 0, 0);
            row.Children.Add(_detailTitle);
            return row;
        }

        private Border BuildDetailActions()
        {
            var openButton = new Button
            {
                Content = "فتح المجلد",
                Style = WorkspaceSurfaceChrome.Style("BaseButton"),
                FontSize = 9.5
            };
            openButton.Click += (_, _) => _coordinator.OpenPath(SelectedItem);

            var copyPathButton = new Button
            {
                Content = "نسخ المسار",
                Style = WorkspaceSurfaceChrome.Style("PrimaryButton"),
                FontSize = 9.5
            };
            copyPathButton.Click += (_, _) => _coordinator.CopyPath(SelectedItem);

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
            actions.Children.Add(openButton);
            Grid.SetColumn(copyPathButton, 2);
            actions.Children.Add(copyPathButton);
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
            string category = _categoryFilter.SelectedItem as string ?? "كل المسارات";
            SettingsWorkspaceFilterResult filtered = _dataService.BuildFilteredItems(
                _allItems,
                _searchInput.Text,
                category);
            foreach (SettingPathItem item in filtered.Items)
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
            actions.Children.Add(CreateRowButton("نسخ", "Icon.Document", item, CopyPath_Click));
            actions.Children.Add(CreateRowButton("فتح", "Icon.View", item, OpenPath_Click));
            actions.Children.Add(CreateRowButton("عرض", "Icon.Settings", item, SelectRow_Click));
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

        private static Button CreateToolbarMenuButton(string text, string automationId)
        {
            var button = WorkspaceSurfaceChrome.ToolbarButton(text, automationId: automationId);
            UiInstrumentation.Identify(button, automationId, text);
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

        private static ContextMenu BuildToolbarMenu(params MenuItemSpec[] items)
        {
            var menu = new ContextMenu
            {
                MinWidth = 196,
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

            foreach (MenuItemSpec item in items)
            {
                var menuItem = new MenuItem
                {
                    Header = item.Header,
                    IsEnabled = item.IsEnabled
                };
                menuItem.Click += item.ClickHandler;
                menu.Items.Add(menuItem);
            }

            return menu;
        }

        private void SelectRow_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
        }

        private void OpenPath_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
            _coordinator.OpenPath(SelectedItem);
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
            _coordinator.CopyPath(SelectedItem);
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
            _databaseValue.Text = metrics.Database;
            _attachmentsValue.Text = metrics.Attachments;
            _lettersValue.Text = metrics.Letters;
            _responsesValue.Text = metrics.Responses;
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

        private readonly record struct MenuItemSpec(string Header, RoutedEventHandler ClickHandler, bool IsEnabled = true);
    }
}
