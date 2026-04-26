using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GuaranteeManager.Models;

namespace GuaranteeManager
{
    public sealed class BanksWorkspaceSurface : UserControl
    {
        private readonly BanksWorkspaceDataService _dataService;
        private readonly BanksWorkspaceCoordinator _coordinator;
        private readonly List<BankWorkspaceItem> _allBanks;
        private readonly ListBox _list = new();
        private readonly TextBox _searchInput = new();
        private readonly ComboBox _sortFilter = new();
        private readonly TextBlock _summary = BuildMutedText(12, FontWeights.SemiBold);
        private readonly TextBlock _bankCountValue = BuildMetricValue();
        private readonly TextBlock _guaranteeCountValue = BuildMetricValue();
        private readonly TextBlock _activeValue = BuildMetricValue();
        private readonly TextBlock _amountValue = BuildMetricValue();
        private readonly Image _detailLogo = new() { Width = 36, Height = 36 };
        private readonly TextBlock _detailTitle = BuildDetailValue(16, FontWeights.Bold);
        private readonly TextBlock _detailSubtitle = BuildMutedText(11, FontWeights.SemiBold);
        private readonly TextBlock _detailStatusBadge = BuildBadgeText();
        private readonly Border _detailStatusBadgeBorder = new();
        private readonly TextBlock _detailAmountHeadline = BuildAmountHeadline();
        private readonly TextBlock _detailAmountCaption = BuildMutedText(11, FontWeights.Normal);
        private readonly TextBlock _detailCount = BuildDetailValue(12, FontWeights.Bold);
        private readonly TextBlock _detailActive = BuildDetailValue(12, FontWeights.Bold);
        private readonly TextBlock _detailExpiring = BuildDetailValue(12, FontWeights.Bold);
        private readonly TextBlock _detailExpired = BuildDetailValue(12, FontWeights.Bold);
        private readonly TextBlock _detailShare = BuildDetailValue(12, FontWeights.SemiBold);
        private readonly Action? _closeRequested;

        public BanksWorkspaceSurface(IReadOnlyList<Guarantee> guarantees, Action? closeRequested, string? initialSearchText = null)
        {
            _dataService = new BanksWorkspaceDataService();
            _coordinator = new BanksWorkspaceCoordinator();
            _allBanks = _dataService.BuildItems(guarantees);
            _closeRequested = closeRequested;
            UiInstrumentation.Identify(this, "Banks.Workspace", "البنوك");
            UiInstrumentation.Identify(_searchInput, "Banks.SearchBox", "بحث البنوك");
            UiInstrumentation.Identify(_sortFilter, "Banks.Filter.Sort", "ترتيب البنوك");
            UiInstrumentation.Identify(_list, "Banks.Table.List", "قائمة البنوك");

            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = new FontFamily("Segoe UI, Tahoma");
            Background = WorkspaceSurfaceChrome.BrushResource("Brush.Canvas");
            RenderOptions.SetBitmapScalingMode(_detailLogo, BitmapScalingMode.HighQuality);

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
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var resetButton = WorkspaceSurfaceChrome.ToolbarButton("إعادة ضبط", primary: true, automationId: "Banks.Toolbar.Reset");
            resetButton.Click += (_, _) =>
            {
                _searchInput.Text = string.Empty;
                _sortFilter.SelectedIndex = 0;
                ApplyFilters();
            };
            Grid.SetColumn(resetButton, 0);
            toolbar.Children.Add(resetButton);

            _sortFilter.Style = WorkspaceSurfaceChrome.Style("FilterComboBox");
            _sortFilter.Items.Add("الأعلى قيمة");
            _sortFilter.Items.Add("الأكثر عدداً");
            _sortFilter.Items.Add("الأعلى نشاطاً");
            _sortFilter.SelectedIndex = 0;
            _sortFilter.SelectionChanged += (_, _) => ApplyFilters();
            Grid.SetColumn(_sortFilter, 2);
            toolbar.Children.Add(_sortFilter);

            _searchInput.TextChanged += (_, _) => ApplyFilters();
            var searchBox = WorkspaceSurfaceChrome.ToolbarSearchBox(_searchInput, "ابحث باسم البنك أو المستفيد الأعلى...");
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
            metrics.Children.Add(BuildMetricCard("عدد البنوك", _bankCountValue, "#2563EB"));
            metrics.Children.Add(BuildMetricCard("إجمالي الضمانات", _guaranteeCountValue, "#0F172A"));
            metrics.Children.Add(BuildMetricCard("الضمانات النشطة", _activeValue, "#16A34A"));
            metrics.Children.Add(BuildMetricCard("إجمالي القيمة", _amountValue, "#E09408"));
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
            AddHeader(inner, "عدد الضمانات", 1, false);
            AddHeader(inner, "نشط", 2, false);
            AddHeader(inner, "قريب الانتهاء", 3, false);
            AddHeader(inner, "منتهي", 4, false);
            AddHeader(inner, "إجمالي القيمة", 5, false);
            AddHeader(inner, "المستفيد الأعلى", 6, true);
            AddHeader(inner, "البنك", 7, true);
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
                    BuildLogoHeader(),
                    _detailStatusBadgeBorder,
                    _detailAmountHeadline,
                    _detailAmountCaption,
                    new Border { Height = 1, Background = WorkspaceSurfaceChrome.BrushFrom("#EDF2F7"), Margin = new Thickness(0, 13, 0, 12) },
                    WorkspaceSurfaceChrome.InfoLine("عدد الضمانات", _detailCount),
                    WorkspaceSurfaceChrome.InfoLine("نشطة", _detailActive),
                    WorkspaceSurfaceChrome.InfoLine("قريبة الانتهاء", _detailExpiring),
                    WorkspaceSurfaceChrome.InfoLine("منتهية", _detailExpired),
                    WorkspaceSurfaceChrome.InfoLine("حصة المحفظة", _detailShare)
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
                Text = "تفاصيل البنك",
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

        private UIElement BuildLogoHeader()
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.RightToLeft };
            row.Children.Add(_detailLogo);

