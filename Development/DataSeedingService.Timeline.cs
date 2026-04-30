#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Microsoft.Data.Sqlite;

namespace GuaranteeManager.Development
{
    public partial class DataSeedingService
    {
        private void NormalizeGeneratedTimelines(
            IReadOnlySet<int> existingGuaranteeIds,
            IReadOnlySet<int> existingRequestIds,
            IReadOnlySet<int> existingAttachmentIds)
        {
            using var connection = SqliteConnectionFactory.Open(_connectionString);
            using var transaction = connection.BeginTransaction();

            try
            {
                List<SeedGuaranteeRow> guarantees = LoadSeedGuarantees(connection, transaction, existingGuaranteeIds);
                List<SeedWorkflowRow> requests = LoadSeedWorkflowRequests(connection, transaction, existingRequestIds);
                List<SeedAttachmentRow> attachments = LoadSeedAttachments(connection, transaction, existingAttachmentIds);

                var requestsByRoot = requests
                    .GroupBy(request => request.RootId)
                    .ToDictionary(group => group.Key, group => group.OrderBy(request => request.SequenceNumber).ThenBy(request => request.Id).ToList());
                var attachmentsByGuarantee = attachments
                    .GroupBy(attachment => attachment.GuaranteeId)
                    .ToDictionary(group => group.Key, group => group.OrderBy(attachment => attachment.Id).ToList());

                var forcedVersionTimes = new Dictionary<int, DateTime>();
                int scenarioIndex = 0;
                foreach (var scenario in guarantees
                    .GroupBy(guarantee => guarantee.RootId)
                    .OrderBy(group => group.Min(guarantee => guarantee.Id)))
                {
                    ApplyScenarioTimeline(
                        connection,
                        transaction,
                        scenarioIndex++,
                        scenario.OrderBy(guarantee => guarantee.VersionNumber).ThenBy(guarantee => guarantee.Id).ToList(),
                        requestsByRoot.TryGetValue(scenario.Key, out List<SeedWorkflowRow>? scenarioRequests)
                            ? scenarioRequests
                            : new List<SeedWorkflowRow>(),
                        attachmentsByGuarantee,
                        forcedVersionTimes);
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static void ApplyScenarioTimeline(
            SqliteConnection connection,
            SqliteTransaction transaction,
            int scenarioIndex,
            List<SeedGuaranteeRow> versions,
            List<SeedWorkflowRow> requests,
            IReadOnlyDictionary<int, List<SeedAttachmentRow>> attachmentsByGuarantee,
            Dictionary<int, DateTime> forcedVersionTimes)
        {
            if (versions.Count == 0)
            {
                return;
            }

            DateTime scenarioStart = BuildScenarioStart(versions, scenarioIndex);
            SeedGuaranteeRow firstVersion = versions[0];
            if (forcedVersionTimes.TryGetValue(firstVersion.Id, out DateTime forcedStart))
            {
                scenarioStart = forcedStart;
            }

            var versionTimes = new Dictionary<int, DateTime>
            {
                [firstVersion.Id] = scenarioStart
            };

            DateTime cursor = scenarioStart;
            foreach (SeedWorkflowRow request in requests)
            {
                DateTime anchor = versionTimes.TryGetValue(request.BaseVersionId, out DateTime baseVersionTime)
                    ? baseVersionTime
                    : cursor;

                DateTime requestTime = NextScenarioEvent(
                    Max(anchor, cursor),
                    1 + ((scenarioIndex + request.SequenceNumber) % 4),
                    9 + ((scenarioIndex + request.SequenceNumber) % 3),
                    (scenarioIndex * 7 + request.SequenceNumber * 11) % 50);

                DateTime updatedAt = requestTime.AddMinutes(30 + ((scenarioIndex + request.SequenceNumber) % 5) * 3);
                DateTime? responseTime = null;

                if (request.HasResponse)
                {
                    responseTime = NextScenarioEvent(
                        requestTime,
                        1 + ((scenarioIndex + request.SequenceNumber) % 3),
                        11 + ((scenarioIndex + request.SequenceNumber) % 4),
                        (scenarioIndex * 13 + request.SequenceNumber * 17) % 50);
                    updatedAt = responseTime.Value;

                    if (request.ResultVersionId.HasValue)
                    {
                        DateTime resultVersionTime = responseTime.Value.AddMinutes(20);
                        forcedVersionTimes[request.ResultVersionId.Value] = resultVersionTime;
                        AssignRelatedVersionTime(
                            versions,
                            request,
                            resultVersionTime,
                            versionTimes,
                            forcedVersionTimes);
                    }
                }

                UpdateWorkflowRequestTime(connection, transaction, request.Id, requestTime, updatedAt, responseTime);
                cursor = Max(cursor, responseTime ?? updatedAt);
            }

            foreach (SeedGuaranteeRow version in versions)
            {
                if (!versionTimes.ContainsKey(version.Id))
                {
                    DateTime forcedTime = forcedVersionTimes.TryGetValue(version.Id, out DateTime candidate)
                        ? candidate
                        : NextScenarioEvent(
                            cursor,
                            2 + ((scenarioIndex + version.VersionNumber) % 5),
                            10 + ((scenarioIndex + version.VersionNumber) % 4),
                            (scenarioIndex * 5 + version.VersionNumber * 9) % 50);
                    versionTimes[version.Id] = forcedTime;
                    cursor = Max(cursor, forcedTime);
                }
            }

            var firstAttachmentSeenAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            foreach (SeedGuaranteeRow version in versions)
            {
                DateTime versionTime = versionTimes[version.Id];
                UpdateGuaranteeCreatedAt(connection, transaction, version.Id, versionTime);

                if (!attachmentsByGuarantee.TryGetValue(version.Id, out List<SeedAttachmentRow>? versionAttachments))
                {
                    continue;
                }

                for (int index = 0; index < versionAttachments.Count; index++)
                {
                    SeedAttachmentRow attachment = versionAttachments[index];
                    DateTime uploadTime = ResolveAttachmentTime(
                        attachment,
                        versionTime,
                        index,
                        firstAttachmentSeenAt);
                    UpdateAttachmentUploadedAt(connection, transaction, attachment.Id, uploadTime);
                }
            }
        }

        private static void AssignRelatedVersionTime(
            IReadOnlyList<SeedGuaranteeRow> versions,
            SeedWorkflowRow request,
            DateTime resultVersionTime,
            IDictionary<int, DateTime> versionTimes,
            IDictionary<int, DateTime> forcedVersionTimes)
        {
            SeedGuaranteeRow? resultVersion = versions.FirstOrDefault(version => version.Id == request.ResultVersionId);
            if (resultVersion != null)
            {
                versionTimes[resultVersion.Id] = resultVersionTime;
                return;
            }

            SeedGuaranteeRow? replacedVersion = versions.FirstOrDefault(version => version.ReplacedByRootId == request.ResultVersionId);
            if (replacedVersion != null)
            {
                versionTimes[replacedVersion.Id] = resultVersionTime;
            }
        }

        private static DateTime ResolveAttachmentTime(
            SeedAttachmentRow attachment,
            DateTime versionTime,
            int index,
            IDictionary<string, DateTime> firstAttachmentSeenAt)
        {
            string key = string.IsNullOrWhiteSpace(attachment.SavedFileName)
                ? $"attachment:{attachment.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                : attachment.SavedFileName.Trim();

            if (firstAttachmentSeenAt.TryGetValue(key, out DateTime firstSeenAt))
            {
                return Max(firstSeenAt.AddMinutes(1), versionTime.AddMinutes(10 + index * 3));
            }

            bool looksLikeBankResponse = attachment.OriginalFileName.Contains("رد_البنك", StringComparison.OrdinalIgnoreCase)
                || attachment.SavedFileName.Contains("response", StringComparison.OrdinalIgnoreCase);
            DateTime uploadTime = versionTime.AddMinutes(looksLikeBankResponse ? 35 + index * 4 : 30 + index * 5);
            firstAttachmentSeenAt[key] = uploadTime;
            return uploadTime;
        }

        private static DateTime BuildScenarioStart(IReadOnlyList<SeedGuaranteeRow> versions, int scenarioIndex)
        {
            DateTime rollingStart = DateTime.Today
                .AddDays(-150 + Math.Min(scenarioIndex * 2, 120))
                .Date
                .AddHours(8 + scenarioIndex % 3)
                .AddMinutes((scenarioIndex * 7) % 50);
            DateTime expiryBound = versions.Min(version => version.ExpiryDate).Date
                .AddDays(-45 - scenarioIndex % 18)
                .AddHours(8 + scenarioIndex % 4)
                .AddMinutes((scenarioIndex * 5) % 45);

            DateTime start = rollingStart <= expiryBound ? rollingStart : expiryBound;
            DateTime latestReasonableStart = DateTime.Today.AddDays(-7 - scenarioIndex % 20).Date.AddHours(8 + scenarioIndex % 4);
            return start <= latestReasonableStart ? start : latestReasonableStart;
        }

        private static DateTime NextScenarioEvent(DateTime anchor, int daysAfter, int hour, int minute)
        {
            DateTime candidate = anchor.Date.AddDays(daysAfter).AddHours(hour).AddMinutes(minute);
            return candidate > anchor ? candidate : anchor.AddHours(2);
        }

        private static DateTime Max(DateTime first, DateTime second) => first >= second ? first : second;

        private static List<SeedGuaranteeRow> LoadSeedGuarantees(
            SqliteConnection connection,
            SqliteTransaction transaction,
            IReadOnlySet<int> existingGuaranteeIds)
        {
            var guarantees = new List<SeedGuaranteeRow>();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                SELECT Id, COALESCE(RootId, Id), GuaranteeNo, VersionNumber, IsCurrent, ExpiryDate, LifecycleStatus, ReplacedByRootId
                FROM Guarantees
                ORDER BY COALESCE(RootId, Id), VersionNumber, Id";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                int id = reader.GetInt32(0);
                if (existingGuaranteeIds.Contains(id))
                {
                    continue;
                }

                guarantees.Add(new SeedGuaranteeRow(
                    id,
                    reader.GetInt32(1),
                    reader.GetString(2),
                    reader.GetInt32(3),
                    !reader.IsDBNull(4) && reader.GetInt32(4) == 1,
                    PersistedDateTime.Parse(reader.GetString(5)),
                    ParseEnumOrDefault(reader.GetString(6), GuaranteeLifecycleStatus.Active),
                    reader.IsDBNull(7) ? null : reader.GetInt32(7)));
            }

            return guarantees;
        }

        private static List<SeedWorkflowRow> LoadSeedWorkflowRequests(
            SqliteConnection connection,
            SqliteTransaction transaction,
            IReadOnlySet<int> existingRequestIds)
        {
            var requests = new List<SeedWorkflowRow>();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                SELECT Id, RootId, SequenceNumber, BaseVersionId, ResultVersionId, RequestType, RequestStatus, ResponseRecordedAt
                FROM WorkflowRequests
                ORDER BY RootId, SequenceNumber, Id";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                int id = reader.GetInt32(0);
                if (existingRequestIds.Contains(id))
                {
                    continue;
                }

                requests.Add(new SeedWorkflowRow(
                    id,
                    reader.GetInt32(1),
                    reader.GetInt32(2),
                    reader.GetInt32(3),
                    reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    ParseEnumOrDefault(reader.GetString(5), RequestType.Verification),
                    ParseEnumOrDefault(reader.GetString(6), RequestStatus.Pending),
                    !reader.IsDBNull(7)));
            }

            return requests;
        }

        private static List<SeedAttachmentRow> LoadSeedAttachments(
            SqliteConnection connection,
            SqliteTransaction transaction,
            IReadOnlySet<int> existingAttachmentIds)
        {
            var attachments = new List<SeedAttachmentRow>();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                SELECT Id, GuaranteeId, SavedFileName, OriginalFileName
                FROM Attachments
                ORDER BY GuaranteeId, Id";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                int id = reader.GetInt32(0);
                if (existingAttachmentIds.Contains(id))
                {
                    continue;
                }

                attachments.Add(new SeedAttachmentRow(
                    id,
                    reader.GetInt32(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    reader.IsDBNull(3) ? string.Empty : reader.GetString(3)));
            }

            return attachments;
        }

        private static void UpdateGuaranteeCreatedAt(SqliteConnection connection, SqliteTransaction transaction, int guaranteeId, DateTime createdAt)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE Guarantees SET CreatedAt = $createdAt WHERE Id = $id";
            command.Parameters.AddWithValue("$createdAt", PersistedDateTime.FormatDateTime(createdAt));
            command.Parameters.AddWithValue("$id", guaranteeId);
            command.ExecuteNonQuery();
        }

