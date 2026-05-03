#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GuaranteeManager.Models;
using GuaranteeManager.Services;

namespace GuaranteeManager.Development
{
    internal static class DialogAuditRunner
    {
        private const int Srccopy = 0x00CC0020;
        private const int Captureblt = 0x40000000;
        private static readonly string OutputRoot = Path.GetFullPath(
            Path.Combine(Environment.CurrentDirectory, "scratch", "DialogAudit"));
        private static readonly string ScreenshotRoot = Path.Combine(OutputRoot, "runtime-screenshots");
        private static readonly string ReportPath = Path.Combine(OutputRoot, "dialog-audit-runtime.md");
        private static readonly string TracePath = Path.Combine(OutputRoot, "dialog-audit-runtime-trace.txt");

        public static void Start(Window owner, bool exitAfterAudit)
        {
            Directory.CreateDirectory(ScreenshotRoot);
            foreach (string oldScreenshot in Directory.EnumerateFiles(ScreenshotRoot, "*.png"))
            {
                File.Delete(oldScreenshot);
            }

            File.WriteAllText(TracePath, "started inside application" + Environment.NewLine);

            IReadOnlyList<DialogSpec> specs = BuildSpecs();
            var records = new List<DialogRecord>();
            int index = 0;

            owner.Dispatcher.BeginInvoke(new Action(ShowNext), DispatcherPriority.ApplicationIdle);

            void ShowNext()
            {
                if (index >= specs.Count)
                {
                    WriteReport(records);
                    Trace("finished");
                    if (exitAfterAudit)
                    {
                        Application.Current.Shutdown();
                    }

                    return;
                }

                DialogSpec spec = specs[index++];
                Trace($"creating {spec.Order} {spec.Key}");

                Window window = spec.Create();
                window.Owner = owner;
                window.ShowInTaskbar = false;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = owner.Left + 96;
                window.Top = owner.Top + 96;
                window.Topmost = true;

                window.Loaded += (_, _) =>
                {
                    window.Activate();
                    var timer = new DispatcherTimer(DispatcherPriority.Background)
                    {
                        Interval = TimeSpan.FromMilliseconds(700)
                    };
                    timer.Tick += (_, _) =>
                    {
                        timer.Stop();
                        string screenshotPath = Path.Combine(ScreenshotRoot, $"{spec.Order:00}-{spec.Key}.png");
                        Trace($"capturing {spec.Order} {spec.Key}");
                        CaptureFromScreen(window, screenshotPath);
                        records.Add(new DialogRecord(
                            spec.Order,
                            spec.Key,
                            spec.Name,
                            window.Title,
                            window.ActualWidth,
                            window.ActualHeight,
                            screenshotPath));
                        AllowAuditClose(window);
                        window.Close();
                        Trace($"closed {spec.Order} {spec.Key}");
                        owner.Dispatcher.BeginInvoke(new Action(ShowNext), DispatcherPriority.ApplicationIdle);
                    };
                    timer.Start();
                };

                window.Show();
            }
        }

        private static IReadOnlyList<DialogSpec> BuildSpecs()
        {
            Guarantee guarantee = BuildGuarantee();
            WorkflowRequest pendingRequest = BuildRequest(RequestType.Extension, RequestStatus.Pending, guarantee.DateCalendar);
            WorkflowRequest closedRequest = BuildRequest(RequestType.Release, RequestStatus.Executed, guarantee.DateCalendar);
            WorkflowRequestListItem requestItem = BuildRequestItem(guarantee, closedRequest);
            var banks = new List<string>
            {
                "بنك ساب",
                "البنك الأهلي السعودي",
                "مصرف الراجحي",
                "بنك الرياض"
            };
            var types = new List<string>
            {
                "ابتدائي",
                "نهائي",
                "صيانة"
            };

            return new List<DialogSpec>
            {
                new(1, "new-guarantee", "إضافة ضمان جديد", () => Create<NewGuaranteeDialog>(banks, types, (Func<string, bool>)(_ => true))),
                new(2, "edit-guarantee", "تعديل ضمان", () => Create<EditGuaranteeDialog>(guarantee, banks, types, (Func<string, bool>)(_ => true))),
                new(3, "replacement-request", "طلب استبدال", () => Create<ReplacementRequestDialog>(guarantee, banks, types, (Func<string, bool>)(_ => true))),
                new(4, "bank-response", "تسجيل رد البنك", () => Create<BankResponseDialog>(new List<WorkflowRequest> { pendingRequest })),
                new(5, "attach-response", "إلحاق مستند رد البنك", () => Create<AttachResponseDocumentDialog>(requestItem)),
                new(6, "attachment-picker", "عرض المرفقات", () => Create<AttachmentPickerDialog>(guarantee.Attachments, "مرفقات الضمان - BG-2026-0016")),
                new(7, "release-request", "طلب إفراج", () => Create<ConfirmationDialog>("طلب إفراج", "تأكيد إنشاء طلب إفراج للضمان BG-2026-0016؟", "إنشاء الطلب", "إلغاء")),
                new(8, "extension-request", "طلب تمديد", () => Create<PromptDialog>("طلب تمديد", "تاريخ الانتهاء المطلوب", "2026/12/31")),
                new(9, "reduction-request", "طلب تخفيض", () => Create<PromptDialog>("طلب تخفيض", "المبلغ المطلوب بعد التخفيض", "800,000.25")),
                new(10, "liquidation-request", "طلب تسييل", () => Create<PromptDialog>("طلب تسييل", "ملاحظات الطلب", "طلب تسييل من واجهة الضمانات.")),
                new(11, "verification-request", "طلب تحقق", () => Create<PromptDialog>("طلب تحقق", "ملاحظات الطلب", "طلب تحقق من واجهة الضمانات.")),
                new(12, "add-bank", "إضافة بنك", () => Create<GuidedTextPromptDialog>("إضافة بنك", "أدخل اسم البنك الجديد ليظهر في صفحة البنوك وقوائم اختيار البنك.", "اسم البنك", "إضافة", string.Empty, null))
            };
        }

