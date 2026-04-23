using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GuaranteeManager.Services;

namespace GuaranteeManager.Utils
{
    public static class WindowLocalFeedbackPresenter
    {
        public static void Show(
            Border border,
            TextBlock messageBlock,
            string message,
            ShellStatusTone tone = ShellStatusTone.Info,
            Action<string, ShellStatusTone>? mirrorAction = null)
        {
            messageBlock.Text = message;
            border.Visibility = Visibility.Visible;

            switch (tone)
            {
                case ShellStatusTone.Success:
                    ApplyColors(border, messageBlock, "#FFFFFF", "#006847", "#006847");
                    break;
                case ShellStatusTone.Warning:
                    ApplyColors(border, messageBlock, "#F7F7F7", "#666666", "#111111");
                    break;
                case ShellStatusTone.Error:
                    ApplyColors(border, messageBlock, "#F2F2F2", "#111111", "#111111");
                    break;
                default:
                    ApplyColors(border, messageBlock, "#FFFFFF", "#CFCFCF", "#111111");
                    break;
            }

            mirrorAction?.Invoke(message, tone);
        }

        private static void ApplyColors(Border border, TextBlock messageBlock, string backgroundHex, string borderHex, string foregroundHex)
        {
            border.Background = BrushFromHex(backgroundHex);
            border.BorderBrush = BrushFromHex(borderHex);
            messageBlock.Foreground = BrushFromHex(foregroundHex);
        }

        private static Brush BrushFromHex(string hex)
        {
            return (Brush)new BrushConverter().ConvertFromString(hex)!;
        }
    }
}
