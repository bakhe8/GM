using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    public static class ContextActionPanelFactory
    {
        private static readonly CornerRadius SharpCorner = new(0);

        public static void Populate(
            Panel host,
            IReadOnlyList<ContextActionSection> sections,
            Func<string, RoutedEventHandler?> handlerResolver,
            bool isEnabled,
            params string[] primaryActionIds)
        {
            Populate(host, sections, handlerResolver, isEnabled, null, primaryActionIds);
        }

        public static void Populate(
            Panel host,
            IReadOnlyList<ContextActionSection> sections,
            Func<string, RoutedEventHandler?> handlerResolver,
            bool isEnabled,
            Func<ContextActionDefinition, ContextActionAvailability>? availabilityResolver = null,
            params string[] primaryActionIds)
        {
            host.Children.Clear();

            HashSet<string> primaryIds = new(primaryActionIds ?? Array.Empty<string>(), StringComparer.Ordinal);

            foreach (ContextActionSection section in sections)
            {
                if (section.Items.Count == 0)
                {
                    continue;
                }

                host.Children.Add(BuildSectionCard(section, handlerResolver, isEnabled, availabilityResolver, primaryIds));
            }
        }

        private static FrameworkElement BuildSectionCard(
            ContextActionSection section,
            Func<string, RoutedEventHandler?> handlerResolver,
            bool isEnabled,
            Func<ContextActionDefinition, ContextActionAvailability>? availabilityResolver,
            HashSet<string> primaryActionIds)
        {
            ContextActionDefinition[] leafActions = section.Items.SelectMany(FlattenLeafActions).ToArray();

            var border = new Border
            {
                Background = GetBrush("Surface_Card_White", Brushes.White),
                BorderBrush = GetBrush("Surface_Border", Brushes.Gainsboro),
                BorderThickness = new Thickness(1),
                CornerRadius = SharpCorner,
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var stack = new StackPanel();
            border.Child = stack;

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            headerGrid.Children.Add(new TextBlock
            {
                Text = section.Header,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = GetBrush("Brand_Slate", Brushes.Black)
            });

            var badge = new Border
            {
                Background = GetBrush("Neutral_050", Brushes.WhiteSmoke),
                BorderBrush = GetBrush("Neutral_300", Brushes.Gainsboro),
                BorderThickness = new Thickness(1),
                CornerRadius = SharpCorner,
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            badge.Child = new TextBlock
            {
                Text = $"{leafActions.Length} إجراء",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("Neutral_700", Brushes.DimGray)
            };

            Grid.SetColumn(badge, 1);
            headerGrid.Children.Add(badge);
            stack.Children.Add(headerGrid);

            if (!string.IsNullOrWhiteSpace(section.Description))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = section.Description,
                    FontSize = 11,
                    Foreground = GetBrush("Neutral_700", Brushes.DimGray),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            var wrap = new WrapPanel
            {
                Margin = new Thickness(0, 10, 0, 0),
                Orientation = Orientation.Horizontal
            };

            foreach (ContextActionDefinition action in leafActions)
            {
                RoutedEventHandler? handler = handlerResolver(action.Id!);
                if (handler == null)
                {
                    continue;
                }

                ContextActionAvailability availability = availabilityResolver?.Invoke(action) ?? ContextActionAvailability.Enabled();
                bool buttonEnabled = isEnabled && availability.IsEnabled;
                string toolTip = buttonEnabled
                    ? action.PolicyTooltip
                    : BuildDisabledTooltip(action, availability.DisabledReason);

                var button = new Button
                {
                    Content = action.Header,
                    ToolTip = toolTip,
                    MinWidth = 132,
                    MinHeight = 34,
                    Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(0, 0, 6, 6),
                    IsEnabled = buttonEnabled
                };

                ToolTipService.SetShowOnDisabled(button, true);

                if (TryFindStyle(primaryActionIds.Contains(action.Id!) ? "Button.Primary" : "Button.Subtle", out Style? style))
                {
                    button.Style = style;
                }

                ButtonIconContentFactory.Apply(button, ContextActionIconResolver.ResolveGeometryKey(action), action.Header);

                if (action.IsDestructive)
                {
                    button.Foreground = GetBrush("Brand_Slate", Brushes.Black);
                }

                button.Click += handler;
                wrap.Children.Add(button);
            }

            stack.Children.Add(wrap);
            return border;
        }

        private static IEnumerable<ContextActionDefinition> FlattenLeafActions(ContextActionDefinition definition)
        {
            if (definition.IsLeaf)
            {
                yield return definition;
                yield break;
            }

            foreach (ContextActionDefinition child in definition.Children.SelectMany(FlattenLeafActions))
            {
                yield return child;
            }
        }

        private static bool TryFindStyle(string resourceKey, out Style? style)
        {
            style = Application.Current.TryFindResource(resourceKey) as Style;
            return style != null;
        }

        private static Brush GetBrush(string resourceKey, Brush fallback)
        {
            return Application.Current.TryFindResource(resourceKey) as Brush ?? fallback;
        }

        private static string BuildDisabledTooltip(ContextActionDefinition action, string? disabledReason)
        {
            string reason = string.IsNullOrWhiteSpace(disabledReason)
                ? "هذا الإجراء غير متاح في الحالة الحالية."
                : disabledReason.Trim();

            if (string.IsNullOrWhiteSpace(action.Description))
            {
                return $"غير متاح الآن{Environment.NewLine}{reason}";
            }

            return $"{action.Description}{Environment.NewLine}غير متاح الآن - {reason}";
        }
    }
}
