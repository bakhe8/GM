using System;
using System.Windows;
using System.Windows.Controls;
using GuaranteeManager.Services;

namespace GuaranteeManager
{
    internal static class DialogFormSupport
    {
        private const double ActionGap = 8d;
        private const double ActionTopMargin = 14d;
        private const double DefaultButtonHeight = 32d;

        public static void WireDirtyTracking(Action markDirty, params FrameworkElement[] elements)
        {
            foreach (FrameworkElement element in elements)
            {
                switch (element)
                {
                    case TextBox textBox:
                        textBox.TextChanged += (_, _) => markDirty();
                        break;
                    case ComboBox comboBox:
                        comboBox.SelectionChanged += (_, _) => markDirty();
                        if (comboBox.IsEditable)
                        {
                            comboBox.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => markDirty()));
                        }
                        break;
                }
            }
        }

        public static bool ConfirmDiscardChanges()
        {
            return App.CurrentApp.GetRequiredService<IAppDialogService>().Confirm(
                "لديك تعديلات غير محفوظة. هل تريد إغلاق النافذة وفقدان هذه التعديلات؟",
                "تأكيد الإغلاق");
        }

        public static Grid BuildActionBar(Button primaryButton, Button secondaryButton, double primaryWidth = 104d, double secondaryWidth = 96d)
        {
            ConfigureActionButton(primaryButton, primaryWidth);
            ConfigureActionButton(secondaryButton, secondaryWidth);

            var actions = new Grid
            {
                FlowDirection = FlowDirection.LeftToRight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, ActionTopMargin, 0, 0)
            };
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var buttonGroup = new Grid
            {
                FlowDirection = FlowDirection.LeftToRight,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            buttonGroup.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(secondaryWidth) });
            buttonGroup.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ActionGap) });
            buttonGroup.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(primaryWidth) });

            Grid.SetColumn(secondaryButton, 0);
            Grid.SetColumn(primaryButton, 2);
            buttonGroup.Children.Add(secondaryButton);
            buttonGroup.Children.Add(primaryButton);
            actions.Children.Add(buttonGroup);
            return actions;
        }

        public static Grid BuildSingleActionBar(Button actionButton, double width = 96d)
        {
            ConfigureActionButton(actionButton, width);

            var actions = new Grid
            {
                FlowDirection = FlowDirection.LeftToRight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, ActionTopMargin, 0, 0)
            };
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            actionButton.HorizontalAlignment = HorizontalAlignment.Left;
            actions.Children.Add(actionButton);
            return actions;
        }

        private static void ConfigureActionButton(Button button, double width)
        {
            button.Width = width;
            button.Height = DefaultButtonHeight;
            button.Margin = new Thickness(0);
            button.FlowDirection = FlowDirection.RightToLeft;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.VerticalContentAlignment = VerticalAlignment.Center;
        }
    }
}
