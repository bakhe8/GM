using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GuaranteeManager.Tests
{
    [Collection("Database")]
    public sealed class DatabaseGuaranteePersistenceTests : DatabaseWorkflowTestBase
    {
        public DatabaseGuaranteePersistenceTests(TestEnvironmentFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public void SaveGuarantee_PersistsCurrentGuaranteeWithRoot()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            Guarantee input = _fixture.CreateGuarantee();

            database.SaveGuarantee(input, new List<string>());

            Guarantee? persisted = database.GetCurrentGuaranteeByNo(input.GuaranteeNo);

            Assert.NotNull(persisted);
            Assert.Equal(persisted!.Id, persisted.RootId);
            Assert.True(persisted.IsCurrent);
            Assert.Equal(1, persisted.VersionNumber);
            Assert.Equal(input.Supplier, persisted.Supplier);
            Assert.Equal(input.ReferenceNumber, persisted.ReferenceNumber);
        }

        [Fact]
        public void AddBankReference_PersistsStandaloneBankAndExposesItAsUniqueBank()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string bankName = $"Reference Bank {_fixture.NextToken("BANK")}";

            database.AddBankReference(bankName);

            Assert.Contains(bankName, database.GetBankReferences());
            Assert.Contains(bankName, database.GetUniqueValues("Bank"));
        }

        [Fact]
        public void UpdateGuarantee_CreatesNewCurrentVersionAndKeepsHistory()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string sourceAttachmentPath = _fixture.CreateSourceFile(contents: "inherited-attachment");
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string> { sourceAttachmentPath });
            Guarantee original = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            original.Amount += 250m;
            original.ExpiryDate = original.ExpiryDate.AddDays(30);
            original.Notes = "updated-version";

            int newVersionId = database.UpdateGuarantee(
                original,
                new List<string>(),
                new List<AttachmentRecord>());

            Guarantee? current = database.GetCurrentGuaranteeByRootId(original.RootId ?? original.Id);
            List<Guarantee> history = database.GetGuaranteeHistory(newVersionId);

            Assert.NotNull(current);
            Assert.Equal(newVersionId, current!.Id);
            Assert.True(current.IsCurrent);
            Assert.Equal(2, current.VersionNumber);
            Assert.Single(current.Attachments);
            Assert.Equal(2, history.Count);
            Assert.Contains(history, item => item.Id == original.Id && !item.IsCurrent);
            Assert.Contains(history, item => item.Id == newVersionId && item.IsCurrent);
        }

        [Fact]
        public void SaveGuarantee_WithAttachment_PersistsMetadataAndMovesFile()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string sourceAttachmentPath = _fixture.CreateSourceFile(contents: "attachment-body");
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string> { sourceAttachmentPath });

            Guarantee persisted = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            AttachmentRecord attachment = Assert.Single(persisted.Attachments);

            Assert.Equal(".txt", attachment.FileExtension);
            Assert.Equal(AttachmentDocumentType.SupportingDocument, attachment.DocumentType);
            Assert.True(File.Exists(attachment.FilePath));
            Assert.NotEqual(sourceAttachmentPath, attachment.FilePath);
            Assert.Equal("attachment-body", File.ReadAllText(attachment.FilePath));
        }

        [Fact]
        public void SaveGuaranteeWithAttachments_PersistsDocumentType()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string sourceAttachmentPath = _fixture.CreateSourceFile(contents: "guarantee-image");
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuaranteeWithAttachments(
                seed,
                new List<AttachmentInput>
                {
                    new(sourceAttachmentPath, AttachmentDocumentType.GuaranteeImage)
                });

            Guarantee persisted = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            AttachmentRecord attachment = Assert.Single(persisted.Attachments);

            Assert.Equal(AttachmentDocumentType.GuaranteeImage, attachment.DocumentType);
            Assert.Equal("صورة ضمان", attachment.DocumentTypeLabel);
        }

        [Fact]
        public void AddGuaranteeAttachments_AppendsEvidenceWithoutCreatingNewVersion()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string sourceAttachmentPath = _fixture.CreateSourceFile(".pdf", "timeline-evidence");
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            database.AddGuaranteeAttachments(
                current.Id,
                new List<AttachmentInput>
                {
                    new(
                        sourceAttachmentPath,
                        AttachmentDocumentType.SupportingDocument,
                        $"guarantee-created:{current.Id}")
                });

            Guarantee reloaded = database.GetGuaranteeById(current.Id)!;
            List<Guarantee> history = database.GetGuaranteeHistory(current.Id);
            List<GuaranteeTimelineEvent> events = database.GetGuaranteeTimelineEvents(current.Id);

            AttachmentRecord attachment = Assert.Single(reloaded.Attachments);
            Assert.Equal(AttachmentDocumentType.SupportingDocument, attachment.DocumentType);
            Assert.Equal($"guarantee-created:{current.Id}", attachment.TimelineEventKey);
            Assert.True(File.Exists(attachment.FilePath));
            Assert.Single(history);
            Assert.DoesNotContain(events, item => item.EventType == "AttachmentAdded" && item.AttachmentId == attachment.Id);
        }

        [Fact]
        public void CountAttachments_CountsOnlyAttachmentsLinkedToCurrentVersion()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string inheritedAttachmentPath = _fixture.CreateSourceFile(contents: "attachment-v1");
            string newAttachmentPath = _fixture.CreateSourceFile(contents: "attachment-v2");
            Guarantee seed = _fixture.CreateGuarantee();
            int baselineAttachmentCount = database.CountAttachments();

            database.SaveGuarantee(seed, new List<string> { inheritedAttachmentPath });
            Guarantee original = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            original.Notes = "second-version";

            database.UpdateGuarantee(
                original,
                new List<string> { newAttachmentPath },
                new List<AttachmentRecord>());

            Guarantee current = database.GetCurrentGuaranteeByRootId(original.RootId ?? original.Id)!;

            Assert.Equal(baselineAttachmentCount + 2, database.CountAttachments());
            Assert.Equal(2, current.Attachments.Count);
            Assert.All(current.Attachments, attachment => Assert.True(File.Exists(attachment.FilePath)));
        }

        [Fact]
        public void SaveGuarantee_DuplicateNormalizedGuaranteeNo_ThrowsAndKeepsSingleCurrentRecord()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string canonicalNumber = $"  DUP-{_fixture.NextToken("NO")}  ";
            Guarantee original = _fixture.CreateGuarantee(canonicalNumber);
            Guarantee duplicate = _fixture.CreateGuarantee(canonicalNumber.ToLowerInvariant());

            database.SaveGuarantee(original, new List<string>());

            Assert.ThrowsAny<Exception>(() => database.SaveGuarantee(duplicate, new List<string>()));
            Assert.NotNull(database.GetCurrentGuaranteeByNo(canonicalNumber));
            Assert.Equal(1, database.CountGuarantees(new GuaranteeQueryOptions
            {
                SearchText = canonicalNumber.Trim()
            }));
        }

    }
}
