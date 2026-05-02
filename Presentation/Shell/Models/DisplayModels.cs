using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Microsoft.Win32;

namespace GuaranteeManager
{
    public sealed class AttachmentItem
    {
        public AttachmentItem(string name, string date, string size, string filePath, string fileKind, string documentType)
        {
            Name = name;
            Date = date;
            Size = size;
            FilePath = filePath;
            FileKind = fileKind;
            DocumentType = documentType;
            Display = $"{documentType} | {name}";
        }

        public string Name { get; }
        public string Date { get; }
        public string Size { get; }
        public string FilePath { get; }
        public string FileKind { get; }
        public string DocumentType { get; }
        public string Display { get; }

        public static AttachmentItem FromAttachment(AttachmentRecord attachment)
        {
            string size = "---";
            if (attachment.Exists)
            {
                var info = new FileInfo(attachment.FilePath);
                size = FormatFileSize(info.Length);
            }

            return new AttachmentItem(
                string.IsNullOrWhiteSpace(attachment.OriginalFileName) ? "مرفق" : attachment.OriginalFileName,
                DualCalendarDateService.FormatGregorianDate(attachment.UploadedAt),
                size,
                attachment.FilePath,
                FormatFileKind(attachment),
                attachment.DocumentTypeLabel);
        }

        private static string FormatFileKind(AttachmentRecord attachment)
        {
            string extension = attachment.FileExtension;
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = Path.GetExtension(attachment.OriginalFileName);
            }

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = Path.GetExtension(attachment.FilePath);
            }

            extension = extension.Trim().TrimStart('.');
            return string.IsNullOrWhiteSpace(extension)
                ? "FILE"
                : extension.ToUpperInvariant();
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes >= 1024 * 1024)
            {
                return $"{bytes / 1024d / 1024d:0.#} MB";
            }

            return $"{Math.Max(1, bytes / 1024).ToString("N0", CultureInfo.InvariantCulture)} KB";
        }
    }

    public enum Tone
    {
        Success,
        Warning,
        Danger,
        Info
    }

    public static class TonePalette
    {
        public static Brush Foreground(Tone tone) => tone switch
        {
            Tone.Success => BrushFrom("#16A34A"),
            Tone.Warning => BrushFrom("#E09408"),
            Tone.Danger => BrushFrom("#EF4444"),
            _ => BrushFrom("#2563EB")
        };

        public static Brush Background(Tone tone) => tone switch
        {
            Tone.Success => BrushFrom("#F2FBF4"),
            Tone.Warning => BrushFrom("#FFF9EC"),
            Tone.Danger => BrushFrom("#FFF3F3"),
            _ => BrushFrom("#EFF6FF")
        };

        public static Brush Border(Tone tone) => tone switch
        {
            Tone.Success => BrushFrom("#C9EFCF"),
            Tone.Warning => BrushFrom("#F6DE99"),
            Tone.Danger => BrushFrom("#F7C5C5"),
            _ => BrushFrom("#BFDBFE")
        };

        private static SolidColorBrush BrushFrom(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }
    }

}
