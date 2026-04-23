using System;
using System.Collections.Generic;

namespace GuaranteeManager.Models
{
    public enum GuaranteeReferenceType
    {
        None,
        Contract,
        PurchaseOrder
    }

    public enum GuaranteeLifecycleStatus
    {
        Active,
        Expired,
        Released,
        Liquidated,
        Replaced,
        Closed
    }

    public class Guarantee
    {
        public int Id { get; set; }
        public string Supplier { get; set; } = string.Empty;
        public string Bank { get; set; } = string.Empty;
        public string GuaranteeNo { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string GuaranteeType { get; set; } = string.Empty;
        public string Beneficiary { get; set; } = string.Empty;
        public GuaranteeReferenceType ReferenceType { get; set; } = GuaranteeReferenceType.None;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public GuaranteeLifecycleStatus LifecycleStatus { get; set; } = GuaranteeLifecycleStatus.Active;
        public int? ReplacesRootId { get; set; }
        public int? ReplacedByRootId { get; set; }

        // Versioning Support
        public int? RootId { get; set; }
        public int VersionNumber { get; set; } = 1;
        public bool IsCurrent { get; set; } = true;

        // Multi-Attachment Support
        public List<AttachmentRecord> Attachments { get; set; } = new List<AttachmentRecord>();

        public bool IsExpired => ExpiryDate.Date < DateTime.Today;
        public bool IsExpiringSoon => !IsExpired && ExpiryDate.Date <= DateTime.Today.AddDays(30);
        public string StatusLabel => IsExpired ? "منتهي" : (IsExpiringSoon ? "قريب الانتهاء" : "نشط");
        public string LifecycleStatusLabel => LifecycleStatus switch
        {
            GuaranteeLifecycleStatus.Active => "نشط",
            GuaranteeLifecycleStatus.Expired => "منتهي الصلاحية",
            GuaranteeLifecycleStatus.Released => "مفرج",
            GuaranteeLifecycleStatus.Liquidated => "مسيّل",
            GuaranteeLifecycleStatus.Replaced => "مستبدل",
            GuaranteeLifecycleStatus.Closed => "مغلق",
            _ => "غير معروف"
        };
        public string VersionLabel => $"v{VersionNumber}";
        public int AttachmentCount => Attachments?.Count ?? 0;
        public bool HasReference => ReferenceType != GuaranteeReferenceType.None && !string.IsNullOrWhiteSpace(ReferenceNumber);
        public bool IsContractReference => ReferenceType == GuaranteeReferenceType.Contract && !string.IsNullOrWhiteSpace(ReferenceNumber);
        public bool IsPurchaseOrderReference => ReferenceType == GuaranteeReferenceType.PurchaseOrder && !string.IsNullOrWhiteSpace(ReferenceNumber);
        public string ReferenceTypeLabel => ReferenceType switch
        {
            GuaranteeReferenceType.Contract => "عقد",
            GuaranteeReferenceType.PurchaseOrder => "أمر شراء",
            _ => "بدون مرجع"
        };

        public string WorkflowDisplayLabel => $"{GuaranteeNo} - {Supplier} - {ExpiryDate:yyyy-MM-dd}";
    }
}
