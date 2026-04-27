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
    public sealed class RequestDisplayItem
    {
        public RequestDisplayItem(WorkflowRequest request)
        {
            Request = request;
            string responseLabel = request.ResponseRecordedAt.HasValue
                ? $"رد: {request.ResponseRecordedAt.Value:yyyy/MM/dd}"
                : "بدون رد";
            Display = $"{request.RequestDate:yyyy/MM/dd} | {request.TypeLabel} | {request.StatusLabel} | {request.RequestedValueLabel} | {responseLabel}";
        }

        public WorkflowRequest Request { get; }
        public string Display { get; }
    }

    public sealed class RequestListDisplayItem
    {
        public RequestListDisplayItem(WorkflowRequestListItem item)
        {
            Item = item;
            Title = $"{item.GuaranteeNo} | {item.Request.TypeLabel} | {item.Request.StatusLabel}";
            Meta = $"{item.Supplier} | {item.Bank}";
            Value = $"{item.RequestedValueFieldLabel}: {item.RequestedValueDisplay} | {item.CurrentVersionLabel} | رد: {item.ResponseDateLabel}";
            Display = $"{Title}\n{Meta}\n{Value}";
            GuaranteeNo = item.GuaranteeNo;
            Supplier = item.Supplier;
            Bank = item.Bank;
            RequestType = item.Request.TypeLabel;
            RequestStatus = item.Request.StatusLabel;
            RequestStatusBrush = item.Request.Status switch
            {
                GuaranteeManager.Models.RequestStatus.Pending => TonePalette.Foreground(Tone.Warning),
                GuaranteeManager.Models.RequestStatus.Executed => TonePalette.Foreground(Tone.Success),
                GuaranteeManager.Models.RequestStatus.Rejected => TonePalette.Foreground(Tone.Danger),
                GuaranteeManager.Models.RequestStatus.Cancelled => TonePalette.Foreground(Tone.Info),
                GuaranteeManager.Models.RequestStatus.Superseded => TonePalette.Foreground(Tone.Info),
                _ => TonePalette.Foreground(Tone.Info)
            };
            RequestedValue = item.RequestedValueDisplay;
            CurrentValue = item.CurrentValueDisplay;
            RequestDate = item.Request.RequestDate.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            ResponseDate = item.ResponseDateLabel;
            VersionLabel = item.CurrentVersionLabel;
        }

        public WorkflowRequestListItem Item { get; }
        public string Title { get; }
        public string Meta { get; }
        public string Value { get; }
        public string Display { get; }
        public string GuaranteeNo { get; }
        public string Supplier { get; }
        public string Bank { get; }
        public ImageSource BankLogo => GuaranteeRow.ResolveBankLogo(Bank);
        public string RequestType { get; }
        public string RequestStatus { get; }
        public Brush RequestStatusBrush { get; }
        public string RequestedValue { get; }
        public string CurrentValue { get; }
        public string RequestDate { get; }
        public string ResponseDate { get; }
        public string VersionLabel { get; }
        public bool CanRegisterResponse => Item.Request.Status == GuaranteeManager.Models.RequestStatus.Pending;
        public bool CanOpenLetter => Item.Request.HasLetter;
        public bool CanOpenResponse => Item.Request.HasResponseDocument;
        public bool CanAttachResponseDocument => Item.Request.Status != GuaranteeManager.Models.RequestStatus.Pending && !Item.Request.HasResponseDocument;
        public bool CanUseResponseAction => CanOpenResponse || CanAttachResponseDocument;
        public bool CanRunQueueAction => CanRegisterResponse || CanUseResponseAction;
        public string QueueActionLabel => CanOpenResponse ? "فتح الرد" : CanAttachResponseDocument ? "إلحاق الرد" : "رد البنك";
        public string QueueActionHint => CanOpenResponse
            ? "يفتح مستند رد البنك المحفوظ لهذا السجل."
            : CanAttachResponseDocument
                ? "هذا الطلب مغلق ولا يملك مستند رد بعد، ويمكن إلحاقه من هنا."
                : CanRegisterResponse
                    ? "الطلب معلق ويمكن تسجيل رد البنك عليه مباشرة."
                    : "لا يوجد إجراء استجابة متاح لهذا السجل حاليًا.";
    }

    public sealed class AttachmentItem
    {
        public AttachmentItem(string name, string date, string size, string filePath)
        {
            Name = name;
            Date = date;
            Size = size;
            FilePath = filePath;
        }

        public string Name { get; }
        public string Date { get; }
        public string Size { get; }
        public string FilePath { get; }

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
                attachment.UploadedAt.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture),
                size,
                attachment.FilePath);
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
            _ => BrushFrom("#3B82F6")
        };

        public static Brush Background(Tone tone) => tone switch
        {
            Tone.Success => BrushFrom("#F2FBF4"),
            Tone.Warning => BrushFrom("#FFF9EC"),
            Tone.Danger => BrushFrom("#FFF3F3"),
            _ => BrushFrom("#F2F7FF")
        };

        public static Brush Border(Tone tone) => tone switch
        {
            Tone.Success => BrushFrom("#C9EFCF"),
            Tone.Warning => BrushFrom("#F6DE99"),
            Tone.Danger => BrushFrom("#F7C5C5"),
            _ => BrushFrom("#CADCFF")
        };

        private static SolidColorBrush BrushFrom(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }
    }

}