            var textStack = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            textStack.Children.Add(_detailTitle);
            textStack.Children.Add(_detailSubtitle);
            row.Children.Add(textStack);
            return row;
        }

        private Border BuildDetailActions()
        {
            var copyNameButton = new Button
            {
                Content = "نسخ الاسم",
                Style = WorkspaceSurfaceChrome.Style("BaseButton"),
                FontSize = 9.5
            };
            copyNameButton.Click += (_, _) =>
            {
                _coordinator.CopyBank(SelectedItem);
            };

            var copyAmountButton = new Button
            {
                Content = "نسخ القيمة",
                Style = WorkspaceSurfaceChrome.Style("BaseButton"),
                FontSize = 9.5
            };
            copyAmountButton.Click += (_, _) =>
            {
                _coordinator.CopyAmount(SelectedItem);
            };

            var copyBeneficiaryButton = new Button
            {
                Content = "نسخ المستفيد",
                Style = WorkspaceSurfaceChrome.Style("PrimaryButton"),
                FontSize = 9.5
            };
            copyBeneficiaryButton.Click += (_, _) =>
            {
                _coordinator.CopyBeneficiary(SelectedItem);
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

            var actions = new Grid { FlowDirection = FlowDirection.LeftToRight, Margin = new Thickness(0, 9, 0, 0) };
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.Children.Add(copyAmountButton);
            Grid.SetColumn(copyNameButton, 2);
            actions.Children.Add(copyNameButton);
            Grid.SetColumn(copyBeneficiaryButton, 4);
            actions.Children.Add(copyBeneficiaryButton);
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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.4, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.9, GridUnitType.Star) });
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

        private BankWorkspaceItem? SelectedItem
            => (_list.SelectedItem as FrameworkElement)?.Tag as BankWorkspaceItem;

        private void ApplyFilters()
        {
            _list.Items.Clear();
            string selectedSort = _sortFilter.SelectedItem as string ?? "الأعلى قيمة";
            BanksWorkspaceFilterResult filtered = _dataService.BuildFilteredItems(
                _allBanks,
                _searchInput.Text,
                selectedSort);
            foreach (BankWorkspaceItem item in filtered.Items)
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

        private FrameworkElement BuildRow(BankWorkspaceItem item)
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
            actions.Children.Add(CreateRowButton("نسخ", "Icon.Document", item, CopyRowBank_Click));
            actions.Children.Add(CreateRowButton("عرض", "Icon.View", item, SelectRow_Click));
            Grid.SetColumn(actions, 0);
            row.Children.Add(actions);

            row.Children.Add(BuildCell(item.CountDisplay, 1, "TableCellCenter"));
            row.Children.Add(BuildCell(item.ActiveDisplay, 2, "TableCellCenter", WorkspaceSurfaceChrome.BrushFrom("#16A34A")));
            row.Children.Add(BuildCell(item.ExpiringDisplay, 3, "TableCellCenter", WorkspaceSurfaceChrome.BrushFrom("#E09408")));
            row.Children.Add(BuildCell(item.ExpiredDisplay, 4, "TableCellCenter", WorkspaceSurfaceChrome.BrushFrom("#EF4444")));
            row.Children.Add(BuildCell(item.AmountDisplay, 5, "TableCellCenter"));
            row.Children.Add(BuildCell(item.TopBeneficiary, 6, "TableCellRight"));

            var bankCell = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft,
                VerticalAlignment = VerticalAlignment.Center
            };
            bankCell.Children.Add(new Image
            {
                Source = item.Logo,
                Width = 18,
                Height = 18,
                Margin = new Thickness(0, 0, 8, 0)
            });
            bankCell.Children.Add(new TextBlock
            {
                Text = item.Bank,
                Style = WorkspaceSurfaceChrome.Style("TableCellRight")
            });
            Grid.SetColumn(bankCell, 7);
            row.Children.Add(bankCell);

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

        private static Button CreateRowButton(string text, string iconKey, BankWorkspaceItem item, RoutedEventHandler handler)
        {
            var button = new Button
            {
                Content = BuildRowButtonContent(text, iconKey),
                Tag = item,
                Style = WorkspaceSurfaceChrome.Style("RowButton")
            };
            UiInstrumentation.Identify(
                button,
                UiInstrumentation.SanitizeAutomationKey($"Banks.RowAction.{text}", item.Bank),
                $"{text} | {item.Bank}");
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

        private void CopyRowBank_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
            _coordinator.CopyBank(SelectedItem);
        }

        private void SelectRowFromSender(object sender)
        {
            if (sender is not FrameworkElement element || element.Tag is not BankWorkspaceItem item)
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

        private void ApplyMetrics(BanksWorkspaceMetrics metrics)
        {
            _bankCountValue.Text = metrics.BankCount;
            _guaranteeCountValue.Text = metrics.GuaranteeCount;
            _activeValue.Text = metrics.ActiveCount;
            _amountValue.Text = metrics.Amount;
        }

        private void ApplyDetailState(BanksWorkspaceDetailState state)
        {
            _detailLogo.Source = state.Logo;
            _detailTitle.Text = state.Title;
            _detailSubtitle.Text = state.Subtitle;
            _detailStatusBadge.Text = state.BadgeText;
            _detailStatusBadge.Foreground = state.BadgeForeground;
            _detailStatusBadgeBorder.Background = state.BadgeBackground;
            _detailStatusBadgeBorder.BorderBrush = state.BadgeBorder;
            _detailAmountHeadline.Text = state.AmountHeadline;
            _detailAmountHeadline.Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A");
            _detailAmountCaption.Text = state.AmountCaption;
            _detailCount.Text = state.Count;
            _detailActive.Text = state.Active;
            _detailActive.Foreground = state.ActiveBrush;
            _detailExpiring.Text = state.Expiring;
            _detailExpiring.Foreground = state.ExpiringBrush;
            _detailExpired.Text = state.Expired;
            _detailExpired.Foreground = state.ExpiredBrush;
            _detailShare.Text = state.Share;
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

        private static TextBlock BuildAmountHeadline()
        {
            return new TextBlock
            {
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A"),
                Margin = new Thickness(0, 12, 0, 0),
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

    }
}
