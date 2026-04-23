using System.Windows;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Microsoft.Win32;

namespace GuaranteeManager.Views
{
    public partial class AttachWorkflowResponseDocumentWindow : Window
    {
        public string ResponseDocumentPath { get; private set; } = string.Empty;
        public string AdditionalNotes { get; private set; } = string.Empty;

        public AttachWorkflowResponseDocumentWindow(WorkflowRequestListItem requestItem)
        {
            InitializeComponent();
            TxtGuaranteeNo.Text = requestItem.GuaranteeNo;
            TxtRequestType.Text = requestItem.Request.TypeLabel;
            TxtRequestStatus.Text = requestItem.Request.StatusLabel;
            TxtRequestSequence.Text = requestItem.Request.SequenceNumber.ToString();
            ButtonIconContentFactory.Apply(BtnBrowseResponseFile, "Icon_Geometry_Attachment", "اختيار مستند الرد");
            ButtonIconContentFactory.Apply(BtnSave, "Icon_Geometry_Confirm", "إلحاق المستند");
            ButtonIconContentFactory.Apply(BtnCancel, "Icon_Geometry_Close", "إغلاق دون حفظ");
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Multiselect = false,
                Filter = "Document Files|*.pdf;*.jpg;*.jpeg;*.png;*.doc;*.docx;*.xls;*.xlsx|All Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                ResponseDocumentPath = dialog.FileName;
                TxtResponseFile.Text = dialog.FileName;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ResponseDocumentPath))
            {
                AppDialogService.ShowWarning("يرجى اختيار مستند رد البنك قبل المتابعة.");
                return;
            }

            AdditionalNotes = TxtAdditionalNotes.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
