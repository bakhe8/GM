using System.Windows;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Views
{
    public partial class TextPromptWindow : Window
    {
        public string ResultText { get; private set; } = string.Empty;

        public TextPromptWindow(string title, string prompt, string label, string confirmButtonText, string initialValue = "", string? nextStepHint = null)
        {
            InitializeComponent();
            Title = title;
            TxtTitle.Text = title;
            TxtPrompt.Text = prompt;
            TxtLabel.Text = label;
            ButtonIconContentFactory.Apply(BtnConfirm, "Icon_Geometry_Confirm", confirmButtonText);
            ButtonIconContentFactory.Apply(BtnCancel, "Icon_Geometry_Close", "إغلاق دون متابعة");
            TxtValue.Text = initialValue;

            if (!string.IsNullOrWhiteSpace(nextStepHint))
            {
                TxtActionHint.Text = nextStepHint;
                ActionHintBorder.Visibility = Visibility.Visible;
            }

            Loaded += (_, _) =>
            {
                TxtValue.Focus();
                TxtValue.SelectAll();
            };
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            string value = TxtValue.Text.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                AppDialogService.ShowWarning("يرجى إدخال القيمة المطلوبة قبل المتابعة.");
                return;
            }

            ResultText = value;
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
