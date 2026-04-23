using System;
using System.Windows;
using System.Windows.Controls;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Microsoft.Win32;

namespace GuaranteeManager.Views
{
    public partial class RecordWorkflowResponseWindow : Window
    {
        private readonly RequestType _requestType;

        public RequestStatus SelectedStatus { get; private set; } = RequestStatus.Executed;
        public string ResponseNotes { get; private set; } = string.Empty;
        public string ResponseDocumentPath { get; private set; } = string.Empty;
        public bool PromoteResponseDocumentToOfficialAttachment { get; private set; }

        public RecordWorkflowResponseWindow(WorkflowRequestListItem requestItem)
        {
            _requestType = requestItem.Request.Type;
            InitializeComponent();
            ButtonIconContentFactory.Apply(BtnBrowseResponseFile, "Icon_Geometry_Attachment", "اختيار مستند الرد");
            ButtonIconContentFactory.Apply(BtnCancel, "Icon_Geometry_Close", "إغلاق دون حفظ");

            TxtGuaranteeNo.Text = requestItem.GuaranteeNo;
            TxtRequestType.Text = requestItem.Request.TypeLabel;
            TxtCurrentExpiry.Text = requestItem.CurrentValueLabel;
            TxtRequestedExpiry.Text = requestItem.RequestedValueLabel;
            LblCurrentValue.Text = requestItem.Request.Type switch
            {
                RequestType.Extension => "الانتهاء الحالي",
                RequestType.Reduction => "المبلغ الحالي",
                RequestType.Release => "الحالة الحالية",
                RequestType.Liquidation => "الحالة الحالية",
                RequestType.Verification => "الحالة الحالية",
                RequestType.Replacement => "رقم الضمان الحالي",
                _ => "القيمة الحالية"
            };
            LblRequestedValue.Text = requestItem.Request.Type switch
            {
                RequestType.Extension => "التاريخ المطلوب",
                RequestType.Reduction => "المبلغ المطلوب",
                RequestType.Release => "الإجراء المطلوب",
                RequestType.Liquidation => "الإجراء المطلوب",
                RequestType.Verification => "الإجراء المطلوب",
                RequestType.Replacement => "رقم الضمان البديل",
                _ => "المطلوب"
            };

            UpdateResultGuidance();
            UpdatePromoteOption();
            UpdateSaveButtonLabel();
        }

        private void Result_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbResult.SelectedItem is not ComboBoxItem selectedItem || selectedItem.Tag is not string tagValue)
            {
                return;
            }

            SelectedStatus = Enum.TryParse(tagValue, true, out RequestStatus status)
                ? status
                : RequestStatus.Executed;

            UpdateResultGuidance();
            UpdatePromoteOption();
            UpdateSaveButtonLabel();
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = false,
                Filter = "Document Files|*.pdf;*.jpg;*.jpeg;*.png;*.doc;*.docx;*.xls;*.xlsx|All Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                ResponseDocumentPath = dialog.FileName;
                TxtResponseFile.Text = dialog.FileName;
                UpdatePromoteOption();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            PromoteResponseDocumentToOfficialAttachment = ChkPromoteResponseDocument.IsChecked == true;

            if (PromoteResponseDocumentToOfficialAttachment && SelectedStatus != RequestStatus.Executed)
            {
                AppDialogService.ShowWarning("يمكن إنشاء إصدار رسمي جديد بمرفق معتمد فقط عند اختيار نتيجة منفذة.");
                return;
            }

            if (PromoteResponseDocumentToOfficialAttachment && string.IsNullOrWhiteSpace(ResponseDocumentPath))
            {
                AppDialogService.ShowWarning("يرجى اختيار مستند رد البنك أولاً إذا أردت إضافته كمرفق رسمي للضمان.");
                return;
            }

            ResponseNotes = TxtResponseNotes.Text.Trim();

