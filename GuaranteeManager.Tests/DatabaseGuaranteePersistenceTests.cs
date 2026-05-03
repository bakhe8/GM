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
        public void SaveGuarantee_PersistsDateCalendarPreference()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            Guarantee input = _fixture.CreateGuarantee();
            input.DateCalendar = GuaranteeDateCalendar.Hijri;

            database.SaveGuarantee(input, new List<string>());

            Guarantee persisted = database.GetCurrentGuaranteeByNo(input.GuaranteeNo)!;

            Assert.Equal(GuaranteeDateCalendar.Hijri, persisted.DateCalendar);
            Assert.Contains("هـ", persisted.WorkflowDisplayLabel);
            Assert.DoesNotContain(" م |", persisted.WorkflowDisplayLabel);
        }

        [Fact]
        public void SaveGuarantee_RejectsAmountWithMoreThanTwoHalalaDigits()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            Guarantee input = _fixture.CreateGuarantee();
            input.Amount = 1000.123m;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => database.SaveGuarantee(input, new List<string>()));

            Assert.Contains("خانتين للهلل", exception.Message);
        }

        [Fact]
        public void SaveGuarantee_RejectsNegativeAmount()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            Guarantee input = _fixture.CreateGuarantee();
            input.Amount = -1000m;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => database.SaveGuarantee(input, new List<string>()));

            Assert.Contains("بالسالب", exception.Message);
        }

        [Fact]
        public void SaveGuarantee_RejectsZeroAmount()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            Guarantee input = _fixture.CreateGuarantee();
            input.Amount = 0m;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => database.SaveGuarantee(input, new List<string>()));

            Assert.Contains("أكبر من صفر", exception.Message);
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
        public void GetBankPortfolioSummaries_AggregatesCurrentGuaranteesInDatabase()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string bankName = $"Summary Bank {_fixture.NextToken("BANK")}";
            string otherBankName = $"Summary Bank {_fixture.NextToken("BANK")}";

            Guarantee first = _fixture.CreateGuarantee();
            first.Bank = bankName;
            first.Supplier = "Summary Supplier A";
            first.Amount = 1000m;
            first.ExpiryDate = DateTime.Today.AddDays(10);

            Guarantee second = _fixture.CreateGuarantee();
            second.Bank = bankName;
            second.Supplier = "Summary Supplier A";
            second.Amount = 500m;
            second.ExpiryDate = DateTime.Today.AddDays(-1);

            Guarantee other = _fixture.CreateGuarantee();
            other.Bank = otherBankName;
            other.Supplier = "Summary Supplier B";
            other.Amount = 750m;
            other.ExpiryDate = DateTime.Today.AddDays(60);

            database.SaveGuarantee(first, new List<string>());
            database.SaveGuarantee(second, new List<string>());
            database.SaveGuarantee(other, new List<string>());

            List<BankPortfolioSummary> summaries = database.GetBankPortfolioSummaries();

            BankPortfolioSummary summary = Assert.Single(summaries, item => item.Bank == bankName);
            Assert.Equal(2, summary.Count);
            Assert.Equal(2, summary.Active);
            Assert.Equal(1, summary.ExpiringSoon);
            Assert.Equal(1, summary.Expired);
            Assert.Equal(1500m, summary.Amount);
            Assert.Equal("Summary Supplier A", summary.TopSupplier);
        }

        [Fact]
        public void UpdateGuarantee_CreatesNewCurrentVersionAndKeepsHistory()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string sourceAttachmentPath = _fixture.CreateSourceFile(contents: "inherited-attachment");
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string> { sourceAttachmentPath });
            Guarantee original = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            original.Supplier = $"{original.Supplier} Updated";
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
        public void UpdateGuarantee_RejectsOperationalFieldMutations()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            (string FieldLabel, Action<Guarantee> Mutate)[] mutations =
            {
                ("رقم الضمان", guarantee => guarantee.GuaranteeNo += "-CHANGED"),
                ("البنك", guarantee => guarantee.Bank += " Changed"),
                ("نوع الضمان", guarantee => guarantee.GuaranteeType += " Changed"),
                ("المبلغ", guarantee => guarantee.Amount += 250m),
                ("تاريخ الانتهاء", guarantee => guarantee.ExpiryDate = guarantee.ExpiryDate.AddDays(30)),
                ("تقويم التاريخ", guarantee => guarantee.DateCalendar = GuaranteeDateCalendar.Hijri),
                ("نوع المرجع", guarantee => guarantee.ReferenceType = GuaranteeReferenceType.PurchaseOrder),
                ("رقم المرجع", guarantee => guarantee.ReferenceNumber += "-CHANGED"),
                ("الحالة التشغيلية", guarantee => guarantee.LifecycleStatus = GuaranteeLifecycleStatus.Released),
                ("رابط الضمان المستبدل", guarantee => guarantee.ReplacesRootId = 12345),
                ("رابط الضمان البديل", guarantee => guarantee.ReplacedByRootId = 67890)
            };

            foreach ((string fieldLabel, Action<Guarantee> mutate) in mutations)
            {
                Guarantee seed = _fixture.CreateGuarantee();
                database.SaveGuarantee(seed, new List<string>());
                Guarantee current = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
                int currentId = current.Id;
                int rootId = current.RootId ?? current.Id;

                mutate(current);
                current.Notes = $"blocked mutation for {fieldLabel}";

                OperationalFieldMutationException exception = Assert.Throws<OperationalFieldMutationException>(
                    () => database.UpdateGuarantee(
                        current,
                        new List<string>(),
                        new List<AttachmentRecord>()));
                Guarantee persisted = database.GetCurrentGuaranteeByRootId(rootId)!;
                List<Guarantee> history = database.GetGuaranteeHistory(currentId);

                Assert.Contains(fieldLabel, exception.Message);
                Assert.Equal(currentId, persisted.Id);
                Assert.Equal(1, persisted.VersionNumber);
                Assert.Single(history);
            }
        }

        [Fact]
        public void GuaranteeVersionConstraints_RejectDuplicateCurrentAndDuplicateVersionNumbers()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee original = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            original.Supplier = $"{original.Supplier} Updated";
            int newVersionId = database.UpdateGuarantee(original, new List<string>(), new List<AttachmentRecord>());

            using SqliteConnection connection = SqliteConnectionFactory.OpenForPath(AppPaths.DatabasePath);

            using SqliteCommand duplicateCurrent = connection.CreateCommand();
            duplicateCurrent.CommandText = "UPDATE Guarantees SET IsCurrent = 1 WHERE Id = $id";
            duplicateCurrent.Parameters.AddWithValue("$id", original.Id);
            Assert.Throws<SqliteException>(() => duplicateCurrent.ExecuteNonQuery());

            using SqliteCommand duplicateVersion = connection.CreateCommand();
            duplicateVersion.CommandText = "UPDATE Guarantees SET VersionNumber = 1 WHERE Id = $id";
            duplicateVersion.Parameters.AddWithValue("$id", newVersionId);
            Assert.Throws<SqliteException>(() => duplicateVersion.ExecuteNonQuery());
        }

        [Fact]
        public void GuaranteeAmountConstraint_RejectsNegativeRawDatabaseWrites()
        {
            string guaranteeNo = $"RAW-NEG-{_fixture.NextToken("NO")}";

            using SqliteConnection connection = SqliteConnectionFactory.OpenForPath(AppPaths.DatabasePath);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Guarantees (
                    Supplier, Bank, GuaranteeNo, Amount, ExpiryDate, GuaranteeType, Beneficiary, Notes,
                    CreatedAt, RootId, VersionNumber, IsCurrent, ReferenceType, ReferenceNumber, LifecycleStatus, DateCalendar
                )
                VALUES (
                    'Supplier', 'Bank', $guaranteeNo, -1, $expiryDate, 'Performance', $beneficiary, '',
                    $createdAt, NULL, 1, 1, 'None', '', 'Active', 'Gregorian'
                )";
            command.Parameters.AddWithValue("$guaranteeNo", guaranteeNo);
            command.Parameters.AddWithValue("$expiryDate", PersistedDateTime.FormatDate(DateTime.Today.AddDays(30)));
            command.Parameters.AddWithValue("$beneficiary", BusinessPartyDefaults.DefaultBeneficiaryName);
            command.Parameters.AddWithValue("$createdAt", PersistedDateTime.FormatDateTime(DateTime.Now));

            Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
        }

        [Fact]
        public void GuaranteeRelationshipTriggers_RejectDanglingRootReferences()
        {
            string guaranteeNo = $"RAW-ROOT-{_fixture.NextToken("NO")}";

            using SqliteConnection connection = SqliteConnectionFactory.OpenForPath(AppPaths.DatabasePath);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Guarantees (
                    Supplier, Bank, GuaranteeNo, Amount, ExpiryDate, GuaranteeType, Beneficiary, Notes,
                    CreatedAt, RootId, VersionNumber, IsCurrent, ReferenceType, ReferenceNumber, LifecycleStatus, DateCalendar
                )
                VALUES (
                    'Supplier', 'Bank', $guaranteeNo, 1000, $expiryDate, 'Performance', $beneficiary, '',
                    $createdAt, 999999999, 1, 1, 'None', '', 'Active', 'Gregorian'
                )";
            command.Parameters.AddWithValue("$guaranteeNo", guaranteeNo);
            command.Parameters.AddWithValue("$expiryDate", PersistedDateTime.FormatDate(DateTime.Today.AddDays(30)));
            command.Parameters.AddWithValue("$beneficiary", BusinessPartyDefaults.DefaultBeneficiaryName);
            command.Parameters.AddWithValue("$createdAt", PersistedDateTime.FormatDateTime(DateTime.Now));

            Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
        }

        [Fact]
        public void DeleteGuarantee_IsBlocked_EnsuringVersionHistoryPermanence()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee persisted = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;

            using SqliteConnection connection = SqliteConnectionFactory.OpenForPath(AppPaths.DatabasePath);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Guarantees WHERE Id = $id";
            command.Parameters.AddWithValue("$id", persisted.Id);

            Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
            Assert.NotNull(database.GetCurrentGuaranteeByNo(seed.GuaranteeNo));
        }

        [Fact]
        public void DeleteAttachment_IsBlocked_EnsuringAuditTrailPermanence()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string sourceAttachmentPath = _fixture.CreateSourceFile(contents: "permanent-attachment");
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string> { sourceAttachmentPath });
            Guarantee persisted = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            AttachmentRecord attachment = Assert.Single(persisted.Attachments);

            using SqliteConnection connection = SqliteConnectionFactory.OpenForPath(AppPaths.DatabasePath);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Attachments WHERE Id = $id";
            command.Parameters.AddWithValue("$id", attachment.Id);

            Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
            Assert.Single(database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!.Attachments);
        }

        [Fact]
        public void SaveGuarantee_WhenAttachmentPromotionIsDeferred_CanRecoverStagedFile()
        {
            var storage = new DeferringAttachmentStorageService();
            var database = new DatabaseService(storage);
            string sourceAttachmentPath = _fixture.CreateSourceFile(contents: "recoverable-attachment");
            Guarantee seed = _fixture.CreateGuarantee();

            DeferredFilePromotionException exception = Assert.Throws<DeferredFilePromotionException>(
                () => database.SaveGuarantee(seed, new List<string> { sourceAttachmentPath }));
            Guarantee persisted = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            AttachmentRecord attachment = Assert.Single(persisted.Attachments);

            Assert.Contains("SaveGuarantee", exception.OperationName);
            Assert.False(File.Exists(attachment.FilePath));

            DatabaseService.ResetRuntimeInitializationForTesting();
            DatabaseService.InitializeRuntime();

            Guarantee recovered = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            Assert.True(File.Exists(Assert.Single(recovered.Attachments).FilePath));
        }

        [Fact]
        public void SaveGuarantee_RejectsOversizedAttachments()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            string sourceAttachmentPath = _fixture.CreateArtifactPath(".pdf");
            using (FileStream stream = File.Create(sourceAttachmentPath))
            {
                stream.SetLength(AttachmentStorageService.MaxAttachmentFileSizeBytes + 1);
            }

            Guarantee seed = _fixture.CreateGuarantee();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => database.SaveGuarantee(seed, new List<string> { sourceAttachmentPath }));

            Assert.Contains("25 ميجابايت", exception.Message);
            Assert.Null(database.GetCurrentGuaranteeByNo(seed.GuaranteeNo));
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

        private sealed class DeferringAttachmentStorageService : AttachmentStorageService
        {
            public override void FinalizeStagedCopies(IEnumerable<StagedAttachmentFile> stagedCopies, string operationName)
            {
                throw new DeferredFilePromotionException(
                    operationName,
                    stagedCopies.Select(stagedCopy => stagedCopy.SavedFileName),
                    new IOException("Simulated promotion deferral."));
            }
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
