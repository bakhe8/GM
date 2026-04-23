using System;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    internal static class WorkflowRequestDataAccess
    {
        public const string SelectColumns = @"
            Id,
            RootId,
            SequenceNumber,
            BaseVersionId,
            ResultVersionId,
            RequestType,
            RequestStatus,
            RequestDate,
            CreatedAt,
            UpdatedAt,
            ResponseRecordedAt,
            RequestedDataJson,
            LetterOriginalFileName,
            LetterSavedFileName,
            ResponseOriginalFileName,
            ResponseSavedFileName,
            ResponseNotes,
            Notes,
            CreatedBy";

        public static WorkflowRequest MapWorkflowRequest(SqliteDataReader reader)
        {
            return new WorkflowRequest
            {
                Id = reader.GetInt32(0),
                RootGuaranteeId = reader.GetInt32(1),
                SequenceNumber = reader.GetInt32(2),
                BaseVersionId = reader.GetInt32(3),
                ResultVersionId = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4),
                Type = Enum.TryParse(reader.GetString(5), true, out RequestType requestType) ? requestType : RequestType.Extension,
                Status = Enum.TryParse(reader.GetString(6), true, out RequestStatus requestStatus) ? requestStatus : RequestStatus.Pending,
                RequestDate = PersistedDateTime.Parse(reader.GetString(7)),
                CreatedAt = PersistedDateTime.Parse(reader.GetString(8)),
                UpdatedAt = PersistedDateTime.Parse(reader.GetString(9)),
                ResponseRecordedAt = reader.IsDBNull(10) ? (DateTime?)null : PersistedDateTime.Parse(reader.GetString(10)),
                RequestedDataJson = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                LetterOriginalFileName = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                LetterSavedFileName = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                ResponseOriginalFileName = reader.IsDBNull(14) ? string.Empty : reader.GetString(14),
                ResponseSavedFileName = reader.IsDBNull(15) ? string.Empty : reader.GetString(15),
                ResponseNotes = reader.IsDBNull(16) ? string.Empty : reader.GetString(16),
                Notes = reader.IsDBNull(17) ? string.Empty : reader.GetString(17),
                CreatedBy = reader.IsDBNull(18) ? string.Empty : reader.GetString(18)
            };
        }
    }
}