        private static void UpdateWorkflowRequestTime(
            SqliteConnection connection,
            SqliteTransaction transaction,
            int requestId,
            DateTime requestDate,
            DateTime updatedAt,
            DateTime? responseRecordedAt)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
                UPDATE WorkflowRequests
                SET RequestDate = $requestDate,
                    CreatedAt = $requestDate,
                    UpdatedAt = $updatedAt,
                    ResponseRecordedAt = $responseRecordedAt
                WHERE Id = $id";
            command.Parameters.AddWithValue("$requestDate", PersistedDateTime.FormatDateTime(requestDate));
            command.Parameters.AddWithValue("$updatedAt", PersistedDateTime.FormatDateTime(updatedAt));
            command.Parameters.AddWithValue("$responseRecordedAt", responseRecordedAt.HasValue
                ? PersistedDateTime.FormatDateTime(responseRecordedAt.Value)
                : DBNull.Value);
            command.Parameters.AddWithValue("$id", requestId);
            command.ExecuteNonQuery();
        }

        private static void UpdateAttachmentUploadedAt(SqliteConnection connection, SqliteTransaction transaction, int attachmentId, DateTime uploadedAt)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE Attachments SET UploadedAt = $uploadedAt WHERE Id = $id";
            command.Parameters.AddWithValue("$uploadedAt", PersistedDateTime.FormatDateTime(uploadedAt));
            command.Parameters.AddWithValue("$id", attachmentId);
            command.ExecuteNonQuery();
        }

        private static TEnum ParseEnumOrDefault<TEnum>(string value, TEnum defaultValue)
            where TEnum : struct
        {
            return Enum.TryParse(value, ignoreCase: true, out TEnum parsed)
                ? parsed
                : defaultValue;
        }

        private sealed record SeedGuaranteeRow(
            int Id,
            int RootId,
            string GuaranteeNo,
            int VersionNumber,
            bool IsCurrent,
            DateTime ExpiryDate,
            GuaranteeLifecycleStatus LifecycleStatus,
            int? ReplacedByRootId);

        private sealed record SeedWorkflowRow(
            int Id,
            int RootId,
            int SequenceNumber,
            int BaseVersionId,
            int? ResultVersionId,
            RequestType Type,
            RequestStatus Status,
            bool HasResponse);

        private sealed record SeedAttachmentRow(
            int Id,
            int GuaranteeId,
            string SavedFileName,
            string OriginalFileName);
    }
}
#endif
