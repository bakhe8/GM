using System;
using System.Collections.Generic;

namespace GuaranteeManager.Models
{
    public enum AttachmentDocumentType
    {
        GuaranteeImage,
        BankResponse,
        SupportingDocument,
        RequestLetter,
        Other
    }

    public sealed record AttachmentInput(string FilePath, AttachmentDocumentType DocumentType, string TimelineEventKey = "")
    {
        public string DocumentTypeLabel => AttachmentDocumentTypeText.Label(DocumentType);

        public static AttachmentInput SupportingDocument(string filePath)
            => new(filePath, AttachmentDocumentType.SupportingDocument);
    }

    public static class AttachmentDocumentTypeText
    {
        public static IReadOnlyList<AttachmentDocumentType> OfficialAttachmentTypes { get; } =
        [
            AttachmentDocumentType.GuaranteeImage,
            AttachmentDocumentType.SupportingDocument,
            AttachmentDocumentType.BankResponse,
            AttachmentDocumentType.RequestLetter,
            AttachmentDocumentType.Other
        ];

        public static string Label(AttachmentDocumentType type) => type switch
        {
            AttachmentDocumentType.GuaranteeImage => "صورة ضمان",
            AttachmentDocumentType.BankResponse => "رد البنك",
            AttachmentDocumentType.SupportingDocument => "مستند داعم",
            AttachmentDocumentType.RequestLetter => "خطاب الطلب",
            AttachmentDocumentType.Other => "أخرى",
            _ => "مستند داعم"
        };

        public static AttachmentDocumentType Parse(string? value, AttachmentDocumentType fallback = AttachmentDocumentType.SupportingDocument)
        {
            return Enum.TryParse(value, ignoreCase: true, out AttachmentDocumentType parsed)
                ? parsed
                : fallback;
        }
    }
}