        private static T Create<T>(params object?[] args)
            where T : Window
        {
            ConstructorInfo? constructor = typeof(T)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .FirstOrDefault(ctor => ctor.GetParameters().Length == args.Length);

            if (constructor == null)
            {
                throw new InvalidOperationException($"No matching private constructor was found for {typeof(T).Name}.");
            }

            return (T)constructor.Invoke(args);
        }

        private static Guarantee BuildGuarantee()
        {
            return new Guarantee
            {
                Id = 16,
                RootId = 16,
                GuaranteeNo = "BG-2026-0016",
                Supplier = "شركة الأنظمة الأمنية الحديثة",
                Bank = "مصرف الراجحي",
                Beneficiary = "مستشفى الملك فيصل التخصصي ومركز الأبحاث",
                GuaranteeType = "ابتدائي",
                ReferenceType = GuaranteeReferenceType.Contract,
                ReferenceNumber = "7200-عقد",
                Amount = 950000.25m,
                ExpiryDate = new DateTime(2026, 6, 4),
                DateCalendar = GuaranteeDateCalendar.Gregorian,
                LifecycleStatus = GuaranteeLifecycleStatus.Active,
                VersionNumber = 1,
                IsCurrent = true,
                Notes = "بيانات عينة لمراجعة تصميم النافذة.",
                Attachments = new List<AttachmentRecord>
                {
                    new()
                    {
                        Id = 1,
                        GuaranteeId = 16,
                        OriginalFileName = "صورة الضمان.pdf",
                        SavedFileName = "sample-guarantee.pdf",
                        FileExtension = ".pdf",
                        DocumentType = AttachmentDocumentType.GuaranteeImage,
                        UploadedAt = new DateTime(2025, 12, 29, 10, 48, 0)
                    },
                    new()
                    {
                        Id = 2,
                        GuaranteeId = 16,
                        OriginalFileName = "خطاب التغطية.pdf",
                        SavedFileName = "sample-letter.pdf",
                        FileExtension = ".pdf",
                        DocumentType = AttachmentDocumentType.SupportingDocument,
                        UploadedAt = new DateTime(2026, 1, 2, 9, 9, 0)
                    }
                }
            };
        }

        private static WorkflowRequest BuildRequest(RequestType type, RequestStatus status, GuaranteeDateCalendar calendar)
        {
            WorkflowRequestedData data = type switch
            {
                RequestType.Extension => new WorkflowRequestedData
                {
                    RequestedExpiryDate = new DateTime(2026, 12, 31),
                    RequestedDateCalendar = calendar
                },
                RequestType.Reduction => new WorkflowRequestedData
                {
                    RequestedAmount = 800000.25m
                },
                _ => new WorkflowRequestedData()
            };

            return new WorkflowRequest
            {
                Id = type == RequestType.Extension ? 1001 : 1002,
                RootGuaranteeId = 16,
                BaseVersionId = 16,
                SequenceNumber = 2,
                Type = type,
                Status = status,
                DateCalendar = calendar,
                RequestDate = new DateTime(2026, 1, 2, 9, 9, 0),
                CreatedAt = new DateTime(2026, 1, 2, 9, 9, 0),
                UpdatedAt = new DateTime(2026, 1, 3, 14, 49, 0),
                ResponseRecordedAt = status == RequestStatus.Pending ? null : new DateTime(2026, 1, 3, 14, 49, 0),
                RequestedDataJson = JsonSerializer.Serialize(data),
                LetterOriginalFileName = "خطاب الطلب.pdf",
                LetterSavedFileName = "letter-sample.pdf",
                ResponseOriginalFileName = status == RequestStatus.Pending ? string.Empty : "رد البنك.pdf",
                ResponseSavedFileName = status == RequestStatus.Pending ? string.Empty : "response-sample.pdf",
                Notes = "طلب عينة لمراجعة التصميم."
            };
        }

