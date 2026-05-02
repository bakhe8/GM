using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class GuaranteeWorkspaceDataServiceTests
    {
        [Fact]
        public void GuaranteeRow_FromGuarantee_UsesSupplierAsDisplayedParty()
        {
            DateTime start = new(2026, 4, 28, 8, 0, 0);
            Guarantee guarantee = CreateGuarantee(1, null, 1, true, start, 1_500_000m);
            guarantee.Supplier = "شركة التشغيل الطبي";
            guarantee.Beneficiary = BusinessPartyDefaults.DefaultBeneficiaryName;

            GuaranteeRow row = GuaranteeRow.FromGuarantee(guarantee, Array.Empty<WorkflowRequest>());

            Assert.Equal("شركة التشغيل الطبي", row.Supplier);
            Assert.Equal(BusinessPartyDefaults.DefaultBeneficiaryName, row.Beneficiary);
            Assert.Equal("\u20C1 1,500,000", row.Amount);
            Assert.Equal("مليون وخمسمئة ألف ريال سعودي \u20C1", row.AmountDescription);
            Assert.Equal("BG-TEST-0001 | شركة التشغيل الطبي", row.RowAutomationName);
        }

        [Fact]
        public void BuildSnapshot_SeparatesTerminalExpiredFromExpiredNeedsClosure()
        {
            DateTime start = new(2026, 4, 28, 8, 0, 0);
            Guarantee expiredOpen = CreateGuarantee(10, null, 1, true, start, 2_000m);
            expiredOpen.GuaranteeNo = "BG-EXPIRED-OPEN";
            expiredOpen.ExpiryDate = DateTime.Today.AddDays(-3);
            expiredOpen.LifecycleStatus = GuaranteeLifecycleStatus.Active;

            Guarantee expiredClosed = CreateGuarantee(11, null, 1, true, start, 1_000m);
            expiredClosed.GuaranteeNo = "BG-EXPIRED-CLOSED";
            expiredClosed.ExpiryDate = DateTime.Today.AddDays(-5);
            expiredClosed.LifecycleStatus = GuaranteeLifecycleStatus.Expired;

            Guarantee expiredReleased = CreateGuarantee(12, null, 1, true, start, 4_000m);
            expiredReleased.GuaranteeNo = "BG-EXPIRED-RELEASED";
            expiredReleased.ExpiryDate = DateTime.Today.AddDays(-7);
            expiredReleased.LifecycleStatus = GuaranteeLifecycleStatus.Released;

            Guarantee closedBeforeExpiry = CreateGuarantee(14, null, 1, true, start, 5_000m);
            closedBeforeExpiry.GuaranteeNo = "BG-CLOSED-BEFORE-EXPIRY";
            closedBeforeExpiry.ExpiryDate = DateTime.Today.AddDays(20);
            closedBeforeExpiry.LifecycleStatus = GuaranteeLifecycleStatus.Expired;

            Guarantee expiredClosedWithPendingRequest = CreateGuarantee(15, null, 1, true, start, 6_000m);
            expiredClosedWithPendingRequest.GuaranteeNo = "BG-EXPIRED-CLOSED-WITH-PENDING";
            expiredClosedWithPendingRequest.ExpiryDate = DateTime.Today.AddDays(-6);
            expiredClosedWithPendingRequest.LifecycleStatus = GuaranteeLifecycleStatus.Expired;

            Guarantee expiredTerminalWithPendingRequest = CreateGuarantee(16, null, 1, true, start, 7_000m);
            expiredTerminalWithPendingRequest.GuaranteeNo = "BG-EXPIRED-TERMINAL-WITH-PENDING";
            expiredTerminalWithPendingRequest.ExpiryDate = DateTime.Today.AddDays(-8);
            expiredTerminalWithPendingRequest.LifecycleStatus = GuaranteeLifecycleStatus.Liquidated;

            Guarantee active = CreateGuarantee(13, null, 1, true, start, 3_000m);
            active.GuaranteeNo = "BG-ACTIVE";
            active.ExpiryDate = DateTime.Today.AddDays(90);

            WorkflowRequestListItem pendingRequest = CreatePendingRequest(expiredClosedWithPendingRequest, 601);
            WorkflowRequestListItem pendingTerminalRequest = CreatePendingRequest(expiredTerminalWithPendingRequest, 602);
            var service = new GuaranteeWorkspaceDataService(
                new TimelineDatabaseStub(
                    new[] { expiredOpen, expiredClosed, expiredReleased, closedBeforeExpiry, expiredClosedWithPendingRequest, expiredTerminalWithPendingRequest, active },
                    Array.Empty<WorkflowRequest>(),
                    requestItems: new[] { pendingRequest, pendingTerminalRequest }),
                new ContextActionService());

            GuaranteeWorkspaceSnapshot snapshot = service.BuildSnapshot(
                string.Empty,
                "كل البنوك",
                "كل البنوك",
                "كل الأنواع",
                "كل الأنواع",
                GuaranteeStatusFilter.Active,
                10,
                1);

            Assert.Equal("1", snapshot.ExpiredCount);
            Assert.Equal("3", snapshot.ExpiredFollowUpCount);

            GuaranteeWorkspaceSnapshot expiredFilterSnapshot = service.BuildSnapshot(
                string.Empty,
                "كل البنوك",
                "كل البنوك",
                "كل الأنواع",
                "كل الأنواع",
                GuaranteeStatusFilter.Expired,
                10,
                1);

            GuaranteeRow row = Assert.Single(expiredFilterSnapshot.Rows);
            Assert.Equal(expiredReleased.GuaranteeNo, row.GuaranteeNo);
            Assert.DoesNotContain(
                expiredFilterSnapshot.Rows,
                row => row.GuaranteeNo == expiredClosed.GuaranteeNo);
            Assert.DoesNotContain(
                expiredFilterSnapshot.Rows,
                row => row.GuaranteeNo == closedBeforeExpiry.GuaranteeNo);
            Assert.DoesNotContain(
                expiredFilterSnapshot.Rows,
                row => row.GuaranteeNo == expiredClosedWithPendingRequest.GuaranteeNo);
            Assert.DoesNotContain(
                expiredFilterSnapshot.Rows,
                row => row.GuaranteeNo == expiredTerminalWithPendingRequest.GuaranteeNo);
        }

        [Fact]
        public void BuildSnapshot_SplitsExpiringSoonByPendingRequestPresence()
        {
            DateTime start = new(2026, 4, 28, 8, 0, 0);
            Guarantee expiringWithRequest = CreateGuarantee(20, null, 1, true, start, 5_000m);
            expiringWithRequest.GuaranteeNo = "BG-SOON-WITH-REQUEST";
            expiringWithRequest.ExpiryDate = DateTime.Today.AddDays(5);

            Guarantee expiringWithoutRequest = CreateGuarantee(21, null, 1, true, start, 6_000m);
            expiringWithoutRequest.GuaranteeNo = "BG-SOON-WITHOUT-REQUEST";
            expiringWithoutRequest.ExpiryDate = DateTime.Today.AddDays(7);

            Guarantee expiredOpen = CreateGuarantee(22, null, 1, true, start, 7_000m);
            expiredOpen.GuaranteeNo = "BG-EXPIRED-OPEN";
            expiredOpen.ExpiryDate = DateTime.Today.AddDays(-2);

            WorkflowRequestListItem pendingRequest = CreatePendingRequest(expiringWithRequest, 701);
            var service = new GuaranteeWorkspaceDataService(
                new TimelineDatabaseStub(
                    new[] { expiringWithRequest, expiringWithoutRequest, expiredOpen },
                    Array.Empty<WorkflowRequest>(),
                    requestItems: new[] { pendingRequest }),
                new ContextActionService());

            GuaranteeWorkspaceSnapshot expiringSnapshot = service.BuildSnapshot(
                string.Empty,
                "كل البنوك",
                "كل البنوك",
                "كل الأنواع",
                "كل الأنواع",
                GuaranteeStatusFilter.ExpiringSoon,
                10,
                1);

            GuaranteeRow expiringRow = Assert.Single(expiringSnapshot.Rows);
            Assert.Equal(expiringWithoutRequest.GuaranteeNo, expiringRow.GuaranteeNo);
            Assert.Equal("1", expiringSnapshot.ExpiringSoonCount);
            Assert.Equal("2", expiringSnapshot.ExpiredFollowUpCount);

            GuaranteeWorkspaceSnapshot followUpSnapshot = service.BuildSnapshot(
                string.Empty,
                "كل البنوك",
                "كل البنوك",
                "كل الأنواع",
                "كل الأنواع",
                GuaranteeStatusFilter.NeedsFollowUp,
                10,
                1);

            Assert.Contains(followUpSnapshot.Rows, row => row.GuaranteeNo == expiringWithRequest.GuaranteeNo);
            Assert.Contains(followUpSnapshot.Rows, row => row.GuaranteeNo == expiredOpen.GuaranteeNo);
            Assert.DoesNotContain(followUpSnapshot.Rows, row => row.GuaranteeNo == expiringWithoutRequest.GuaranteeNo);
        }

        [Fact]
        public void BuildSelectionArtifacts_ForCurrentGuarantee_BuildsNewestFirstTimeline()
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
                    "إضافة مرفق رد البنك",
                    "تسجيل رد طلب إفراج",
                    "طلب إفراج",
                    "إنشاء الضمان"
                },
                artifacts.Timeline.Select(item => item.Title));
            Assert.DoesNotContain("الإصدار الناتج", artifacts.Timeline[1].Detail);
            Assert.Contains("تم إنهاء دورة حياة الضمان بالإفراج", artifacts.Timeline[1].Detail);
            Assert.Equal(TimelineEvidenceActionKind.ResponseDocument, artifacts.Timeline[1].EvidenceActionKind);
            Assert.Equal("فتح رد البنك", artifacts.Timeline[1].EvidenceActionLabel);
            Assert.Equal(TimelineEvidenceActionKind.Attachment, artifacts.Timeline[0].EvidenceActionKind);
            Assert.Equal("فتح المرفق", artifacts.Timeline[0].EvidenceActionLabel);
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
                    "تسجيل رد طلب إفراج",
                    "طلب إفراج",
                    "الإصدار الثاني",
                    "تسجيل رد طلب تمديد",
                    "طلب تمديد",
                    "إنشاء الضمان"
                },
                artifacts.Timeline.Select(item => item.Title));

            TimelineItem versionItem = Assert.Single(artifacts.Timeline, item => item.Title == "الإصدار الثاني");
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
                Beneficiary = BusinessPartyDefaults.DefaultBeneficiaryName,
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

        private static WorkflowRequestListItem CreatePendingRequest(Guarantee guarantee, int requestId)
        {
            return new WorkflowRequestListItem
            {
                Request = new WorkflowRequest
                {
                    Id = requestId,
                    RootGuaranteeId = guarantee.RootId ?? guarantee.Id,
                    BaseVersionId = guarantee.Id,
                    SequenceNumber = 1,
                    Type = RequestType.Extension,
                    Status = RequestStatus.Pending,
                    RequestDate = DateTime.Today.AddDays(-2)
                },
                CurrentGuaranteeId = guarantee.Id,
                RootGuaranteeId = guarantee.RootId ?? guarantee.Id,
                GuaranteeNo = guarantee.GuaranteeNo,
                Supplier = guarantee.Supplier,
                Bank = guarantee.Bank,
                CurrentAmount = guarantee.Amount,
                CurrentExpiryDate = guarantee.ExpiryDate,
                CurrentVersionNumber = guarantee.VersionNumber,
                BaseVersionNumber = guarantee.VersionNumber,
                LifecycleStatus = guarantee.LifecycleStatus
            };
        }

        private sealed class TimelineDatabaseStub : IDatabaseService
        {
            private readonly List<Guarantee> _history;
            private readonly List<WorkflowRequest> _requests;
            private readonly List<WorkflowRequestListItem> _requestItems;
            private readonly List<GuaranteeTimelineEvent> _events;

            public TimelineDatabaseStub(
                IEnumerable<Guarantee> history,
                IEnumerable<WorkflowRequest> requests,
                IEnumerable<GuaranteeTimelineEvent>? events = null,
                IEnumerable<WorkflowRequestListItem>? requestItems = null)
            {
                _history = history.ToList();
                _requests = requests.ToList();
                _requestItems = requestItems?.ToList() ?? new List<WorkflowRequestListItem>();
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
            public List<Guarantee> QueryGuarantees(GuaranteeQueryOptions options)
            {
                IEnumerable<Guarantee> query = _history.Where(guarantee => guarantee.IsCurrent);
                string search = options.SearchText?.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(guarantee =>
                        guarantee.GuaranteeNo.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        guarantee.Supplier.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        guarantee.Bank.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        guarantee.GuaranteeType.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        guarantee.ReferenceNumber.Contains(search, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(options.Bank))
                {
                    query = query.Where(guarantee => string.Equals(guarantee.Bank, options.Bank, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(options.GuaranteeType))
                {
                    query = query.Where(guarantee => string.Equals(guarantee.GuaranteeType, options.GuaranteeType, StringComparison.OrdinalIgnoreCase));
                }

                if (options.LifecycleStatus.HasValue)
                {
                    query = query.Where(guarantee => guarantee.LifecycleStatus == options.LifecycleStatus.Value);
                }

                if (options.LifecycleStatuses is { Count: > 0 })
                {
                    HashSet<GuaranteeLifecycleStatus> statuses = options.LifecycleStatuses.ToHashSet();
                    query = query.Where(guarantee => statuses.Contains(guarantee.LifecycleStatus));
                }

                if (options.NeedsExpiryFollowUpOnly)
                {
                    query = query.Where(guarantee => guarantee.NeedsExpiryFollowUp);
                }
                else if (options.TimeStatus.HasValue)
                {
                    query = options.TimeStatus.Value switch
                    {
                        GuaranteeTimeStatus.Active => query.Where(guarantee => !guarantee.IsExpired && !guarantee.IsExpiringSoon),
                        GuaranteeTimeStatus.ExpiringSoon => query.Where(guarantee => guarantee.IsExpiringSoon),
                        GuaranteeTimeStatus.Expired => query.Where(guarantee => guarantee.IsExpired),
                        _ => query
                    };
                }

                query = options.SortMode == GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
                    ? query.OrderBy(guarantee => guarantee.ExpiryDate).ThenBy(guarantee => guarantee.GuaranteeNo)
                    : query.OrderByDescending(guarantee => guarantee.CreatedAt).ThenBy(guarantee => guarantee.GuaranteeNo);

                if (options.Offset.HasValue)
                {
                    query = query.Skip(options.Offset.Value);
                }

                if (options.Limit.HasValue)
                {
                    query = query.Take(options.Limit.Value);
                }

                return query.ToList();
            }
            public int CountGuarantees(GuaranteeQueryOptions? options = null) => throw new NotSupportedException();
            public int CountAttachments() => throw new NotSupportedException();
            public List<Guarantee> SearchGuarantees(string query) => throw new NotSupportedException();
            public int SaveWorkflowRequest(WorkflowRequest req) => throw new NotSupportedException();
            public bool HasPendingWorkflowRequest(int rootId, RequestType requestType) => throw new NotSupportedException();
            public int GetPendingWorkflowRequestCount() => throw new NotSupportedException();
            public WorkflowRequest? GetWorkflowRequestById(int requestId) => throw new NotSupportedException();
            public List<WorkflowRequestListItem> QueryWorkflowRequests(WorkflowRequestQueryOptions options)
            {
                IEnumerable<WorkflowRequestListItem> query = _requestItems;

                if (options.RequestStatus.HasValue)
                {
                    query = query.Where(item => item.Request.Status == options.RequestStatus.Value);
                }

                return query.ToList();
            }
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
            public Guarantee? GetGuaranteeById(int guaranteeId) => _history.FirstOrDefault(guarantee => guarantee.Id == guaranteeId);
            public Guarantee? GetCurrentGuaranteeByRootId(int rootId) => throw new NotSupportedException();
            public Guarantee? GetCurrentGuaranteeByNo(string guaranteeNo) => throw new NotSupportedException();
            public int CreateNewVersion(Guarantee newG, int sourceId, List<string> newTempFiles, List<AttachmentRecord> inheritedAttachments) => throw new NotSupportedException();
            public int CreateNewVersionWithAttachments(Guarantee newG, int sourceId, List<AttachmentInput> newAttachments, List<AttachmentRecord> inheritedAttachments) => throw new NotSupportedException();
        }
    }
}
