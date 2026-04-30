using System.Diagnostics;
using System.IO;
using System.Windows;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed class ShellSessionCoordinator
    {
        public ShellLastFileState RememberLastFile(GuaranteeRow row)
        {
            return new ShellLastFileState(
                row.RootId,
                row.GuaranteeNo,
                $"{row.Supplier} | {row.Bank}");
        }

        public Guarantee? ResolveLastFileGuarantee(ShellLastFileState state, IDatabaseService database)
        {
            if (!state.HasLastFile)
            {
                return null;
            }

            Guarantee? guarantee = database.GetCurrentGuaranteeByRootId(state.RootId);
            if (guarantee != null)
            {
                return guarantee;
            }

            MessageBox.Show(
                "تعذر العثور على آخر ضمان تم تحديده. ربما تم حذفه أو تغييره.",
                "استئناف آخر ضمان",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return null;
        }

        public void OpenAttachment(AttachmentItem? item)
        {
            if (item == null)
            {
                return;
            }

            OpenPath(item.FilePath, "المرفق");
        }

        private static void OpenPath(string path, string label)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                MessageBox.Show(
                    $"تعذر فتح {label}. الملف غير موجود.",
                    label,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