            if (!ConfirmCommitment())
            {
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdatePromoteOption()
        {
            if (ChkPromoteResponseDocument == null || TxtPromoteGuidance == null)
            {
                return;
            }

            bool showPromoteOption = _requestType == RequestType.Verification;
            ChkPromoteResponseDocument.Visibility = showPromoteOption ? Visibility.Visible : Visibility.Collapsed;
            TxtPromoteGuidance.Visibility = showPromoteOption ? Visibility.Visible : Visibility.Collapsed;

            if (!showPromoteOption)
            {
                ChkPromoteResponseDocument.IsChecked = false;
                ChkPromoteResponseDocument.IsEnabled = false;
                PromoteResponseDocumentToOfficialAttachment = false;
                ChkPromoteResponseDocument.ToolTip = "هذا الخيار يخص طلبات التحقق فقط.";
                TxtPromoteGuidance.Text = string.Empty;
                return;
            }

            bool isExecuted = SelectedStatus == RequestStatus.Executed;
            bool hasResponseDocument = !string.IsNullOrWhiteSpace(ResponseDocumentPath);
            bool canPromote = isExecuted && hasResponseDocument;
            ChkPromoteResponseDocument.IsEnabled = canPromote;
            if (!canPromote)
            {
                ChkPromoteResponseDocument.IsChecked = false;
                PromoteResponseDocumentToOfficialAttachment = false;
            }

            ToolTipService.SetShowOnDisabled(ChkPromoteResponseDocument, true);

            if (!isExecuted)
            {
                ChkPromoteResponseDocument.ToolTip = "غير متاح الآن - يظهر هذا الخيار فقط عندما تكون النتيجة المختارة منفذة.";
                TxtPromoteGuidance.Text = "ترقية مستند رد البنك إلى ملف داعم رسمي تعمل فقط عند اختيار نتيجة منفذة.";
                return;
            }

            if (!hasResponseDocument)
            {
                ChkPromoteResponseDocument.ToolTip = "غير متاح الآن - اختر مستند رد البنك أولًا لتفعيل هذا الخيار.";
                TxtPromoteGuidance.Text = "اختر مستند رد البنك أولًا إذا أردت اعتماده ضمن الملفات الداعمة الرسمية وإنشاء إصدار جديد.";
                return;
            }

            ChkPromoteResponseDocument.ToolTip = "استخدم هذا الخيار إذا كان مستند رد البنك سيصبح جزءًا رسميًا من سجل الضمان.";
            TxtPromoteGuidance.Text = "يمكنك الآن اعتماد مستند رد البنك كمرفق رسمي. عند تحديد هذا الخيار سيُنشأ إصدار جديد مرتبط بالضمان.";
        }

        private void UpdateResultGuidance()
        {
            if (TxtResultGuidance == null)
            {
                return;
            }

            TxtResultGuidance.Text = SelectedStatus switch
            {
                RequestStatus.Executed => "استخدم هذه النتيجة عندما يكون رد البنك أو الإجراء النهائي مؤكدًا. مستند رد البنك اختياري في هذه المرحلة، ويمكن إلحاقه لاحقًا من شاشة الطلبات إذا وصل بعد التأكيد.",
                RequestStatus.Rejected => _requestType switch
                {
                    RequestType.Extension => "التمديد لم يُعتمد. الضمان يبقى قائمًا بتاريخ انتهائه الحالي، لذا راجع الحاجة إلى متابعة جديدة أو إجراء بديل قبل الانتهاء.",
                    RequestType.Release => "الإفراج لم يُعتمد. الضمان يبقى قائمًا حتى يصل رد منفذ أو يُتخذ إجراء بديل موثق.",
                    RequestType.Liquidation => "التسييل لم يُنفذ. راجع مع البنك ما إذا كانت هناك متابعة مطلوبة أو نتيجة مختلفة يجب تسجيلها.",
                    RequestType.Reduction => "التخفيض لم يُعتمد. مبلغ الضمان لن يتغير حتى يصل رد منفذ أو يُنشأ طلب جديد.",
                    RequestType.Verification => "التحقق لم يُعتمد. يبقى السجل دون تغيير، ويمكن توثيق الخطوة التالية في الملاحظات.",
                    RequestType.Replacement => "الاستبدال لم يُعتمد. الضمان الحالي يبقى قائمًا إلى أن يصل رد منفذ أو يُنشأ طلب بديل.",
                    _ => "الطلب رُفض. راجع الخطوة التالية المطلوبة مع البنك قبل إغلاق المتابعة."
                },
                RequestStatus.Cancelled => "أغلق الطلب بهذه النتيجة فقط عندما يكون قد فقد الحاجة التشغيلية أو استُبدل بطلب أحدث ومعلوم السبب.",
                _ => string.Empty
            };
        }

        private void UpdateSaveButtonLabel()
        {
            if (BtnSave == null)
            {
                return;
            }

            string label = SelectedStatus switch
            {
                RequestStatus.Executed => "تأكيد الرد ومتابعة تنفيذ الطلب",
                RequestStatus.Rejected => "تأكيد الرد ومتابعة إغلاق الطلب",
                RequestStatus.Cancelled => "تأكيد الرد ومتابعة إلغاء الطلب",
                _ => "تأكيد الرد ومتابعة التسجيل"
            };

            ButtonIconContentFactory.Apply(BtnSave, "Icon_Geometry_Confirm", label);
        }

        private bool ConfirmCommitment()
        {
            string statusEffect = SelectedStatus switch
            {
                RequestStatus.Executed => "سيغلق هذا النموذج أولًا، ثم يسجل البرنامج الرد كنتيجة منفذة ويطبق أثر الطلب على السجل.",
                RequestStatus.Rejected => "سيغلق هذا النموذج أولًا، ثم يسجل البرنامج الرد كنتيجة مرفوضة ويغلق الطلب دون تنفيذ.",
                RequestStatus.Cancelled => "سيغلق هذا النموذج أولًا، ثم يسجل البرنامج الرد كنتيجة مُلغاة ويغلق الطلب إداريًا.",
                _ => "سيغلق هذا النموذج أولًا، ثم يسجل البرنامج الرد المختار."
            };

            string responseDocumentEffect = string.IsNullOrWhiteSpace(ResponseDocumentPath)
                ? "لن يُحفظ مستند رد بنك مع هذا الطلب الآن، ويمكن إلحاقه لاحقًا من شاشة الطلبات."
                : PromoteResponseDocumentToOfficialAttachment
                    ? "سيُحفظ مستند رد البنك، ويُعتمد ضمن الملفات الداعمة الرسمية، وسيُنشأ إصدار جديد مرتبط."
                    : "سيُحفظ مستند رد البنك مع هذا الطلب.";

            string notesEffect = string.IsNullOrWhiteSpace(ResponseNotes)
                ? "لن تُحفظ ملاحظات إضافية مع الرد."
                : "ستُحفظ الملاحظات المدخلة مع هذا الرد.";

            string message =
                "سيتم الآن اعتماد الخطوات التالية:" + Environment.NewLine +
                $"- {statusEffect}" + Environment.NewLine +
                $"- {responseDocumentEffect}" + Environment.NewLine +
                $"- {notesEffect}" + Environment.NewLine + Environment.NewLine +
                "هل تريد متابعة تسجيل رد البنك بهذه الصورة؟";

            MessageBoxImage image = SelectedStatus == RequestStatus.Executed
                ? MessageBoxImage.Question
                : MessageBoxImage.Warning;

            return AppDialogService.Ask(
                message,
                "تأكيد تسجيل رد البنك",
                MessageBoxButton.YesNo,
                image) == MessageBoxResult.Yes;
        }
    }
}
