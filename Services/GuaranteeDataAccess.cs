using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    internal static class GuaranteeDataAccess
    {
        public const string NormalizedGuaranteeNoSqlExpression = "LOWER(TRIM(GuaranteeNo))";

        public const string SelectColumns = @"
            Id,
            Supplier,
            Bank,
            GuaranteeNo,
            Amount,
            ExpiryDate,
            GuaranteeType,
            Beneficiary,
            Notes,
            CreatedAt,
            RootId,
            VersionNumber,
            IsCurrent,
            ReferenceType,
            ReferenceNumber,
            LifecycleStatus,
            ReplacesRootId,
            ReplacedByRootId";

        public static void NormalizeGuaranteeRoots(SqliteConnection connection, SqliteTransaction? transaction = null)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "UPDATE Guarantees SET RootId = Id WHERE RootId IS NULL";
            cmd.ExecuteNonQuery();
        }

        public static string NormalizeGuaranteeNo(string? guaranteeNo)
        {
            return guaranteeNo?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        public static bool GuaranteeNumbersEqual(string? left, string? right)
        {
            return string.Equals(
                NormalizeGuaranteeNo(left),
                NormalizeGuaranteeNo(right),
                StringComparison.Ordinal);
        }

        public static List<AttachmentRecord> GetAttachmentsForGuarantee(int guaranteeId, SqliteConnection connection, SqliteTransaction? transaction = null)
        {
            var attachments = new List<AttachmentRecord>();
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "SELECT Id, GuaranteeId, OriginalFileName, SavedFileName, FileExtension, UploadedAt, DocumentType, TimelineEventKey FROM Attachments WHERE GuaranteeId = $gid";
            cmd.Parameters.AddWithValue("$gid", guaranteeId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                attachments.Add(MapAttachment(reader));
            }

            return attachments;
        }

        public static AttachmentRecord MapAttachment(SqliteDataReader reader)
        {
            return new AttachmentRecord
            {
                Id = reader.GetInt32(0),
                GuaranteeId = reader.GetInt32(1),
                OriginalFileName = reader.GetString(2),
                SavedFileName = reader.GetString(3),
                FileExtension = reader.GetString(4),
                UploadedAt = PersistedDateTime.Parse(reader.GetString(5)),
                DocumentType = reader.IsDBNull(6)
                    ? AttachmentDocumentType.SupportingDocument
                    : AttachmentDocumentTypeText.Parse(reader.GetString(6)),
                TimelineEventKey = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
            };
        }

        public static GuaranteeLifecycleStatus ParseLifecycleStatus(string? value)
        {
            return Enum.TryParse(value, true, out GuaranteeLifecycleStatus status)
                ? status
                : GuaranteeLifecycleStatus.Active;
        }

        public static GuaranteeReferenceType ParseReferenceType(string? value)
        {
            return Enum.TryParse(value, true, out GuaranteeReferenceType referenceType)
                ? referenceType
                : GuaranteeReferenceType.None;
        }

        public static (GuaranteeReferenceType ReferenceType, string ReferenceNumber) NormalizeReference(
            GuaranteeReferenceType referenceType,
            string? referenceNumber)
        {
            string normalizedReferenceNumber = referenceNumber?.Trim() ?? string.Empty;

            if (referenceType == GuaranteeReferenceType.None || string.IsNullOrWhiteSpace(normalizedReferenceNumber))
            {
                return (GuaranteeReferenceType.None, string.Empty);
            }

            return (referenceType, normalizedReferenceNumber);
        }

        public static Guarantee MapGuarantee(SqliteDataReader reader)
        {
            GuaranteeReferenceType rawReferenceType = reader.IsDBNull(13) ? GuaranteeReferenceType.None : ParseReferenceType(reader.GetString(13));
            string rawReferenceNumber = reader.IsDBNull(14) ? string.Empty : reader.GetString(14);
            (GuaranteeReferenceType referenceType, string referenceNumber) = NormalizeReference(rawReferenceType, rawReferenceNumber);

            return new Guarantee
            {
                Id = reader.GetInt32(0),
                Supplier = reader.GetString(1),
                Bank = reader.GetString(2),
                GuaranteeNo = reader.GetString(3),
                Amount = reader.GetDecimal(4),
                ExpiryDate = PersistedDateTime.Parse(reader.GetString(5)),
                GuaranteeType = reader.GetString(6),
                Beneficiary = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                Notes = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                CreatedAt = PersistedDateTime.Parse(reader.GetString(9)),
                RootId = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10),
                VersionNumber = reader.GetInt32(11),
                IsCurrent = reader.GetInt32(12) == 1,
                ReferenceType = referenceType,
                ReferenceNumber = referenceNumber,
                LifecycleStatus = reader.IsDBNull(15) ? GuaranteeLifecycleStatus.Active : ParseLifecycleStatus(reader.GetString(15)),
                ReplacesRootId = reader.IsDBNull(16) ? (int?)null : reader.GetInt32(16),
                ReplacedByRootId = reader.IsDBNull(17) ? (int?)null : reader.GetInt32(17)
            };
        }
    }
}
