using GuaranteeManager.Models;

namespace GuaranteeManager.Contracts
{
    public interface IGuaranteeFileWorkspace : IRefreshableView
    {
        void SetRequestFocus(int? requestId);

        void LoadGuarantee(Guarantee guarantee, bool userInitiated = false);

        void FocusSection(GuaranteeFileFocusArea area);
    }
}
