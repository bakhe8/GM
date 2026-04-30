using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class GuaranteeWorkspaceDataServiceTests
    {
        [Fact]
        public void BuildSelectionArtifacts_ForCurrentGuarantee_BuildsFullChronologicalTimeline()
        {
            DateTime start = new(2026, 4, 28, 8, 0, 0);
            var current = CreateGuarantee(1, null, 1, true, start, 1_500_000m);
            current.LifecycleStatus = GuaranteeLifecycleStatus.Released;
            current.Attachments.Add(new AttachmentRecord
            {
                Id = 100,
                GuaranteeId = current.Id,
                OriginalFileName = "release-confirmation.pdf",
                SavedFileName = "release-confirmation.pdf",
                FileExtension = ".pdf",
                UploadedAt = start.AddHours(4),
                DocumentType = AttachmentDocumentType.BankResponse
            });
            var request = new WorkflowRequest
            {
                Id = 10,
                RootGuaranteeId = 1,
                SequenceNumber = 1,
                BaseVersionId = current.Id,
                Type = RequestType.Release,
                Status = RequestStatus.Executed,
                RequestDate = start.AddHours(1),
                ResponseRecordedAt = start.AddHours(2),
                ResponseSavedFileName = "bank-response.pdf",
                ResponseNotes = "تم تأكيد رد البنك."
            };

            var service = new GuaranteeWorkspaceDataService(
                new TimelineDatabaseStub(new[] { current }, new[] { request }),
                new ContextActionService());
            GuaranteeRow row = GuaranteeRow.FromGuarantee(current, new[] { request });

            GuaranteeSelectionArtifacts artifacts = service.BuildSelectionArtifacts(row);

            Assert.Equal(4, artifacts.Timeline.Count);
            Assert.Equal(
                new[]
                {
                    "إنشاء الضمان",
                    "طلب إفراج",
                    "تسجيل رد طلب إفراج",
                    "إضافة مرفق رد البنك"
                },
                artifacts.Timeline.Select(item => item.Title));
            Assert.DoesNotContain("الإصدار الناتج", artifacts.Timeline[2].Detail);
            Assert.Contains("تم إنهاء دورة حياة الضمان بالإفراج", artifacts.Timeline[2].Detail);
            Assert.Equal(TimelineEvidenceActionKind.ResponseDocument, artifacts.Timeline[2].EvidenceActionKind);
            Assert.Equal("فتح رد البنك", artifacts.Timeline[2].EvidenceActionLabel);
            Assert.Equal(TimelineEvidenceActionKind.Attachment, artifacts.Timeline[3].EvidenceActionKind);
            Assert.Equal("فتح المرفق", artifacts.Timeline[3].EvidenceActionLabel);
        }

        [Fact]
        public void BuildSelectionArtifacts_FromStoredTimeline_WiresEvidenceActionsToEvents()
        {
            DateTime start = new(2026, 4, 28, 8, 0, 0);
            var current = CreateGuarantee(1, null, 1, true, start, 1_500_000m);
            current.Attachments.Add(new AttachmentRecord
            {
                Id = 100,
                GuaranteeId = current.Id,
                OriginalFileName = "guarantee-image.pdf",
                SavedFileName = "guarantee-image.pdf",
                FileExtension = ".pdf",
                UploadedAt = start.AddHours(4),
                DocumentType = AttachmentDocumentType.GuaranteeImage
            });
            current.Attachments.Add(new AttachmentRecord
            {
                Id = 101,
                GuaranteeId = current.Id,
                OriginalFileName = "creation-evidence.pdf",
                SavedFileName = "creation-evidence.pdf",
                FileExtension = ".pdf",
                UploadedAt = start.AddHours(5),
                DocumentType = AttachmentDocumentType.SupportingDocument,
                TimelineEventKey = "guarantee-created:1"
            });
            var request = new WorkflowRequest
            {
                Id = 12,
                RootGuaranteeId = 1,
                SequenceNumber = 1,
                BaseVersionId = current.Id,
                Type = RequestType.Release,
                Status = RequestStatus.Executed,
                RequestDate = start.AddHours(1),
                ResponseRecordedAt = start.AddHours(2),
                LetterSavedFileName = "release-letter.pdf",
                ResponseNotes = "تم الرد دون مستند محفوظ."
            };
            var events = new[]
            {
                CreateEvent(1, "guarantee-created:1", "GuaranteeCreated", current.Id, null, null, start, 10, "إنشاء الضمان", "تم إنشاء الضمان.", "مكتمل", "Success"),
                CreateEvent(1, "workflow-request-created:12", "WorkflowRequestCreated", current.Id, request.Id, null, start.AddHours(1), 20, "طلب إفراج", "القيمة المطلوبة: إفراج", "مسجل", "Info"),
                CreateEvent(1, "workflow-response:12", "WorkflowResponseRecorded", current.Id, request.Id, null, start.AddHours(2), 30, "تسجيل رد طلب إفراج", "تم الرد دون مستند محفوظ.", "منفذ", "Success"),
                CreateEvent(1, "attachment-added:1:guarantee-image.pdf", "AttachmentAdded", current.Id, null, 100, start.AddHours(4), 40, "إضافة مرفق صورة ضمان", "guarantee-image.pdf", "مضاف", "Info")
            };

            var service = new GuaranteeWorkspaceDataService(
                new TimelineDatabaseStub(new[] { current }, new[] { request }, events),
                new ContextActionService());
            GuaranteeRow row = GuaranteeRow.FromGuarantee(current, new[] { request });

            GuaranteeSelectionArtifacts artifacts = service.BuildSelectionArtifacts(row);

            TimelineItem requestEvent = Assert.Single(artifacts.Timeline, item => item.Title == "طلب إفراج");
            Assert.Equal(TimelineEvidenceActionKind.RequestLetter, requestEvent.EvidenceActionKind);
            Assert.Equal("فتح خطاب الطلب", requestEvent.EvidenceActionLabel);

            TimelineItem responseEvent = Assert.Single(artifacts.Timeline, item => item.Title == "تسجيل رد طلب إفراج");
            Assert.Equal(TimelineEvidenceActionKind.ResponseDocument, responseEvent.EvidenceActionKind);
            Assert.Equal("إرفاق", responseEvent.EvidenceActionLabel);

            TimelineItem guaranteeEvent = Assert.Single(artifacts.Timeline, item => item.Title == "إنشاء الضمان");
            Assert.Equal(TimelineEvidenceActionKind.Attachment, guaranteeEvent.EvidenceActionKind);
            Assert.Equal("فتح المرفق", guaranteeEvent.EvidenceActionLabel);

            TimelineItem attachmentEvent = Assert.Single(artifacts.Timeline, item => item.Title == "إضافة مرفق صورة ضمان");
            Assert.Equal(TimelineEvidenceActionKind.Attachment, attachmentEvent.EvidenceActionKind);
            Assert.Equal("فتح المرفق", attachmentEvent.EvidenceActionLabel);
        }

        [Fact]
        public void BuildSelectionArtifacts_ForReplacementResponse_ShowsReplacementGuaranteeInsteadOfResultVersion()
        {
            DateTime start = new(2026, 4, 28, 8, 0, 0);
            var current = CreateGuarantee(1, null, 1, true, start, 1_500_000m);
            current.LifecycleStatus = GuaranteeLifecycleStatus.Replaced;
            var request = new WorkflowRequest
            {
                Id = 11,
                RootGuaranteeId = 1,
                SequenceNumber = 2,
                BaseVersionId = current.Id,
                ResultVersionId = 99,
                Type = RequestType.Replacement,
                Status = RequestStatus.Executed,
                RequestDate = start.AddHours(1),
                ResponseRecordedAt = start.AddHours(2),
                ResponseNotes = "تم اعتماد الاستبدال.",
                RequestedDataJson = JsonSerializer.Serialize(new WorkflowRequestedData
                {
                    ReplacementGuaranteeNo = "BG-ALT-0001"
                })
            };

            var service = new GuaranteeWorkspaceDataService(
                new TimelineDatabaseStub(new[] { current }, new[] { request }),
                new ContextActionService());
            GuaranteeRow row = GuaranteeRow.FromGuarantee(current, new[] { request });

            GuaranteeSelectionArtifacts artifacts = service.BuildSelectionArtifacts(row);
            TimelineItem responseItem = Assert.Single(
                artifacts.Timeline,
                item => item.Title == "تسجيل رد طلب استبدال");

            Assert.Contains("الضمان البديل: BG-ALT-0001", responseItem.Detail);
            Assert.DoesNotContain("الإصدار الناتج", responseItem.Detail);
        }

        [Fact]
        public void BuildSelectionArtifacts_ForReleaseAfterExtension_DatesLifecycleEndFromBankResponseOnly()
        {
            DateTime start = new(2026, 4, 28, 8, 0, 0);
            var original = CreateGuarantee(1, 1, 1, false, start, 1_500_000m);
            var extended = CreateGuarantee(2, 1, 2, true, start.AddHours(2), 1_500_000m);
            extended.LifecycleStatus = GuaranteeLifecycleStatus.Released;
            extended.ExpiryDate = new DateTime(2027, 2, 28);

            var extensionRequest = new WorkflowRequest
            {
                Id = 20,
                RootGuaranteeId = 1,
                SequenceNumber = 1,
                BaseVersionId = original.Id,
                ResultVersionId = extended.Id,
                Type = RequestType.Extension,
                Status = RequestStatus.Executed,
                RequestDate = start.AddHours(1),
                ResponseRecordedAt = start.AddHours(2),
                ResponseNotes = "تم تمديد الضمان."
            };
            var releaseRequest = new WorkflowRequest
            {
                Id = 21,
                RootGuaranteeId = 1,
                SequenceNumber = 2,
                BaseVersionId = extended.Id,
                Type = RequestType.Release,
                Status = RequestStatus.Executed,
                RequestDate = start.AddHours(3),
                ResponseRecordedAt = start.AddHours(4),
                ResponseNotes = "تم الإفراج بعد التمديد."
            };

            var service = new GuaranteeWorkspaceDataService(
                new TimelineDatabaseStub(new[] { extended, original }, new[] { extensionRequest, releaseRequest }),
                new ContextActionService());
            GuaranteeRow row = GuaranteeRow.FromGuarantee(extended, new[] { extensionRequest, releaseRequest });

            GuaranteeSelectionArtifacts artifacts = service.BuildSelectionArtifacts(row);

            Assert.Equal(
                new[]
                {
                    "إنشاء الضمان",
                    "طلب تمديد",
                    "تسجيل رد طلب تمديد",
                    "إصدار جديد v2",
                    "طلب إفراج",
                    "تسجيل رد طلب إفراج"
                },
                artifacts.Timeline.Select(item => item.Title));

            TimelineItem versionItem = Assert.Single(artifacts.Timeline, item => item.Title == "إصدار جديد v2");
            Assert.DoesNotContain("إنهاء دورة حياة", versionItem.Detail);
            Assert.DoesNotContain("مفرج", versionItem.Detail);

            TimelineItem releaseResponse = Assert.Single(artifacts.Timeline, item => item.Title == "تسجيل رد طلب إفراج");
            Assert.Equal("12:00:00", releaseResponse.Time);
            Assert.Contains("تم إنهاء دورة حياة الضمان بالإفراج", releaseResponse.Detail);
        }

        private static Guarantee CreateGuarantee(
            int id,
            int? rootId,
            int versionNumber,
            bool isCurrent,
            DateTime createdAt,
            decimal amount)
        {
            return new Guarantee
            {
                Id = id,
                RootId = rootId,
                VersionNumber = versionNumber,
                IsCurrent = isCurrent,
                GuaranteeNo = "BG-TEST-0001",
                Supplier = "مورد اختبار",
                Beneficiary = "مستفيد اختبار",
                Bank = "بنك ساب",
                Amount = amount,
                CreatedAt = createdAt,
                ExpiryDate = new DateTime(2026, 12, 31),
                GuaranteeType = "ابتدائي",
                LifecycleStatus = GuaranteeLifecycleStatus.Active
            };
        }

        private static GuaranteeTimelineEvent CreateEvent(
            int rootId,
            string eventKey,
            string eventType,
            int? guaranteeId,
            int? workflowRequestId,
            int? attachmentId,
            DateTime occurredAt,
            int sortOrder,
            string title,
            string details,
            string status,
            string toneKey)
        {
            return new GuaranteeTimelineEvent
            {
                RootId = rootId,
                EventKey = eventKey,
                EventType = eventType,
                GuaranteeId = guaranteeId,
                WorkflowRequestId = workflowRequestId,
                AttachmentId = attachmentId,
                OccurredAt = occurredAt,
                SortOrder = sortOrder,
                Title = title,
                Details = details,
                Status = status,
                ToneKey = toneKey
            };
        }

        private sealed class TimelineDatabaseStub : IDatabaseService
        {
            private readonly List<Guarantee> _history;
            private readonly List<WorkflowRequest> _requests;
            private readonly List<GuaranteeTimelineEvent> _events;

            public TimelineDatabaseStub(
                IEnumerable<Guarantee> history,
                IEnumerable<WorkflowRequest> requests,
                IEnumerable<GuaranteeTimelineEvent>? events = null)
            {
                _history = history.ToList();
                _requests = requests.ToList();
                _events = events?.ToList() ?? new List<GuaranteeTimelineEvent>();
            }

            public List<Guarantee> GetGuaranteeHistory(int guaranteeId) => _history;
            public Dictionary<int, IReadOnlyList<AttachmentRecord>> GetSeriesAttachmentsByRootIds(IReadOnlyCollection<int> rootIds)
                => _history
                    .GroupBy(guarantee => guarantee.RootId ?? guarantee.Id)
                    .Where(group => rootIds.Contains(group.Key))
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<AttachmentRecord>)group.SelectMany(guarantee => guarantee.Attachments).ToList());

            public List<GuaranteeTimelineEvent> GetGuaranteeTimelineEvents(int guaranteeId) => _events.ToList();

            public List<WorkflowRequest> GetWorkflowRequestsByRootId(int rootId)
                => _requests.Where(request => request.RootGuaranteeId == rootId).ToList();

            public void SaveGuarantee(Guarantee g, List<string> tempFilePaths) => throw new NotSupportedException();
            public void SaveGuaranteeWithAttachments(Guarantee g, List<AttachmentInput> attachments) => throw new NotSupportedException();
            public void AddGuaranteeAttachments(int guaranteeId, List<AttachmentInput> attachments) => throw new NotSupportedException();
            public int UpdateGuarantee(Guarantee g, List<string> newTempFiles, List<AttachmentRecord> removedAttachments) => throw new NotSupportedException();
            public int UpdateGuaranteeWithAttachments(Guarantee g, List<AttachmentInput> newAttachments, List<AttachmentRecord> removedAttachments) => throw new NotSupportedException();
            public List<Guarantee> QueryGuarantees(GuaranteeQueryOptions options) => throw new NotSupportedException();
            public int CountGuarantees(GuaranteeQueryOptions? options = null) => throw new NotSupportedException();
            public int CountAttachments() => throw new NotSupportedException();
            public List<Guarantee> SearchGuarantees(string query) => throw new NotSupportedException();
            public int SaveWorkflowRequest(WorkflowRequest req) => throw new NotSupportedException();
            public bool HasPendingWorkflowRequest(int rootId, RequestType requestType) => throw new NotSupportedException();
            public int GetPendingWorkflowRequestCount() => throw new NotSupportedException();
            public WorkflowRequest? GetWorkflowRequestById(int requestId) => throw new NotSupportedException();
            public List<WorkflowRequestListItem> QueryWorkflowRequests(WorkflowRequestQueryOptions options) => throw new NotSupportedException();
            public int CountWorkflowRequests(WorkflowRequestQueryOptions? options = null) => throw new NotSupportedException();
            public List<WorkflowRequestListItem> SearchWorkflowRequests(string query) => throw new NotSupportedException();
            public void RecordWorkflowResponse(int requestId, RequestStatus newStatus, string responseNotes, string responseOriginalFileName, string responseSavedFileName, int? resultVersionId = null) => throw new NotSupportedException();
            public void AttachWorkflowResponseDocument(int requestId, string responseNotes, string responseOriginalFileName, string responseSavedFileName) => throw new NotSupportedException();
            public int ExecuteExtensionWorkflowRequest(int requestId, DateTime newExpiryDate, string responseNotes, string responseOriginalFileName, string responseSavedFileName, string? responseAttachmentSourcePath = null) => throw new NotSupportedException();
            public int ExecuteReductionWorkflowRequest(int requestId, decimal newAmount, string responseNotes, string responseOriginalFileName, string responseSavedFileName, string? responseAttachmentSourcePath = null) => throw new NotSupportedException();
            public int ExecuteReleaseWorkflowRequest(int requestId, string responseNotes, string responseOriginalFileName, string responseSavedFileName, string? responseAttachmentSourcePath = null) => throw new NotSupportedException();
            public int ExecuteLiquidationWorkflowRequest(int requestId, string responseNotes, string responseOriginalFileName, string responseSavedFileName, string? responseAttachmentSourcePath = null) => throw new NotSupportedException();
            public int? ExecuteVerificationWorkflowRequest(int requestId, string responseNotes, string responseOriginalFileName, string responseSavedFileName, string? responseAttachmentSourcePath = null, bool promoteResponseDocumentToOfficialAttachment = false) => throw new NotSupportedException();
            public int ExecuteReplacementWorkflowRequest(int requestId, string replacementGuaranteeNo, string replacementSupplier, string replacementBank, decimal replacementAmount, DateTime replacementExpiryDate, string replacementGuaranteeType, string replacementBeneficiary, GuaranteeReferenceType replacementReferenceType, string replacementReferenceNumber, string responseNotes, string responseOriginalFileName, string responseSavedFileName, string? responseAttachmentSourcePath = null) => throw new NotSupportedException();
            public void DeleteAttachment(AttachmentRecord att) => throw new NotSupportedException();
            public void AddBankReference(string bankName) => throw new NotSupportedException();
            public List<string> GetBankReferences() => throw new NotSupportedException();
            public List<string> GetUniqueValues(string columnName) => throw new NotSupportedException();
            public bool IsGuaranteeNoUnique(string guaranteeNo) => throw new NotSupportedException();
            public Guarantee? GetGuaranteeById(int guaranteeId) => throw new NotSupportedException();
            public Guarantee? GetCurrentGuaranteeByRootId(int rootId) => throw new NotSupportedException();
            public Guarantee? GetCurrentGuaranteeByNo(string guaranteeNo) => throw new NotSupportedException();
            public int CreateNewVersion(Guarantee newG, int sourceId, List<string> newTempFiles, List<AttachmentRecord> inheritedAttachments) => throw new NotSupportedException();
            public int CreateNewVersionWithAttachments(Guarantee newG, int sourceId, List<AttachmentInput> newAttachments, List<AttachmentRecord> inheritedAttachments) => throw new NotSupportedException();
        }
    }
}
