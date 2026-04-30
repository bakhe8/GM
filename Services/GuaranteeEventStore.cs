using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    internal static class GuaranteeEventStore
    {
        public static void EnsureSchema(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS GuaranteeEvents (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RootId INTEGER NOT NULL,
                    GuaranteeId INTEGER,
                    WorkflowRequestId INTEGER,
                    AttachmentId INTEGER,
                    EventKey TEXT NOT NULL UNIQUE,
                    EventType TEXT NOT NULL,
                    OccurredAt TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL DEFAULT 100,
                    Title TEXT NOT NULL,
                    Details TEXT,
                    Status TEXT,
                    ToneKey TEXT NOT NULL DEFAULT 'Info',
                    CreatedAt TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_guarantee_events_root_time ON GuaranteeEvents(RootId, OccurredAt, SortOrder, Id);
                CREATE INDEX IF NOT EXISTS idx_guarantee_events_guarantee ON GuaranteeEvents(GuaranteeId);
                CREATE INDEX IF NOT EXISTS idx_guarantee_events_request ON GuaranteeEvents(WorkflowRequestId);
                CREATE INDEX IF NOT EXISTS idx_guarantee_events_attachment ON GuaranteeEvents(AttachmentId);
                CREATE TRIGGER IF NOT EXISTS trg_guarantee_events_no_update
                BEFORE UPDATE ON GuaranteeEvents
                BEGIN
                    SELECT RAISE(ABORT, 'GuaranteeEvents is append-only.');
                END;
                CREATE TRIGGER IF NOT EXISTS trg_guarantee_events_no_delete
                BEFORE DELETE ON GuaranteeEvents
                BEGIN
                    SELECT RAISE(ABORT, 'GuaranteeEvents is append-only.');
                END;
            ";
            command.ExecuteNonQuery();
        }

        public static List<GuaranteeTimelineEvent> GetEventsForGuarantee(
            SqliteConnection connection,
            int guaranteeId)
        {
            EnsureSchema(connection);
            BackfillMissingEvents(connection);

            int? rootId = ResolveRootId(connection, guaranteeId);
            if (!rootId.HasValue)
            {
                return new List<GuaranteeTimelineEvent>();
            }

            var events = new List<GuaranteeTimelineEvent>();
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, RootId, GuaranteeId, WorkflowRequestId, AttachmentId, EventKey, EventType,
                       OccurredAt, SortOrder, Title, Details, Status, ToneKey
                FROM GuaranteeEvents
                WHERE RootId = $rootId
                ORDER BY OccurredAt ASC, SortOrder ASC, Id ASC";
            command.Parameters.AddWithValue("$rootId", rootId.Value);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                events.Add(MapEvent(reader));
            }

            return events;
        }

        public static void BackfillMissingEvents(SqliteConnection connection)
        {
            EnsureSchema(connection);
            BackfillGuaranteeVersionEvents(connection);
            BackfillWorkflowRequestCreatedEvents(connection);
            BackfillWorkflowResponseEvents(connection);
            BackfillWorkflowResponseDocumentEvents(connection);
            BackfillAttachmentEvents(connection);
        }

        private static void BackfillGuaranteeVersionEvents(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = $"SELECT {GuaranteeDataAccess.SelectColumns} FROM Guarantees ORDER BY COALESCE(RootId, Id), VersionNumber, CreatedAt, Id";

            var versions = new List<Guarantee>();
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    versions.Add(GuaranteeDataAccess.MapGuarantee(reader));
                }
            }

            foreach (Guarantee version in versions)
            {
                int rootId = version.RootId ?? version.Id;
                bool isFirstVersion = version.VersionNumber <= 1;
                InsertEventIfMissing(
                    connection,
                    rootId,
                    version.Id,
                    workflowRequestId: null,
                    attachmentId: null,
                    eventKey: isFirstVersion ? $"guarantee-created:{version.Id}" : $"guarantee-version:{version.Id}",
                    eventType: isFirstVersion ? "GuaranteeCreated" : "GuaranteeVersionCreated",
                    occurredAt: version.CreatedAt,
                    sortOrder: isFirstVersion ? 10 : 50,
                    title: isFirstVersion ? "إنشاء الضمان" : $"إصدار جديد {version.VersionLabel}",
                    details: isFirstVersion
                        ? $"تم إنشاء الضمان بقيمة {version.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال وانتهاء {version.ExpiryDate:yyyy/MM/dd}."
                        : $"تم حفظ شروط هذا الإصدار: المبلغ {version.Amount.ToString("N0", CultureInfo.InvariantCulture)} ريال | الانتهاء {version.ExpiryDate:yyyy/MM/dd}.",
                    status: isFirstVersion ? "مكتمل" : "موثق",
                    toneKey: isFirstVersion ? "Success" : "Info");
            }
        }

        private static void BackfillWorkflowRequestCreatedEvents(SqliteConnection connection)
        {
            foreach (WorkflowRequest request in LoadWorkflowRequests(connection))
            {
                string detail = $"القيمة المطلوبة: {request.RequestedValueLabel}";
                if (!string.IsNullOrWhiteSpace(request.Notes))
                {
                    detail += $" | {request.Notes.Trim()}";
                }

                InsertEventIfMissing(
                    connection,
                    request.RootGuaranteeId,
                    request.BaseVersionId,
                    request.Id,
                    attachmentId: null,
                    eventKey: $"workflow-request-created:{request.Id}",
                    eventType: "WorkflowRequestCreated",
                    occurredAt: request.RequestDate,
                    sortOrder: 20,
                    title: request.TypeLabel,
                    details: detail,
                    status: request.Status == RequestStatus.Pending ? request.StatusLabel : "مسجل",
                    toneKey: request.Status == RequestStatus.Pending ? "Warning" : "Info");
            }
        }

        private static void BackfillWorkflowResponseEvents(SqliteConnection connection)
        {
            foreach (WorkflowRequest request in LoadWorkflowRequests(connection).Where(request => request.ResponseRecordedAt.HasValue))
            {
                InsertEventIfMissing(
                    connection,
                    request.RootGuaranteeId,
                    request.ResultVersionId ?? request.BaseVersionId,
                    request.Id,
                    attachmentId: null,
                    eventKey: $"workflow-response:{request.Id}",
                    eventType: "WorkflowResponseRecorded",
                    occurredAt: request.ResponseRecordedAt!.Value,
                    sortOrder: 30,
                    title: $"تسجيل رد {request.TypeLabel}",
                    details: BuildWorkflowResponseDetails(request),
                    status: request.StatusLabel,
                    toneKey: GetRequestToneKey(request.Status));
            }
        }

        private static void BackfillWorkflowResponseDocumentEvents(SqliteConnection connection)
        {
            foreach (WorkflowRequest request in LoadWorkflowRequests(connection)
                         .Where(request => request.ResponseRecordedAt.HasValue && request.HasResponseDocument))
            {
                if (ResponseEventCapturedDocument(connection, request.Id))
                {
                    continue;
                }

                string documentName = string.IsNullOrWhiteSpace(request.ResponseOriginalFileName)
                    ? "تم إلحاق مستند رد البنك."
                    : request.ResponseOriginalFileName.Trim();
                InsertEventIfMissing(
                    connection,
                    request.RootGuaranteeId,
                    request.ResultVersionId ?? request.BaseVersionId,
                    request.Id,
                    attachmentId: null,
                    eventKey: $"workflow-response-document:{request.Id}",
                    eventType: "WorkflowResponseDocumentAttached",
                    occurredAt: GetResponseDocumentAttachmentTimestamp(request),
                    sortOrder: 35,
                    title: "إلحاق مستند رد البنك",
                    details: documentName,
                    status: "مرفق",
                    toneKey: "Info");
            }
        }

        private static void BackfillAttachmentEvents(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT a.Id, a.GuaranteeId, COALESCE(g.RootId, g.Id) AS RootId,
                       a.OriginalFileName, a.SavedFileName, a.FileExtension, a.UploadedAt, a.DocumentType,
                       a.TimelineEventKey
                FROM Attachments a
                INNER JOIN Guarantees g ON g.Id = a.GuaranteeId
                ORDER BY RootId, LOWER(IFNULL(a.SavedFileName, '')), a.UploadedAt, a.Id";

            var selected = new Dictionary<string, AttachmentEventRow>(StringComparer.OrdinalIgnoreCase);
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    int rootId = reader.GetInt32(2);
                    string savedFileName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
                    string timelineEventKey = reader.IsDBNull(8) ? string.Empty : reader.GetString(8);
                    if (!string.IsNullOrWhiteSpace(timelineEventKey))
                    {
                        continue;
                    }

                    string key = string.IsNullOrWhiteSpace(savedFileName)
                        ? $"attachment-id:{reader.GetInt32(0).ToString(CultureInfo.InvariantCulture)}"
                        : $"attachment-file:{rootId.ToString(CultureInfo.InvariantCulture)}:{savedFileName}";
                    if (selected.ContainsKey(key))
                    {
                        continue;
                    }

                    selected[key] = new AttachmentEventRow(
                        reader.GetInt32(0),
                        reader.GetInt32(1),
                        rootId,
                        reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        savedFileName,
                        PersistedDateTime.Parse(reader.GetString(6)),
                        AttachmentDocumentTypeText.Parse(reader.IsDBNull(7) ? null : reader.GetString(7)));
                }
            }

            foreach (AttachmentEventRow attachment in selected.Values)
            {
                string documentTypeLabel = AttachmentDocumentTypeText.Label(attachment.DocumentType);
                string name = string.IsNullOrWhiteSpace(attachment.OriginalFileName)
                    ? "مرفق رسمي"
                    : attachment.OriginalFileName.Trim();
                string stableKey = string.IsNullOrWhiteSpace(attachment.SavedFileName)
                    ? $"attachment-added:{attachment.AttachmentId}"
                    : $"attachment-added:{attachment.RootId}:{attachment.SavedFileName}";
                InsertEventIfMissing(
                    connection,
                    attachment.RootId,
                    attachment.GuaranteeId,
                    workflowRequestId: null,
                    attachment.AttachmentId,
                    stableKey,
                    "AttachmentAdded",
                    attachment.UploadedAt,
                    40,
                    $"إضافة مرفق {documentTypeLabel}",
                    name,
                    "مضاف",
                    "Info");
            }
        }

        private static List<WorkflowRequest> LoadWorkflowRequests(SqliteConnection connection)
        {
            var requests = new List<WorkflowRequest>();
            var command = connection.CreateCommand();
            command.CommandText = $"SELECT {WorkflowRequestDataAccess.SelectColumns} FROM WorkflowRequests ORDER BY RootId, SequenceNumber, RequestDate, Id";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                requests.Add(WorkflowRequestDataAccess.MapWorkflowRequest(reader));
            }

            return requests;
        }

        private static void InsertEventIfMissing(
            SqliteConnection connection,
            int rootId,
            int? guaranteeId,
            int? workflowRequestId,
            int? attachmentId,
            string eventKey,
            string eventType,
            DateTime occurredAt,
            int sortOrder,
            string title,
            string details,
            string status,
            string toneKey)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO GuaranteeEvents (
                    RootId, GuaranteeId, WorkflowRequestId, AttachmentId, EventKey, EventType,
                    OccurredAt, SortOrder, Title, Details, Status, ToneKey, CreatedAt
                )
                VALUES (
                    $rootId, $guaranteeId, $workflowRequestId, $attachmentId, $eventKey, $eventType,
                    $occurredAt, $sortOrder, $title, $details, $status, $toneKey, $createdAt
                )";
            command.Parameters.AddWithValue("$rootId", rootId);
            command.Parameters.AddWithValue("$guaranteeId", (object?)guaranteeId ?? DBNull.Value);
            command.Parameters.AddWithValue("$workflowRequestId", (object?)workflowRequestId ?? DBNull.Value);
            command.Parameters.AddWithValue("$attachmentId", (object?)attachmentId ?? DBNull.Value);
            command.Parameters.AddWithValue("$eventKey", eventKey);
            command.Parameters.AddWithValue("$eventType", eventType);
            command.Parameters.AddWithValue("$occurredAt", PersistedDateTime.FormatDateTime(occurredAt));
            command.Parameters.AddWithValue("$sortOrder", sortOrder);
            command.Parameters.AddWithValue("$title", title);
            command.Parameters.AddWithValue("$details", details);
            command.Parameters.AddWithValue("$status", status);
            command.Parameters.AddWithValue("$toneKey", toneKey);
            command.Parameters.AddWithValue("$createdAt", PersistedDateTime.FormatDateTime(DateTime.Now));
            command.ExecuteNonQuery();
        }

        private static int? ResolveRootId(SqliteConnection connection, int guaranteeId)
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT COALESCE(RootId, Id) FROM Guarantees WHERE Id = $id LIMIT 1";
            command.Parameters.AddWithValue("$id", guaranteeId);
            object? value = command.ExecuteScalar();
            return value == null || value == DBNull.Value ? null : Convert.ToInt32(value);
        }

        private static GuaranteeTimelineEvent MapEvent(SqliteDataReader reader)
        {
            return new GuaranteeTimelineEvent
            {
                Id = reader.GetInt32(0),
                RootId = reader.GetInt32(1),
                GuaranteeId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                WorkflowRequestId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                AttachmentId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                EventKey = reader.GetString(5),
                EventType = reader.GetString(6),
                OccurredAt = PersistedDateTime.Parse(reader.GetString(7)),
                SortOrder = reader.GetInt32(8),
                Title = reader.GetString(9),
                Details = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                Status = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                ToneKey = reader.GetString(12)
            };
        }

        private static string BuildWorkflowResponseDetails(WorkflowRequest request)
        {
            string detail = WorkflowRequestDisplayText.BuildDetail(request);
            string effect = request.Status == RequestStatus.Executed
                ? request.Type switch
                {
                    RequestType.Extension when request.ResultVersionId.HasValue => "الإصدار الناتج مسجل في السجل الزمني",
                    RequestType.Reduction when request.ResultVersionId.HasValue => "الإصدار الناتج مسجل في السجل الزمني",
                    RequestType.Verification when request.ResultVersionId.HasValue => "اعتماد مستند رسمي على سجل الضمان",
                    RequestType.Release => "تم إنهاء دورة حياة الضمان بالإفراج",
                    RequestType.Liquidation => "تم إنهاء دورة حياة الضمان بالتسييل",
                    RequestType.Replacement => string.IsNullOrWhiteSpace(request.ReplacementGuaranteeNo)
                        ? "تم إنشاء ضمان بديل"
                        : $"الضمان البديل: {request.ReplacementGuaranteeNo}",
                    RequestType.Annulment => "مسار قديم ملغى",
                    _ => string.Empty
                }
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(effect))
            {
                detail += $" | {effect}";
            }

            if (request.HasResponseDocument)
            {
                detail += " | رد البنك مرفق";
            }

            return detail;
        }

        private static bool ResponseEventCapturedDocument(SqliteConnection connection, int requestId)
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT Details FROM GuaranteeEvents WHERE EventKey = $eventKey LIMIT 1";
            command.Parameters.AddWithValue("$eventKey", $"workflow-response:{requestId.ToString(CultureInfo.InvariantCulture)}");
            object? value = command.ExecuteScalar();
            string details = value == null || value == DBNull.Value
                ? string.Empty
                : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

            return details.Contains("رد البنك مرفق", StringComparison.Ordinal);
        }

        private static DateTime GetResponseDocumentAttachmentTimestamp(WorkflowRequest request)
        {
            if (!request.ResponseRecordedAt.HasValue)
            {
                return request.UpdatedAt;
            }

            return request.UpdatedAt > request.ResponseRecordedAt.Value
                ? request.UpdatedAt
                : request.ResponseRecordedAt.Value;
        }

        private static string GetRequestToneKey(RequestStatus status) => status switch
        {
            RequestStatus.Executed => "Success",
            RequestStatus.Pending => "Warning",
            RequestStatus.Rejected or RequestStatus.Cancelled => "Danger",
            _ => "Info"
        };

        private sealed record AttachmentEventRow(
            int AttachmentId,
            int GuaranteeId,
            int RootId,
            string OriginalFileName,
            string SavedFileName,
            DateTime UploadedAt,
            AttachmentDocumentType DocumentType);
    }
}
