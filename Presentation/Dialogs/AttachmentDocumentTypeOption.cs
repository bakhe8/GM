using GuaranteeManager.Models;

namespace GuaranteeManager
{
    internal sealed record AttachmentDocumentTypeOption(AttachmentDocumentType Value, string Label)
    {
        public override string ToString() => Label;
    }
}
