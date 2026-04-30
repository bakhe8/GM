using System;
using System.IO;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Models
{
    public class AttachmentRecord
    {
        public int Id { get; set; }
        public int GuaranteeId { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string SavedFileName { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public AttachmentDocumentType DocumentType { get; set; } = AttachmentDocumentType.SupportingDocument;
        public string TimelineEventKey { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.Now;
        public string DocumentTypeLabel => AttachmentDocumentTypeText.Label(DocumentType);

        /// <summary>
        /// Computed property to rebuild the full path at runtime.
        /// Requirement: Do NOT persist absolute paths in the database.
        /// </summary>
        public string FilePath => Path.Combine(AppPaths.AttachmentsFolder, SavedFileName);

        public bool Exists => File.Exists(FilePath);
    }
}
