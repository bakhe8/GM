using System;
using System.Collections.Generic;
using System.IO;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;
using GuaranteeManager.Services;
using Microsoft.Data.Sqlite;

namespace GuaranteeManager.Services.Seeding
{
    public partial class DataSeedingService
    {
        private readonly IDatabaseService _databaseService;
        private readonly IWorkflowService _workflowService;
        private readonly string _connectionString;
        private int _counter;

        private readonly string[] _suppliers =
        {
            "شركة المشاريع الكبرى للمقاولات",
            "مؤسسة الحلول التقنية المتطورة",
            "شركة التوريدات الصناعية المحدودة",
            "مكتب الاستشارات الهندسية الدولي",
            "شركة الخدمات اللوجستية المتكاملة",
            "مجموعة التطور العقاري",
            "شركة الأنظمة الأمنية الحديثة",
            "مؤسسة الريادة للتجارة والمقاولات",
            "شركة البناء والتشييد الحديث",
            "مجموعة الخليج للاستثمار"
        };

        private readonly string[] _banks =
        {
            "مصرف الراجحي",
            "البنك الأهلي السعودي",
            "بنك الرياض",
            "البنك العربي الوطني",
            "مصرف الإنماء",
            "بنك البلاد",
            "بنك الجزيرة",
            "بنك ساب"
        };

        private readonly string[] _guaranteeTypes =
        {
            "ابتدائي",
            "نهائي",
            "دفعة مقدمة",
            "صيانة"
        };

        public DataSeedingService(IDatabaseService databaseService, IWorkflowService workflowService)
        {
            _databaseService = databaseService;
            _workflowService = workflowService;
            _connectionString = $"Data Source={AppPaths.DatabasePath}";
        }

