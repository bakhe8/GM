using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    public static class ContextActionMenuFactory
    {
        public static ContextMenu Build(
            IReadOnlyList<ContextActionSection> sections,
            Func<string, RoutedEventHandler?> handlerResolver,
            Func<ContextActionDefinition, ContextActionAvailability>? availabilityResolver = null)
        {
            var menu = new ContextMenu();

            for (int i = 0; i < sections.Count; i++)
            {
                if (i > 0)
                {
                    menu.Items.Add(new Separator());
                }

                menu.Items.Add(BuildSection(sections[i], handlerResolver, availabilityResolver));
            }

            return menu;
        }

        public static ContextMenu Build(
            string sectionHeader,
            string sectionDescription,
            IReadOnlyList<ContextActionDefinition> actions,
            Func<string, RoutedEventHandler?> handlerResolver,
            Func<ContextActionDefinition, ContextActionAvailability>? availabilityResolver = null)
        {
            return Build(
                new[]
                {
                    new ContextActionSection(sectionHeader, sectionDescription, actions.ToArray())
                },
                handlerResolver,
                availabilityResolver);
        }

        private static MenuItem BuildSection(
            ContextActionSection section,
            Func<string, RoutedEventHandler?> handlerResolver,
            Func<ContextActionDefinition, ContextActionAvailability>? availabilityResolver)
        {
            var item = new MenuItem
            {
                Header = section.Header,
                ToolTip = section.Description,
                FontWeight = FontWeights.SemiBold,
                Icon = CreateMenuIcon(ContextActionIconResolver.ResolveSectionGeometryKey(section.Header, section.Description))
            };

            foreach (ContextActionDefinition child in section.Items)
            {
                item.Items.Add(BuildNode(child, handlerResolver, availabilityResolver));
            }

            return item;
        }

        private static object BuildNode(
            ContextActionDefinition definition,
            Func<string, RoutedEventHandler?> handlerResolver,
            Func<ContextActionDefinition, ContextActionAvailability>? availabilityResolver)
        {
            ContextActionAvailability availability = availabilityResolver?.Invoke(definition) ?? ContextActionAvailability.Enabled();
            var item = new MenuItem
            {
                Header = definition.Header,
                Icon = CreateMenuIcon(ContextActionIconResolver.ResolveGeometryKey(definition))
            };

            if (definition.IsDestructive)
            {
                item.Foreground = GetBrush("Brand_Slate", Brushes.Black);
                item.FontWeight = FontWeights.SemiBold;
            }

            if (definition.HasChildren)
            {
                foreach (ContextActionDefinition child in definition.Children)
                {
                    item.Items.Add(BuildNode(child, handlerResolver, availabilityResolver));
                }

                item.ToolTip = availability.IsEnabled
                    ? definition.PolicyTooltip
                    : BuildDisabledTooltip(definition, availability.DisabledReason);
                item.IsEnabled = availability.IsEnabled;
                return item;
            }

            if (!definition.IsLeaf || string.IsNullOrWhiteSpace(definition.Id))
            {
                item.ToolTip = BuildDisabledTooltip(definition, availability.DisabledReason);
                item.IsEnabled = false;
                return item;
            }

            RoutedEventHandler? handler = handlerResolver(definition.Id);
            bool isEnabled = availability.IsEnabled && handler != null;

            item.ToolTip = isEnabled
                ? definition.PolicyTooltip
                : BuildDisabledTooltip(definition, availability.DisabledReason);

            if (!isEnabled)
            {
                item.IsEnabled = false;
                return item;
            }

            item.Click += handler;
            return item;
        }

        public static ContextActionDefinition? FindActionById(IEnumerable<ContextActionSection> sections, string actionId)
        {
            foreach (ContextActionSection section in sections)
            {
                ContextActionDefinition? action = FindActionById(section.Items, actionId);
                if (action != null)
                {
                    return action;
                }
            }

            return null;
        }

        public static IReadOnlyList<ContextActionDefinition> FindActionsByIds(IEnumerable<ContextActionSection> sections, params string[] actionIds)
        {
            var found = new List<ContextActionDefinition>();
            foreach (string actionId in actionIds)
            {
                ContextActionDefinition? action = FindActionById(sections, actionId);
                if (action != null)
                {
                    found.Add(action);
                }
            }

            return found;
        }

        private static ContextActionDefinition? FindActionById(IEnumerable<ContextActionDefinition> definitions, string actionId)
        {
            foreach (ContextActionDefinition definition in definitions)
            {
                if (string.Equals(definition.Id, actionId, StringComparison.Ordinal))
                {
                    return definition;
                }

                if (!definition.HasChildren)
                {
                    continue;
                }

                ContextActionDefinition? child = FindActionById(definition.Children, actionId);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private static string BuildDisabledTooltip(ContextActionDefinition definition, string? disabledReason)
        {
            string reason = string.IsNullOrWhiteSpace(disabledReason)
                ? "هذا الإجراء غير متاح في الحالة الحالية."
                : disabledReason.Trim();

            if (string.IsNullOrWhiteSpace(definition.Description))
            {
                return $"غير متاح الآن{Environment.NewLine}{reason}";
            }

            return $"{definition.Description}{Environment.NewLine}غير متاح الآن - {reason}";
        }

        private static object? CreateMenuIcon(string geometryKey, Brush? strokeOverride = null)
        {
            if (Application.Current.TryFindResource(geometryKey) is not Geometry geometry)
            {
                return null;
            }

            var path = new Path
            {
                Data = geometry,
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                StrokeThickness = 1.7,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = Brushes.Transparent,
                Stroke = strokeOverride ?? GetBrush("Neutral_700", Brushes.DimGray),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            return path;
        }

        private static Brush GetBrush(string resourceKey, Brush fallback)
        {
            return Application.Current.TryFindResource(resourceKey) as Brush ?? fallback;
        }
    }
}
