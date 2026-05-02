using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace GuaranteeManager
{
    public sealed record ReferenceTablePagerButtonItem(
        int PageNumber,
        string Label,
        bool IsCurrent,
        string AutomationId,
        string AutomationName);

    public sealed class ReferenceTablePagerController
    {
        private readonly string _automationPrefix;
        private readonly string _itemLabel;
        private readonly int _pageSize;
        private readonly Action _pageChanged;
        private readonly StackPanel _buttons = new()
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        private int _totalItems;

        public ReferenceTablePagerController(string automationPrefix, string itemLabel, int pageSize, Action pageChanged)
        {
            _automationPrefix = automationPrefix;
            _itemLabel = itemLabel;
            _pageSize = Math.Max(1, pageSize);
            _pageChanged = pageChanged;
        }

        public int CurrentPage { get; private set; } = 1;

        public int PageSize => _pageSize;

        public int TotalPages => CalculateTotalPages(_totalItems, _pageSize);

        public static int CalculateTotalPages(int totalItems, int pageSize)
        {
            return Math.Max(1, (int)Math.Ceiling(Math.Max(0, totalItems) / (double)Math.Max(1, pageSize)));
        }

        public static IReadOnlyList<int> BuildVisiblePageNumbers(int currentPage, int totalPages)
        {
            int safeTotal = Math.Max(1, totalPages);
            return Enumerable.Range(1, safeTotal).ToList();
        }

        public Grid BuildFooter(TextBlock summary)
        {
            var footer = new Grid
            {
                Style = WorkspaceSurfaceChrome.Style("ReferenceTablePager")
            };

            footer.Children.Add(_buttons);
            summary.Style = WorkspaceSurfaceChrome.Style("ReferenceTableFooterSummary");
            footer.Children.Add(summary);
            RenderButtons();
            return footer;
        }

        public IReadOnlyList<T> Page<T>(IReadOnlyList<T> items)
        {
            _totalItems = items.Count;
            CurrentPage = Math.Clamp(CurrentPage, 1, TotalPages);
            RenderButtons();
            return items
                .Skip((CurrentPage - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();
        }

        public void ResetToFirstPage()
        {
            CurrentPage = 1;
        }

        public void MoveToItemIndex(int itemIndex)
        {
            CurrentPage = itemIndex < 0
                ? 1
                : Math.Max(1, (itemIndex / _pageSize) + 1);
        }

        public string BuildSummary()
        {
            if (_totalItems == 0)
            {
                return $"لا توجد {_itemLabel} مطابقة";
            }

            int start = ((CurrentPage - 1) * _pageSize) + 1;
            int end = Math.Min(CurrentPage * _pageSize, _totalItems);
            return string.Format(
                CultureInfo.InvariantCulture,
                "عرض {0:N0} - {1:N0} من {2:N0} {3}",
                start,
                end,
                _totalItems,
                _itemLabel);
        }

        public static string BuildSummary(int totalItems, int pageSize, int currentPage, string itemLabel)
        {
            if (totalItems == 0)
            {
                return $"لا توجد {itemLabel} مطابقة";
            }

            int safePageSize = Math.Max(1, pageSize);
            int totalPages = CalculateTotalPages(totalItems, safePageSize);
            int safeCurrent = Math.Clamp(currentPage, 1, totalPages);
            int start = ((safeCurrent - 1) * safePageSize) + 1;
            int end = Math.Min(safeCurrent * safePageSize, totalItems);
            return string.Format(
                CultureInfo.InvariantCulture,
                "عرض {0:N0} - {1:N0} من {2:N0} {3}",
                start,
                end,
                totalItems,
                itemLabel);
        }

        private void GoToPage(int pageNumber)
        {
            int targetPage = Math.Clamp(pageNumber, 1, TotalPages);
            if (targetPage == CurrentPage)
            {
                return;
            }

            CurrentPage = targetPage;
            _pageChanged();
        }

        private void RenderButtons()
        {
            _buttons.Children.Clear();
            _buttons.Children.Add(BuildNavigationButton("←", CurrentPage - 1, CurrentPage > 1, "Previous", "الصفحة السابقة"));

            foreach (int pageNumber in BuildVisiblePageNumbers(CurrentPage, TotalPages).Reverse())
            {
                var button = new Button
                {
                    Content = pageNumber.ToString(CultureInfo.InvariantCulture),
                    Margin = new Thickness(6, 0, 0, 0),
                    Style = WorkspaceSurfaceChrome.Style(pageNumber == CurrentPage
                        ? "ReferenceTablePagerActiveButton"
                        : "ReferenceTablePagerButton")
                };
                UiInstrumentation.Identify(
                    button,
                    $"{_automationPrefix}.Pager.Page.{pageNumber.ToString(CultureInfo.InvariantCulture)}",
                    $"الصفحة {pageNumber.ToString(CultureInfo.InvariantCulture)}");
                int targetPage = pageNumber;
                button.Click += (_, _) => GoToPage(targetPage);
                _buttons.Children.Add(button);
            }

            var nextButton = BuildNavigationButton("→", CurrentPage + 1, CurrentPage < TotalPages, "Next", "الصفحة التالية");
            nextButton.Margin = new Thickness(6, 0, 0, 0);
            _buttons.Children.Add(nextButton);
            _buttons.Children.Add(new Button
            {
                Content = _pageSize.ToString(CultureInfo.InvariantCulture),
                IsEnabled = false,
                MinWidth = 46,
                Margin = new Thickness(12, 0, 0, 0),
                Style = WorkspaceSurfaceChrome.Style("ReferenceTablePagerButton")
            });
            _buttons.Children.Add(new TextBlock
            {
                Text = "لكل صفحة",
                FontSize = 11,
                Foreground = WorkspaceSurfaceChrome.BrushResource("Brush.Text.Secondary"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            });
        }

        private Button BuildNavigationButton(string label, int targetPage, bool isEnabled, string automationKey, string automationName)
        {
            var button = new Button
            {
                Content = label,
                IsEnabled = isEnabled,
                Style = WorkspaceSurfaceChrome.Style("ReferenceTablePagerButton")
            };
            AutomationProperties.SetAutomationId(button, $"{_automationPrefix}.Pager.{automationKey}");
            AutomationProperties.SetName(button, automationName);
            button.Click += (_, _) => GoToPage(targetPage);
            return button;
        }
    }
}
