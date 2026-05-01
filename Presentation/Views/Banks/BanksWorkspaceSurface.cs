using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed class BanksWorkspaceSurface : UserControl
    {
        private readonly BanksWorkspaceDataService _dataService;
        private readonly List<BankWorkspaceItem> _allBanks;
        private readonly ListBox _list = new();
        private readonly TextBox _searchInput = new();
        private readonly Button _highestValueSortButton = new();
        private readonly Button _mostCountSortButton = new();
        private readonly Button _mostActiveSortButton = new();
        private readonly TextBlock _summary = BuildMutedText(12, FontWeights.SemiBold);
        private readonly TextBlock _bankCountValue = BuildMetricValue();
        private readonly TextBlock _guaranteeCountValue = BuildMetricValue();
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
        private readonly Action<string?> _showGuaranteesForBank;
        private readonly Action<string> _addBankReference;
        private readonly ReferenceTablePagerController _pager;
        private string _selectedSortFilter = BankSortFilters.HighestValue;

        public BanksWorkspaceSurface(
            IReadOnlyList<Guarantee> guarantees,
            IReadOnlyList<string> bankReferences,
            Action<string?> showGuaranteesForBank,
            Action<string> addBankReference,
            string? initialSearchText = null)
        {
            _dataService = new BanksWorkspaceDataService();
            _allBanks = _dataService.BuildItems(guarantees, bankReferences);
            _showGuaranteesForBank = showGuaranteesForBank;
            _addBankReference = addBankReference;
            _pager = new ReferenceTablePagerController("Banks", "بنك", 10, ApplyFilters);
            UiInstrumentation.Identify(this, "Banks.Workspace", "البنوك");
            UiInstrumentation.Identify(_searchInput, "Banks.SearchBox", "بحث البنوك");
            UiInstrumentation.Identify(_highestValueSortButton, "Banks.Filter.Sort.HighestValue", BankSortFilters.HighestValue);
            UiInstrumentation.Identify(_mostCountSortButton, "Banks.Filter.Sort.MostCount", BankSortFilters.MostCount);
            UiInstrumentation.Identify(_mostActiveSortButton, "Banks.Filter.Sort.MostActive", BankSortFilters.MostActive);
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
                BuildToolbarBlock(),
                BuildMetrics(),
                BuildTableSection(),
                BuildDetailPanel());
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
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            UIElement sortButtons = BuildSortButtons();
            Grid.SetColumn(sortButtons, 0);
            toolbar.Children.Add(sortButtons);

            _searchInput.TextChanged += (_, _) =>
            {
                _pager.ResetToFirstPage();
                ApplyFilters();
            };
            var searchBox = WorkspaceSurfaceChrome.ToolbarSearchBox(_searchInput, "ابحث باسم البنك أو المورد الأعلى...");
            Grid.SetColumn(searchBox, 2);
            toolbar.Children.Add(searchBox);

            return toolbar;
        }

        private UIElement BuildSortButtons()
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft
            };

            ConfigureSortButton(_highestValueSortButton, BankSortFilters.HighestValue);
            ConfigureSortButton(_mostCountSortButton, BankSortFilters.MostCount);
            ConfigureSortButton(_mostActiveSortButton, BankSortFilters.MostActive);

            panel.Children.Add(_highestValueSortButton);
            panel.Children.Add(_mostCountSortButton);
            panel.Children.Add(_mostActiveSortButton);
            UpdateSortButtons();
            return panel;
        }

        private void ConfigureSortButton(Button button, string sortFilter)
        {
            button.Content = sortFilter;
            button.Tag = sortFilter;
            button.Height = 36;
            button.MinWidth = 112;
            button.FontSize = 11;
            button.Margin = new Thickness(0, 0, 8, 0);
            button.Click += (_, _) => SelectSortFilter(sortFilter);
            AutomationProperties.SetName(button, sortFilter);
        }

        private void SelectSortFilter(string sortFilter, bool resetPage = true, bool apply = true)
        {
            string normalizedSort = BankSortFilters.Normalize(sortFilter);
            bool changed = !string.Equals(_selectedSortFilter, normalizedSort, StringComparison.Ordinal);
            _selectedSortFilter = normalizedSort;
            UpdateSortButtons();

            if (resetPage)
            {
                _pager.ResetToFirstPage();
            }

            if (apply && (changed || resetPage))
            {
                ApplyFilters();
            }
        }

        private void UpdateSortButtons()
        {
            ApplySortButtonState(_highestValueSortButton, BankSortFilters.HighestValue);
            ApplySortButtonState(_mostCountSortButton, BankSortFilters.MostCount);
            ApplySortButtonState(_mostActiveSortButton, BankSortFilters.MostActive);
        }

        private void ApplySortButtonState(Button button, string sortFilter)
        {
            bool selected = string.Equals(_selectedSortFilter, sortFilter, StringComparison.Ordinal);
            button.Style = WorkspaceSurfaceChrome.Style(selected ? "PrimaryButton" : "BaseButton");
        }

        private System.Windows.Controls.Primitives.UniformGrid BuildMetrics()
        {
            var metrics = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 3
            };
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("عدد البنوك", _bankCountValue, "#2563EB"));
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("إجمالي الضمانات", _guaranteeCountValue, "#0F172A"));
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("إجمالي القيمة", _amountValue, "#E09408"));
            WorkspaceSurfaceChrome.ApplyMetricCardSpacing(metrics);
            return metrics;
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
            AddHeader(inner, "المورد الأعلى", 6, true);
            AddHeader(inner, "البنك", 7, true);
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
            _detailAmountCaption.HorizontalAlignment = HorizontalAlignment.Left;
            _detailAmountCaption.TextAlignment = TextAlignment.Right;

            return new StackPanel
            {
                Margin = new Thickness(16, 14, 16, 14),
                Children =
                {
                    BuildLogoHeader(),
                    _detailAmountHeadline,
                    _detailAmountCaption,
                    new Border { Height = 1, Background = WorkspaceSurfaceChrome.BrushFrom("#EDF2F7"), Margin = new Thickness(0, 13, 0, 12) },
                    WorkspaceSurfaceChrome.DetailFactLine("عدد الضمانات", _detailCount, "Icon.Guarantees"),
                    WorkspaceSurfaceChrome.DetailFactLine("نشطة", _detailActive, "Icon.Check"),
                    WorkspaceSurfaceChrome.DetailFactLine("قريبة الانتهاء", _detailExpiring, "Icon.Calendar"),
                    WorkspaceSurfaceChrome.DetailFactLine("منتهية", _detailExpired, "Icon.Close"),
                    WorkspaceSurfaceChrome.DetailFactLine("حصة المحفظة", _detailShare, "Icon.Reports")
                }
            };
        }

        private UIElement BuildLogoHeader()
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
            row.Children.Add(_detailLogo);

            var textStack = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.RightToLeft };
            titleRow.Children.Add(_detailTitle);
            textStack.Children.Add(titleRow);

            var subtitleRow = new StackPanel { Orientation = Orientation.Horizontal, FlowDirection = FlowDirection.RightToLeft };
            subtitleRow.Children.Add(_detailSubtitle);
            textStack.Children.Add(subtitleRow);
            row.Children.Add(textStack);
            Grid.SetColumn(row, 1);
            grid.Children.Add(row);
            return grid;
        }

        private Border BuildDetailActions()
        {
            var addBankButton = new Button
            {
                Content = "إضافة بنك",
                Style = WorkspaceSurfaceChrome.Style("PrimaryButton"),
                FontSize = 9.5
            };
            UiInstrumentation.Identify(addBankButton, "Banks.QuickAction.AddBank", "إضافة بنك");
            addBankButton.Click += AddBank_Click;

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
            actions.Children.Add(addBankButton);
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
            string selectedSort = BankSortFilters.Normalize(_selectedSortFilter);
            UpdateSortButtons();
            BanksWorkspaceFilterResult filtered = _dataService.BuildFilteredItems(
                _allBanks,
                _searchInput.Text,
                selectedSort);
            IReadOnlyList<BankWorkspaceItem> pageItems = _pager.Page(filtered.Items);
            foreach (BankWorkspaceItem item in pageItems)
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
            actions.Children.Add(CreateRowButton("عرض", "Icon.View", item, ShowBankGuarantees_Click));
            Grid.SetColumn(actions, 0);
            row.Children.Add(actions);

            row.Children.Add(BuildCell(item.CountDisplay, 1, "TableCellCenter"));
            row.Children.Add(BuildCell(item.ActiveDisplay, 2, "TableCellCenter", WorkspaceSurfaceChrome.BrushFrom("#16A34A")));
            row.Children.Add(BuildCell(item.ExpiringDisplay, 3, "TableCellCenter", WorkspaceSurfaceChrome.BrushFrom("#E09408")));
            row.Children.Add(BuildCell(item.ExpiredDisplay, 4, "TableCellCenter", WorkspaceSurfaceChrome.BrushFrom("#EF4444")));
            row.Children.Add(BuildCell(item.AmountDisplay, 5, "TableCellCenter"));
            row.Children.Add(BuildCell(item.TopSupplier, 6, "TableCellRight"));

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

        private void ShowBankGuarantees_Click(object sender, RoutedEventArgs e)
        {
            SelectRowFromSender(sender);
            _showGuaranteesForBank(SelectedItem?.Bank);
        }

        private void AddBank_Click(object sender, RoutedEventArgs e)
        {
            if (!GuidedTextPromptDialog.TryShow(
                    "إضافة بنك",
                    "أدخل اسم البنك الجديد ليظهر في صفحة البنوك وقوائم اختيار البنك.",
                    "اسم البنك",
                    "إضافة",
                    string.Empty,
                    out string bankName))
            {
                return;
            }

            string normalizedBankName = bankName.Trim();
            if (_allBanks.Exists(item => item.Bank.Equals(normalizedBankName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("هذا البنك موجود بالفعل.", "إضافة بنك", MessageBoxButton.OK, MessageBoxImage.Information);
                _searchInput.Text = normalizedBankName;
                SelectBank(normalizedBankName);
                return;
            }

            try
            {
                _addBankReference(normalizedBankName);
                _allBanks.Add(BankWorkspaceItem.Empty(normalizedBankName));
                _searchInput.Text = normalizedBankName;
                _pager.ResetToFirstPage();
                ApplyFilters();
                SelectBank(normalizedBankName);
                App.CurrentApp.GetRequiredService<IShellStatusService>().ShowInfo(
                    "تمت إضافة البنك.",
                    $"البنوك • {normalizedBankName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "إضافة بنك", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SelectBank(string bankName)
        {
            foreach (object row in _list.Items)
            {
                if (row is FrameworkElement frameworkElement
                    && frameworkElement.Tag is BankWorkspaceItem item
                    && item.Bank.Equals(bankName, StringComparison.OrdinalIgnoreCase))
                {
                    _list.SelectedItem = frameworkElement;
                    frameworkElement.Focus();
                    return;
                }
            }
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
            _amountValue.Text = metrics.Amount;
            _amountValue.FontSize = metrics.Amount.Length > 18 ? 22 : 32;
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
            return WorkspaceSurfaceChrome.MetricValueText();
        }

        private static TextBlock BuildAmountHeadline()
        {
            return new TextBlock
            {
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A"),
                Margin = new Thickness(0, 12, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
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

        private static class BankSortFilters
        {
            public const string HighestValue = "الأعلى قيمة";
            public const string MostCount = "الأكثر عدداً";
            public const string MostActive = "الأعلى نشاطاً";

            public static string Normalize(string? sortFilter)
            {
                return sortFilter switch
                {
                    MostCount => MostCount,
                    MostActive => MostActive,
                    _ => HighestValue
                };
            }
        }

    }
}