        public void Seed(bool clearExistingData = true)
        {
            try
            {
                HashSet<int> existingGuaranteeIds = clearExistingData
                    ? new HashSet<int>()
                    : LoadExistingIds("Guarantees");
                HashSet<int> existingRequestIds = clearExistingData
                    ? new HashSet<int>()
                    : LoadExistingIds("WorkflowRequests");
                HashSet<int> existingAttachmentIds = clearExistingData
                    ? new HashSet<int>()
                    : LoadExistingIds("Attachments");

                if (clearExistingData)
                {
                    ClearExistingData();
                }

                _counter = 1;

                // ── نشط — بدون طلبات ──────────────────────────────────────────────
                SeedActive_LongExpiry_NoRequests();
                SeedActive_MediumExpiry_NoRequests();
                SeedActive_NoReference();

                // ── نشط — انتهاء وشيك ─────────────────────────────────────────────
                SeedActive_ExpiringSoon_NoRequests();
                SeedActive_VeryUrgent_NoRequests();

                // ── نشط — منتهية تاريخاً ──────────────────────────────────────────
                SeedActive_DateExpired_NoRequests();
                SeedActive_DateExpiredLong_PendingRelease();

                // ── نشط — طلب معلق لكل نوع ────────────────────────────────────────
                SeedActive_PendingVerification();
                SeedActive_PendingExtension();
                SeedActive_PendingRelease();
                SeedActive_PendingReduction();
                SeedActive_PendingReplacement();

                // ── نشط — طلبات مرفوضة (كل نوع) ──────────────────────────────────
                SeedActive_LiquidationRejected();
                SeedActive_LiquidationCancelled();
                SeedActive_ReleaseRejected();
                SeedActive_ReductionRejected();
                SeedActive_ReductionCancelled();
                SeedActive_VerificationRejected();
                SeedActive_VerificationCancelled();
                SeedActive_ReplacementRejected();

                // ── نشط — طلبات مكتملة + معلقة ────────────────────────────────────
                SeedActive_VerificationExecuted_ExtensionPending();
                SeedActive_ExtensionRejected_VerificationPending();
                SeedActive_ReleaseCancelled_VerificationPending();

                // ── نشط — طلبات معلقة متعددة في آنٍ واحد ────────────────────────
                SeedActive_MultiplePending_VerificationAndExtension();
                SeedActive_MultiplePending_ReleaseAndLiquidation();
                SeedActive_MultiplePending_SixTypes();

                // ── متعددة الإصدارات ────────────────────────────────────────────────
                SeedActive_ExtendedOnce();
                SeedActive_ExtendedTwice();
                SeedActive_Reduced();
                SeedActive_Extended_ThenVerificationSuperseded();
                SeedActive_ReductionSuperseded_ByExtension_NewReductionPending();
                SeedActive_ComplexChain_ExtendedThenReduced_PendingVerification();

                // ── إسقاط تلقائي عند تنفيذ إفراج/تسييل ──────────────────────────
                SeedReleased_AutoSuperseded();
                SeedLiquidated_AutoSuperseded();

                // ── حدود تاريخ الانتهاء ────────────────────────────────────────────
                SeedActive_ExpiringToday();
                SeedActive_ExpiringTomorrow();
                SeedActive_ExpiredYesterday();

                // ── مبالغ متطرفة ────────────────────────────────────────────────────
                SeedActive_LargeAmount_WithRequests();
                SeedActive_SmallAmount_WithPendingVerification();

                // ── مفرج عنها ──────────────────────────────────────────────────────
                SeedReleased_Simple();

                // ── مسيّلة ──────────────────────────────────────────────────────────
                SeedLiquidated_Simple();

                // ── مستبدلة ─────────────────────────────────────────────────────────
                SeedReplaced_Simple();
                SeedReplaced_NewGuaranteeExtended();
                SeedReplaced_NewGuaranteeHasPendingVerification();
                SeedChainReplacement_ThreeGenerations();

                // ── مرفقات متعددة ─────────────────────────────────────────────────
                SeedActive_ManyAttachments();

                NormalizeGeneratedTimelines(existingGuaranteeIds, existingRequestIds, existingAttachmentIds);
            }
            catch (Exception ex)
            {
                SimpleLogger.Log($"DataSeeding failed: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // نشط — بدون طلبات
        // ══════════════════════════════════════════════════════════════════════

        private void SeedActive_LongExpiry_NoRequests()
        {
            var g = BuildGuarantee(0, 150, 500_000m, GuaranteeReferenceType.Contract, "عقد-1001");
            Save(g, attachmentCount: 2);
        }

        private void SeedActive_MediumExpiry_NoRequests()
        {
            var g = BuildGuarantee(1, 60, 1_200_000m, GuaranteeReferenceType.PurchaseOrder, "PO-5542");
            Save(g, attachmentCount: 1);
        }

        private void SeedActive_NoReference()
        {
            var g = BuildGuarantee(2, 90, 300_000m, GuaranteeReferenceType.None, "");
            Save(g, attachmentCount: 0);
        }

        // ══════════════════════════════════════════════════════════════════════
        // نشط — انتهاء وشيك
        // ══════════════════════════════════════════════════════════════════════

        private void SeedActive_ExpiringSoon_NoRequests()
        {
            var g = BuildGuarantee(3, 25, 750_000m, GuaranteeReferenceType.Contract, "عقد-2088");
            Save(g, attachmentCount: 1);
        }

        private void SeedActive_VeryUrgent_NoRequests()
        {
            var g = BuildGuarantee(4, 7, 450_000m, GuaranteeReferenceType.PurchaseOrder, "PO-0099");
            Save(g, attachmentCount: 0);
        }

        // ══════════════════════════════════════════════════════════════════════
        // نشط — منتهية تاريخاً
        // ══════════════════════════════════════════════════════════════════════

        private void SeedActive_DateExpired_NoRequests()
        {
            var g = BuildGuarantee(5, -20, 900_000m, GuaranteeReferenceType.Contract, "عقد-3301");
            Save(g, attachmentCount: 1);
        }

        private void SeedActive_DateExpiredLong_PendingRelease()
        {
            var g = BuildGuarantee(6, -60, 1_500_000m, GuaranteeReferenceType.PurchaseOrder, "PO-7712");
            var current = Save(g);
            _workflowService.CreateReleaseRequest(current.Id, "الضمان منتهي ويحتاج إعادة/إفراج للبنك", "محمد العتيبي");
        }

        // ══════════════════════════════════════════════════════════════════════
        // متعددة الإصدارات
        // ══════════════════════════════════════════════════════════════════════

        private void SeedActive_ExtendedOnce()
        {
            var g = BuildGuarantee(2, 15, 950_000m, GuaranteeReferenceType.Contract, "عقد-2200");
            var current = Save(g, attachmentCount: 1);

            var extReq = _workflowService.CreateExtensionRequest(
                current.Id,
                DateTime.Today.AddDays(75),
                "تمديد أول بعد طلب المستفيد",
                "هند الرشيدي");
            _workflowService.RecordBankResponse(extReq.Id, RequestStatus.Executed, "موافقة البنك على التمديد");
        }

        private void SeedActive_ExtendedTwice()
        {
            var g = BuildGuarantee(3, 10, 1_300_000m, GuaranteeReferenceType.PurchaseOrder, "PO-3344");
            var current = Save(g, attachmentCount: 2);

            var ext1 = _workflowService.CreateExtensionRequest(
                current.Id,
                DateTime.Today.AddDays(50),
                "تمديد أول",
                "سلطان الحربي");
            _workflowService.RecordBankResponse(ext1.Id, RequestStatus.Executed, "موافقة التمديد الأول");

            current = GetCurrent(current)!;
            var ext2 = _workflowService.CreateExtensionRequest(
                current.Id,
                DateTime.Today.AddDays(110),
                "تمديد ثانٍ",
                "سلطان الحربي");
            _workflowService.RecordBankResponse(ext2.Id, RequestStatus.Executed, "موافقة التمديد الثاني");
        }

        private void SeedActive_Reduced()
        {
            var g = BuildGuarantee(4, 65, 2_500_000m, GuaranteeReferenceType.Contract, "عقد-4466");
            var current = Save(g, attachmentCount: 1);

            var redReq = _workflowService.CreateReductionRequest(
                current.Id,
                1_250_000m,
                "تخفيض بعد اعتماد أعمال المرحلة الأولى",
                "وليد الغامدي");
            _workflowService.RecordBankResponse(redReq.Id, RequestStatus.Executed, "موافقة البنك على التخفيض");
        }

        private void SeedActive_Extended_ThenVerificationSuperseded()
        {
            // تمديد + تحقق معلقان معاً، ثم يُنفَّذ التمديد فيُسقَط التحقق يدوياً
            var g = BuildGuarantee(5, 12, 600_000m, GuaranteeReferenceType.Contract, "عقد-5577");
            var current = Save(g, attachmentCount: 0);

            var verReq = _workflowService.CreateVerificationRequest(current.Id, "تحقق قبل التمديد", "منى الحمدان");
            var extReq = _workflowService.CreateExtensionRequest(
                current.Id,
                DateTime.Today.AddDays(80),
                "تمديد متزامن مع التحقق",
                "منى الحمدان");

            _workflowService.RecordBankResponse(extReq.Id, RequestStatus.Executed, "موافقة التمديد");
            // التمديد لا يُسقط التحقق تلقائياً، نسجّله يدوياً
            _workflowService.RecordBankResponse(verReq.Id, RequestStatus.Superseded, "أُسقط التحقق بعد تنفيذ التمديد وإنشاء نسخة جديدة");
        }

        private void SeedActive_ReductionSuperseded_ByExtension_NewReductionPending()
        {
            // تخفيض وتمديد معلقان معاً، ينفَّذ التمديد، يُسقَط التخفيض، ثم طلب تخفيض جديد
            var g = BuildGuarantee(6, 90, 2_000_000m, GuaranteeReferenceType.Contract, "عقد-5680");
            var current = Save(g, attachmentCount: 1);

            var redReq = _workflowService.CreateReductionRequest(
                current.Id,
                1_000_000m,
                "تخفيض أولي قبل التمديد",
                "بندر القحطاني");
            var extReq = _workflowService.CreateExtensionRequest(
                current.Id,
                DateTime.Today.AddDays(200),
                "تمديد عاجل متزامن مع التخفيض",
                "بندر القحطاني");

            _workflowService.RecordBankResponse(extReq.Id, RequestStatus.Executed, "موافقة البنك على التمديد");
            // التمديد لم يُسقط التخفيض تلقائياً — نسجّله يدوياً
            _workflowService.RecordBankResponse(redReq.Id, RequestStatus.Superseded, "أُسقط طلب التخفيض بعد تنفيذ التمديد");

            // طلب تخفيض جديد على النسخة الموسَّعة
            current = GetCurrent(current)!;
            _workflowService.CreateReductionRequest(
                current.Id,
                800_000m,
                "تخفيض جديد على الضمان الموسَّع",
                "بندر القحطاني");
        }

        private void SeedActive_ComplexChain_ExtendedThenReduced_PendingVerification()
        {
            // سلسلة: أصلي -> ممتد (الثاني) -> مخفَّض (الثالث) -> تحقق معلق
            var g = BuildGuarantee(7, 60, 4_000_000m, GuaranteeReferenceType.Contract, "عقد-5790");
            var current = Save(g, attachmentCount: 1);

            var ext = _workflowService.CreateExtensionRequest(
                current.Id,
                DateTime.Today.AddDays(150),
                "تمديد المرحلة الأولى",
                "راشد الجهني");
            _workflowService.RecordBankResponse(ext.Id, RequestStatus.Executed, "موافقة التمديد الأول");

            current = GetCurrent(current)!;
            var red = _workflowService.CreateReductionRequest(
                current.Id,
                2_000_000m,
                "تخفيض بعد إنجاز النصف الأول",
                "راشد الجهني");
            _workflowService.RecordBankResponse(red.Id, RequestStatus.Executed, "موافقة التخفيض");

            current = GetCurrent(current)!;
            _workflowService.CreateVerificationRequest(
                current.Id,
                "تحقق نهائي على النسخة المعدَّلة",
                "راشد الجهني");
        }

        // ══════════════════════════════════════════════════════════════════════
        // حدود تاريخ الانتهاء
        // ══════════════════════════════════════════════════════════════════════

        private void SeedActive_ExpiringToday()
        {
            var g = BuildGuarantee(2, 0, 660_000m, GuaranteeReferenceType.Contract, "عقد-8500");
            var current = Save(g, attachmentCount: 1);
            // ينتهي اليوم — يجب أن يظهر ضمن "قريب الانتهاء" في الواجهة
            _workflowService.CreateExtensionRequest(
                current.Id,
                DateTime.Today.AddDays(90),
                "تمديد عاجل — الضمان ينتهي اليوم",
                "مريم الشهري");
        }

        private void SeedActive_ExpiringTomorrow()
        {
            var g = BuildGuarantee(3, 1, 480_000m, GuaranteeReferenceType.PurchaseOrder, "PO-8600");
            Save(g, attachmentCount: 0);
            // ينتهي غداً — حالة حدية بالغة الإلحاح
        }

        private void SeedActive_ExpiredYesterday()
        {
            var g = BuildGuarantee(4, -1, 820_000m, GuaranteeReferenceType.Contract, "عقد-8700");
            var current = Save(g, attachmentCount: 1);
            // انتهى أمس — الإجراء الوحيد المتاح هو الإفراج/إعادة الضمان للبنك.
            _workflowService.CreateReleaseRequest(
                current.Id,
                "إفراج/إعادة ضمان انتهى أمس",
                "عادل القرني");
        }

        // ══════════════════════════════════════════════════════════════════════
        // مبالغ متطرفة
        // ══════════════════════════════════════════════════════════════════════

        private void SeedActive_LargeAmount_WithRequests()
        {
            var g = BuildGuarantee(5, 120, 12_000_000m, GuaranteeReferenceType.Contract, "عقد-9000");
            var current = Save(g, attachmentCount: 2);
            var ext = _workflowService.CreateExtensionRequest(
                current.Id,
                DateTime.Today.AddDays(300),
                "تمديد لضمان بمبلغ كبير",
                "سامي العجمي");
            _workflowService.RecordBankResponse(ext.Id, RequestStatus.Executed, "موافقة البنك على التمديد");

            current = GetCurrent(current)!;
            _workflowService.CreateReductionRequest(
                current.Id,
                6_000_000m,
                "تخفيض نصفي للضمان الكبير",
                "سامي العجمي");
        }

        private void SeedActive_SmallAmount_WithPendingVerification()
        {
            var g = BuildGuarantee(6, 45, 15_000m, GuaranteeReferenceType.PurchaseOrder, "PO-9100");
            var current = Save(g, attachmentCount: 0);
            _workflowService.CreateVerificationRequest(current.Id, "تحقق من ضمان بمبلغ صغير", "نوف الشمري");
        }

        // ══════════════════════════════════════════════════════════════════════
        // مرفقات متعددة
        // ══════════════════════════════════════════════════════════════════════

        private void SeedActive_ManyAttachments()
        {
            var g = BuildGuarantee(3, 100, 1_150_000m, GuaranteeReferenceType.Contract, "عقد-1588");
            var current = Save(g, attachmentCount: 4);
            _workflowService.CreateVerificationRequest(
                current.Id,
                "تحقق من ضمان مرفق به مستندات متعددة",
                "خلود الصواط");
        }

        // ══════════════════════════════════════════════════════════════════════
        // مساعدات
        // ══════════════════════════════════════════════════════════════════════

        private Guarantee BuildGuarantee(int supplierIndex, int expiryDaysFromToday, decimal amount,
            GuaranteeReferenceType refType, string refNumber)
        {
            int si = supplierIndex % _suppliers.Length;
            int bi = _counter % _banks.Length;
            int ti = _counter % _guaranteeTypes.Length;

            return new Guarantee
            {
                Supplier = _suppliers[si],
                Bank = _banks[bi],
                GuaranteeNo = NextGuaranteeNo(),
                Amount = amount,
                ExpiryDate = DateTime.Today.AddDays(expiryDaysFromToday),
                GuaranteeType = _guaranteeTypes[ti],
                Beneficiary = BusinessPartyDefaults.DefaultBeneficiaryName,
                ReferenceType = refType,
                ReferenceNumber = refNumber,
                LifecycleStatus = GuaranteeLifecycleStatus.Active,
                VersionNumber = 1,
                IsCurrent = true
            };
        }

        private string NextGuaranteeNo()
        {
            while (true)
            {
                string guaranteeNo = $"BG-{DateTime.Now.Year}-{_counter++:D4}";
                if (_databaseService.IsGuaranteeNoUnique(guaranteeNo))
                {
                    return guaranteeNo;
                }
            }
        }

        private Guarantee Save(Guarantee g, int attachmentCount = 0)
        {
            List<AttachmentInput> files = CreateDummyFiles(attachmentCount);
            _databaseService.SaveGuaranteeWithAttachments(g, files);
            return _databaseService.GetCurrentGuaranteeByNo(g.GuaranteeNo)
                ?? throw new InvalidOperationException($"فشل حفظ الضمان {g.GuaranteeNo}");
        }

        private Guarantee? GetCurrent(Guarantee g)
        {
            int rootId = g.RootId ?? g.Id;
            return _databaseService.GetCurrentGuaranteeByRootId(rootId);
        }

        private List<AttachmentInput> CreateDummyFiles(int count)
        {
            var attachments = new List<AttachmentInput>();
            if (count == 0) return attachments;

            string tempDir = Path.Combine(Path.GetTempPath(), "GSeed");
            Directory.CreateDirectory(tempDir);

            string[] names = { "صورة_الضمان", "خطاب_التعميد", "مرفق_فني", "مستند_بنكي", "شهادة_الإنجاز" };
            string[] exts = { ".pdf", ".jpg", ".png" };
            AttachmentDocumentType[] documentTypes =
            [
                AttachmentDocumentType.GuaranteeImage,
                AttachmentDocumentType.RequestLetter,
                AttachmentDocumentType.SupportingDocument,
                AttachmentDocumentType.BankResponse,
                AttachmentDocumentType.Other
            ];
            var rnd = new Random();

            for (int i = 0; i < count; i++)
            {
                string uid = Guid.NewGuid().ToString("N")[..6];
                string fileName = $"{names[rnd.Next(names.Length)]}_{uid}{exts[rnd.Next(exts.Length)]}";
                string fullPath = Path.Combine(tempDir, fileName);
                File.WriteAllText(fullPath, $"محتوى تجريبي للملف: {fileName}");
                attachments.Add(new AttachmentInput(fullPath, documentTypes[i % documentTypes.Length]));
            }
            return attachments;
        }

        private void ClearExistingData()
        {
            using var connection = SqliteConnectionFactory.Open(_connectionString);
            GuaranteeEventStore.EnsureSchema(connection);
            using var transaction = connection.BeginTransaction();
            try
            {
                var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    DROP TRIGGER IF EXISTS trg_guarantee_events_no_update;
                    DROP TRIGGER IF EXISTS trg_guarantee_events_no_delete;
                    DROP TRIGGER IF EXISTS trg_guarantees_no_hard_delete;
                    DROP TRIGGER IF EXISTS trg_attachments_no_hard_delete;
                    DELETE FROM GuaranteeEvents;
                    DELETE FROM Attachments;
                    DELETE FROM WorkflowRequests;
                    DELETE FROM Guarantees;";
                cmd.ExecuteNonQuery();
                transaction.Commit();
                GuaranteeEventStore.EnsureSchema(connection);
                GuaranteeSchemaManager.EnsureCurrentGuaranteeIntegrity(connection);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private HashSet<int> LoadExistingIds(string tableName)
        {
            string safeTableName = tableName switch
            {
                "Guarantees" => "Guarantees",
                "WorkflowRequests" => "WorkflowRequests",
                "Attachments" => "Attachments",
                _ => throw new ArgumentOutOfRangeException(nameof(tableName), tableName, "Unsupported seed table.")
            };

            var ids = new HashSet<int>();
            using var connection = SqliteConnectionFactory.Open(_connectionString);
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT Id FROM {safeTableName}";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                ids.Add(reader.GetInt32(0));
            }

            return ids;
        }
    }
}
