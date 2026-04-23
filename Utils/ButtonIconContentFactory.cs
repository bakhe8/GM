using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Linq;

namespace GuaranteeManager.Utils
{
    public static class ButtonIconContentFactory
    {
        public static object Create(FrameworkElement owner, string iconGeometryKey, string text)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            if (TryCreateOfficialIcon(owner, iconGeometryKey) is Rectangle officialIcon)
            {
                panel.Children.Add(officialIcon);
            }
            else
            if (owner.TryFindResource(iconGeometryKey) is Geometry geometry)
            {
                var path = new Path
                {
                    Data = geometry
                };

                if (owner.TryFindResource("Path_ButtonIcon") is Style iconStyle)
                {
                    path.Style = iconStyle;
                }

                panel.Children.Add(path);
            }

            panel.Children.Add(new TextBlock
            {
                Text = text,
                VerticalAlignment = VerticalAlignment.Center
            });

            return panel;
        }

        private static Rectangle? TryCreateOfficialIcon(FrameworkElement owner, string iconGeometryKey)
        {
            if (owner.TryFindResource(GetOfficialMaskKey(iconGeometryKey)) is not Brush maskBrush)
            {
                return null;
            }

            var rectangle = new Rectangle
            {
                OpacityMask = maskBrush
            };

            if (owner.TryFindResource("Rectangle_ButtonIcon") is Style iconStyle)
            {
                rectangle.Style = iconStyle;
            }

            BindingOperations.SetBinding(
                rectangle,
                Shape.FillProperty,
                new Binding("Foreground")
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1)
                });

            return rectangle;
        }

        private static string GetOfficialMaskKey(string iconGeometryKey)
        {
            return iconGeometryKey switch
            {
                "Icon_Geometry_Open" => "Icon_Mask_Open",
                "Icon_Geometry_Output" => "Icon_Mask_Output",
                "Icon_Geometry_Close" => "Icon_Mask_Close",
                "Icon_Geometry_Journey" => "Icon_Mask_Journey",
                "Icon_Geometry_Advisor" => "Icon_Mask_Advisor",
                _ => string.Empty
            };
        }

        public static void Apply(Button button, string iconGeometryKey, string text)
        {
            button.Content = Create(button, iconGeometryKey, text);
        }

        public static string ExtractText(object? content)
        {
            if (content is TextBlock textBlock)
            {
                return textBlock.Text;
            }

            if (content is string rawText)
            {
                return rawText;
            }

            if (content is Panel panel)
            {
                var text = panel.Children
                    .OfType<TextBlock>()
                    .Select(block => block.Text)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

                return text ?? string.Empty;
            }

            return content?.ToString() ?? string.Empty;
        }
    }
}