        private static WorkflowRequestListItem BuildRequestItem(Guarantee guarantee, WorkflowRequest request)
        {
            return new WorkflowRequestListItem
            {
                Request = request,
                CurrentGuaranteeId = guarantee.Id,
                RootGuaranteeId = guarantee.RootId ?? guarantee.Id,
                GuaranteeNo = guarantee.GuaranteeNo,
                Supplier = guarantee.Supplier,
                Bank = guarantee.Bank,
                ReferenceType = guarantee.ReferenceType,
                ReferenceNumber = guarantee.ReferenceNumber,
                CurrentAmount = guarantee.Amount,
                CurrentExpiryDate = guarantee.ExpiryDate,
                CurrentDateCalendar = guarantee.DateCalendar,
                CurrentVersionNumber = guarantee.VersionNumber,
                BaseVersionNumber = guarantee.VersionNumber,
                LifecycleStatus = guarantee.LifecycleStatus
            };
        }

        private static void CaptureFromScreen(Window window, string path)
        {
            HwndSource? source = (HwndSource?)PresentationSource.FromVisual(window);
            double scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1d;
            double scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1d;
            Point topLeft = window.PointToScreen(new Point(0, 0));
            int x = (int)Math.Round(topLeft.X);
            int y = (int)Math.Round(topLeft.Y);
            int width = Math.Max(1, (int)Math.Round(window.ActualWidth * scaleX));
            int height = Math.Max(1, (int)Math.Round(window.ActualHeight * scaleY));

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memoryDc = CreateCompatibleDC(screenDc);
            IntPtr bitmapHandle = CreateCompatibleBitmap(screenDc, width, height);
            IntPtr previous = SelectObject(memoryDc, bitmapHandle);

            try
            {
                if (!BitBlt(memoryDc, 0, 0, width, height, screenDc, x, y, Srccopy | Captureblt))
                {
                    throw new InvalidOperationException("Could not capture dialog screenshot from the screen.");
                }

                BitmapSource bitmap = Imaging.CreateBitmapSourceFromHBitmap(
                    bitmapHandle,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bitmap.Freeze();

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using FileStream stream = File.Create(path);
                encoder.Save(stream);
            }
            finally
            {
                SelectObject(memoryDc, previous);
                DeleteObject(bitmapHandle);
                DeleteDC(memoryDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        private static void AllowAuditClose(Window window)
        {
            FieldInfo? field = window.GetType().GetField("_allowCloseWithoutPrompt", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.FieldType == typeof(bool))
            {
                field.SetValue(window, true);
            }
        }

        private static void WriteReport(IReadOnlyList<DialogRecord> records)
        {
            var lines = new List<string>
            {
                "# Dialog Audit Runtime Capture",
                string.Empty,
                $"Generated: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}",
                string.Empty,
                "| # | Dialog | Title | Size | Screenshot |",
                "|---|---|---|---:|---|"
            };

            foreach (DialogRecord record in records)
            {
                string screenshot = Path.GetRelativePath(OutputRoot, record.ScreenshotPath).Replace('\\', '/');
                lines.Add($"| {record.Order} | {record.Name} | {record.Title} | {record.Width:N0} x {record.Height:N0} | `{screenshot}` |");
            }

            File.WriteAllLines(ReportPath, lines);
        }

        private static void Trace(string line)
            => File.AppendAllText(
                TracePath,
                DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + line + Environment.NewLine);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr handle);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr handle);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(
            IntPtr destination,
            int destinationX,
            int destinationY,
            int width,
            int height,
            IntPtr source,
            int sourceX,
            int sourceY,
            int rasterOperation);

        private sealed record DialogSpec(int Order, string Key, string Name, Func<Window> Create);

        private sealed record DialogRecord(
            int Order,
            string Key,
            string Name,
            string Title,
            double Width,
            double Height,
            string ScreenshotPath);
    }
}
#endif
