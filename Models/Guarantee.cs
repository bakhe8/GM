using System;
using System.Collections.Generic;
using System.Globalization;

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
        // Legacy-only value kept for backwards compatibility with older datasets.
        Closed
    }

    public static class GuaranteeLifecycleStatusDisplay
    {
        public static string GetLabel(GuaranteeLifecycleStatus status) => status switch
        {
            GuaranteeLifecycleStatus.Active => "نشط",
            GuaranteeLifecycleStatus.Expired => "منتهي الصلاحية",
            GuaranteeLifecycleStatus.Released => "مفرج",
            GuaranteeLifecycleStatus.Liquidated => "مسيّل",
            GuaranteeLifecycleStatus.Replaced => "مستبدل",
            GuaranteeLifecycleStatus.Closed => "مغلق (قديم)",
            _ => "غير معروف"
        };

        public static bool IsLegacyOnly(GuaranteeLifecycleStatus status) => status == GuaranteeLifecycleStatus.Closed;
    }

    public static class GuaranteeVersionDisplay
    {
        public static string GetLabel(int versionNumber)
        {
            if (versionNumber <= 0)
            {
                return "---";
            }

            if (versionNumber <= 10)
            {
                return GetUnitOrdinal(versionNumber);
            }

            if (versionNumber < 20)
            {
                return versionNumber switch
                {
                    11 => "الحادي عشر",
                    12 => "الثاني عشر",
                    _ => $"{GetUnitOrdinal(versionNumber % 10)} عشر"
                };
            }

            if (versionNumber < 100)
            {
                int ones = versionNumber % 10;
                string tens = GetTensOrdinal(versionNumber / 10);
                return ones == 0
                    ? tens
                    : $"{GetCompoundUnitOrdinal(ones)} و{tens}";
            }

            return $"رقم {versionNumber.ToString("N0", CultureInfo.InvariantCulture)}";
        }

        private static string GetUnitOrdinal(int value) => value switch
        {
            1 => "الأول",
            2 => "الثاني",
            3 => "الثالث",
            4 => "الرابع",
            5 => "الخامس",
            6 => "السادس",
            7 => "السابع",
            8 => "الثامن",
            9 => "التاسع",
            10 => "العاشر",
            _ => value.ToString("N0", CultureInfo.InvariantCulture)
        };

        private static string GetCompoundUnitOrdinal(int value) => value switch
        {
            1 => "الحادي",
            2 => "الثاني",
            3 => "الثالث",
            4 => "الرابع",
            5 => "الخامس",
            6 => "السادس",
            7 => "السابع",
            8 => "الثامن",
            9 => "التاسع",
            _ => value.ToString("N0", CultureInfo.InvariantCulture)
        };

        private static string GetTensOrdinal(int value) => value switch
        {
            2 => "العشرون",
            3 => "الثلاثون",
            4 => "الأربعون",
            5 => "الخمسون",
            6 => "الستون",
            7 => "السبعون",
            8 => "الثمانون",
            9 => "التسعون",
            _ => value.ToString("N0", CultureInfo.InvariantCulture)
        };
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
        public bool NeedsExpiryFollowUp =>
            IsExpired &&
            (LifecycleStatus == GuaranteeLifecycleStatus.Active || LifecycleStatus == GuaranteeLifecycleStatus.Expired);
        public string StatusLabel => IsExpired ? "منتهي" : (IsExpiringSoon ? "قريب الانتهاء" : "نشط");
        public string LifecycleStatusLabel => GuaranteeLifecycleStatusDisplay.GetLabel(LifecycleStatus);
        public string VersionLabel => GuaranteeVersionDisplay.GetLabel(VersionNumber);
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
