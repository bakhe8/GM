#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;
using GuaranteeManager.Services;
using Microsoft.Data.Sqlite;

namespace GuaranteeManager.Development
{
    public class DataSeedingService
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

        private readonly string[] _beneficiaries =
        {
            "وزارة المالية - إدارة المشتريات",
            "الهيئة الملكية للجبيل وينبع",
            "وزارة الصحة - المستشفيات الحكومية",
            "شركة أرامكو السعودية",
            "وزارة التعليم - الإدارة العامة للمشاريع",
            "أمانة منطقة الرياض",
            "وزارة النقل والخدمات اللوجستية",
            "الهيئة السعودية للمقاولين"
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
        // نشط — طلب معلق لكل نوع
        // ══════════════════════════════════════════════════════════════════════

        private void SeedActive_PendingVerification()
        {
            var g = BuildGuarantee(7, 80, 600_000m, GuaranteeReferenceType.Contract, "عقد-4421");
            var current = Save(g, attachmentCount: 1);
            _workflowService.CreateVerificationRequest(current.Id, "التحقق الدوري المطلوب من المستفيد", "سارة الزهراني");
        }

        private void SeedActive_PendingExtension()
        {
            var g = BuildGuarantee(0, 20, 2_000_000m, GuaranteeReferenceType.Contract, "عقد-5500");
            var current = Save(g, attachmentCount: 2);
            _workflowService.CreateExtensionRequest(
                current.Id,
                DateTime.Today.AddDays(110),
                "طلب تمديد بسبب تأخر تنفيذ المشروع",
                "فيصل القحطاني");
        }

        private void SeedActive_PendingRelease()
        {
            var g = BuildGuarantee(1, 45, 850_000m, GuaranteeReferenceType.PurchaseOrder, "PO-8830");
            var current = Save(g, attachmentCount: 0);
            _workflowService.CreateReleaseRequest(current.Id, "انتهاء الالتزام التعاقدي", "نورة السبيعي");
        }

        private void SeedActive_PendingReduction()
        {
            var g = BuildGuarantee(2, 70, 1_800_000m, GuaranteeReferenceType.Contract, "عقد-6601");
            var current = Save(g, attachmentCount: 1);
            _workflowService.CreateReductionRequest(
                current.Id,
                900_000m,
                "تخفيض بنسبة 50% بعد إتمام المرحلة الأولى",
                "خالد الدوسري");
        }

        private void SeedActive_PendingReplacement()
        {
            var g = BuildGuarantee(3, 55, 1_600_000m, GuaranteeReferenceType.Contract, "عقد-6750");
            var current = Save(g, attachmentCount: 1);
            string newNo = NextGuaranteeNo();
            _workflowService.CreateReplacementRequest(
                current.Id,
                newNo,
                current.Supplier,
                _banks[(_counter + 2) % _banks.Length],
                current.Amount,
                DateTime.Today.AddDays(200),
                current.GuaranteeType,
                current.Beneficiary,
                GuaranteeReferenceType.Contract,
                "عقد-6750-R",
                "طلب استبدال معلق لتغيير البنك الضامن",
                "هديل الرشيدي");
        }

        // ══════════════════════════════════════════════════════════════════════
        // نشط — طلبات مرفوضة (كل نوع)
        // ══════════════════════════════════════════════════════════════════════

        private void SeedActive_LiquidationRejected()
        {
            var g = BuildGuarantee(4, 40, 700_000m, GuaranteeReferenceType.PurchaseOrder, "PO-7001");
            var current = Save(g, attachmentCount: 0);
            var req = _workflowService.CreateLiquidationRequest(current.Id, "محاولة تسييل قبل انتهاء الضمان", "عمر الفيصل");
            _workflowService.RecordBankResponse(req.Id, RequestStatus.Rejected, "رفض البنك التسييل لعدم وجود تخلف رسمي موثق");
        }

        private void SeedActive_LiquidationCancelled()
        {
            var g = BuildGuarantee(5, 45, 1_100_000m, GuaranteeReferenceType.Contract, "عقد-7100");
            var current = Save(g, attachmentCount: 1);
            var req = _workflowService.CreateLiquidationRequest(current.Id, "طلب تسييل أولي", "سلطان الغامدي");
            _workflowService.RecordBankResponse(req.Id, RequestStatus.Cancelled, "إلغاء طلب التسييل بعد التوصل لتسوية ودية");
        }

        private void SeedActive_ReleaseRejected()
        {
            var g = BuildGuarantee(6, 35, 950_000m, GuaranteeReferenceType.Contract, "عقد-7200");
            var current = Save(g, attachmentCount: 0);
            var req = _workflowService.CreateReleaseRequest(current.Id, "طلب إفراج قبل اكتمال الأعمال", "نادر العمري");
            _workflowService.RecordBankResponse(req.Id, RequestStatus.Rejected, "رفض البنك الإفراج لعدم اكتمال شروط التسليم");
        }

        private void SeedActive_ReductionRejected()
        {
            var g = BuildGuarantee(7, 65, 2_400_000m, GuaranteeReferenceType.PurchaseOrder, "PO-7300");
            var current = Save(g, attachmentCount: 1);
            var req = _workflowService.CreateReductionRequest(current.Id, 800_000m, "تخفيض بعد إنجاز المرحلة الأولى", "ريما الشهري");
            _workflowService.RecordBankResponse(req.Id, RequestStatus.Rejected, "رفض البنك التخفيض لعدم تقديم شهادة إنجاز معتمدة");
        }

        private void SeedActive_ReductionCancelled()
        {
            var g = BuildGuarantee(0, 50, 1_300_000m, GuaranteeReferenceType.Contract, "عقد-7400");
            var current = Save(g, attachmentCount: 0);
            var req = _workflowService.CreateReductionRequest(current.Id, 600_000m, "طلب تخفيض أولي", "وليد السلمي");
            _workflowService.RecordBankResponse(req.Id, RequestStatus.Cancelled, "إلغاء طلب التخفيض بناءً على رغبة المستفيد");
        }

        private void SeedActive_VerificationRejected()
        {
            var g = BuildGuarantee(1, 75, 400_000m, GuaranteeReferenceType.PurchaseOrder, "PO-7500");
            var current = Save(g, attachmentCount: 1);
            var req = _workflowService.CreateVerificationRequest(current.Id, "تحقق دوري من قبل المستفيد", "لينا الحربي");
            _workflowService.RecordBankResponse(req.Id, RequestStatus.Rejected, "رفض البنك التحقق لمشكلة في البيانات المقدمة");
        }

        private void SeedActive_VerificationCancelled()
        {
            var g = BuildGuarantee(2, 40, 550_000m, GuaranteeReferenceType.Contract, "عقد-7600");
            var current = Save(g, attachmentCount: 0);
            var req = _workflowService.CreateVerificationRequest(current.Id, "طلب تحقق روتيني", "أسامة الخالد");
            _workflowService.RecordBankResponse(req.Id, RequestStatus.Cancelled, "إلغاء طلب التحقق بعد تقديم صورة من الضمان مباشرةً");
        }

        private void SeedActive_ReplacementRejected()
        {
            var g = BuildGuarantee(3, 120, 1_750_000m, GuaranteeReferenceType.Contract, "عقد-7700");
            var current = Save(g, attachmentCount: 1);
            string newNo = NextGuaranteeNo();
            var req = _workflowService.CreateReplacementRequest(
                current.Id,
                newNo,
                current.Supplier,
                _banks[(_counter + 3) % _banks.Length],
                current.Amount,
                DateTime.Today.AddDays(180),
                current.GuaranteeType,
                current.Beneficiary,
                GuaranteeReferenceType.Contract,
                "عقد-7700-R",
                "طلب استبدال لتغيير البنك",
                "دانة العتيبي");
            _workflowService.RecordBankResponse(req.Id, RequestStatus.Rejected, "رفض البنك الجديد استلام الضمان لعدم توفر الرصيد اللازم");
        }

        // ══════════════════════════════════════════════════════════════════════
        // نشط — طلبات مكتملة + معلقة
        // ══════════════════════════════════════════════════════════════════════

        private void SeedActive_VerificationExecuted_ExtensionPending()
        {
            var g = BuildGuarantee(4, 18, 700_000m, GuaranteeReferenceType.Contract, "عقد-7733");
            var current = Save(g, attachmentCount: 1);

            var verReq = _workflowService.CreateVerificationRequest(current.Id, "تحقق أول", "عبدالله الشمري");
            _workflowService.RecordBankResponse(verReq.Id, RequestStatus.Executed, "تم التحقق وأُكدت صحة الضمان");

            current = GetCurrent(current)!;
            _workflowService.CreateExtensionRequest(
                current.Id,
                DateTime.Today.AddDays(100),
                "طلب تمديد عقب التحقق",
                "عبدالله الشمري");
        }

        private void SeedActive_ExtensionRejected_VerificationPending()
        {
            var g = BuildGuarantee(5, 30, 400_000m, GuaranteeReferenceType.PurchaseOrder, "PO-9915");
            var current = Save(g, attachmentCount: 0);

            var extReq = _workflowService.CreateExtensionRequest(
                current.Id,
                DateTime.Today.AddDays(120),
                "طلب تمديد أول",
                "ريم المطيري");
            _workflowService.RecordBankResponse(extReq.Id, RequestStatus.Rejected, "رفض البنك التمديد لعدم استيفاء الشروط");

            current = GetCurrent(current)!;
            _workflowService.CreateVerificationRequest(current.Id, "تحقق بعد رفض التمديد", "ريم المطيري");
        }

        private void SeedActive_ReleaseCancelled_VerificationPending()
        {
            var g = BuildGuarantee(6, 55, 1_100_000m, GuaranteeReferenceType.Contract, "عقد-1155");
            var current = Save(g, attachmentCount: 1);

            var relReq = _workflowService.CreateReleaseRequest(current.Id, "طلب إفراج أولي", "تركي العنزي");
            _workflowService.RecordBankResponse(relReq.Id, RequestStatus.Cancelled, "تم إلغاء الطلب بناءً على طلب المستفيد");

            current = GetCurrent(current)!;
            _workflowService.CreateVerificationRequest(current.Id, "تحقق بعد إلغاء الإفراج", "تركي العنزي");
        }

        // ══════════════════════════════════════════════════════════════════════
        // نشط — طلبات معلقة متعددة في آنٍ واحد
        // ══════════════════════════════════════════════════════════════════════

        private void SeedActive_MultiplePending_VerificationAndExtension()
        {
            // يُظهر أن نوعين مختلفين يمكن أن يكونا معلقين في آنٍ واحد
            var g = BuildGuarantee(7, 85, 1_050_000m, GuaranteeReferenceType.Contract, "عقد-8001");
            var current = Save(g, attachmentCount: 1);
            _workflowService.CreateVerificationRequest(current.Id, "تحقق بناءً على طلب المستفيد", "بشرى الحمدان");
            _workflowService.CreateExtensionRequest(
                current.Id,
                DateTime.Today.AddDays(175),
                "تمديد متزامن مع التحقق",
                "بشرى الحمدان");
        }

        private void SeedActive_MultiplePending_ReleaseAndLiquidation()
        {
            // طلب إفراج وتسييل معلقان على نفس الضمان — سيناريو تنافسي حدّي
            var g = BuildGuarantee(0, 35, 1_400_000m, GuaranteeReferenceType.PurchaseOrder, "PO-8100");
            var current = Save(g, attachmentCount: 0);
            _workflowService.CreateReleaseRequest(current.Id, "طلب إفراج من الجهة المستفيدة", "جابر الدوسري");
            _workflowService.CreateLiquidationRequest(current.Id, "طلب تسييل موازٍ بسبب التأخر", "جابر الدوسري");
        }

        private void SeedActive_MultiplePending_SixTypes()
        {
            // جميع أنواع الطلبات الستة معلقة في آنٍ واحد — أقصى حالة حدية
            var g = BuildGuarantee(1, 100, 3_500_000m, GuaranteeReferenceType.Contract, "عقد-8200");
            var current = Save(g, attachmentCount: 2);
            _workflowService.CreateVerificationRequest(current.Id, "تحقق دوري", "الإدارة المالية");
            _workflowService.CreateExtensionRequest(
                current.Id,
                DateTime.Today.AddDays(280),
                "تمديد لمدة عام إضافي",
                "الإدارة المالية");
            _workflowService.CreateReductionRequest(
                current.Id,
                1_500_000m,
                "تخفيض بعد اعتماد المرحلة الأولى",
                "الإدارة المالية");
            _workflowService.CreateReleaseRequest(current.Id, "طلب إفراج مسبق", "الإدارة المالية");
            _workflowService.CreateLiquidationRequest(current.Id, "طلب تسييل احتياطي", "الإدارة المالية");
            string newNo = NextGuaranteeNo();
            _workflowService.CreateReplacementRequest(
                current.Id,
                newNo,
                current.Supplier,
                _banks[(_counter + 1) % _banks.Length],
                current.Amount,
                DateTime.Today.AddDays(365),
                current.GuaranteeType,
                current.Beneficiary,
                GuaranteeReferenceType.Contract,
                "عقد-8200-R",
                "طلب استبدال لتحديث البنك الضامن",
                "الإدارة المالية");
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
        // إسقاط تلقائي عند تنفيذ إفراج/تسييل
        // ══════════════════════════════════════════════════════════════════════

        private void SeedReleased_AutoSuperseded()
        {
            // تمديد + تحقق معلقان، ينفَّذ الإفراج فيُسقطان تلقائياً
            var g = BuildGuarantee(0, 30, 1_200_000m, GuaranteeReferenceType.Contract, "عقد-8300");
            var current = Save(g, attachmentCount: 1);

            _workflowService.CreateExtensionRequest(
                current.Id,
                DateTime.Today.AddDays(130),
                "تمديد سينسحب تلقائياً",
                "أحمد الزيد");
            _workflowService.CreateVerificationRequest(current.Id, "تحقق سينسحب تلقائياً", "أحمد الزيد");

            var relReq = _workflowService.CreateReleaseRequest(
                current.Id,
                "إفراج نهائي — يُسقط جميع الطلبات المعلقة تلقائياً",
                "أحمد الزيد");
            _workflowService.RecordBankResponse(relReq.Id, RequestStatus.Executed, "تم الإفراج");
            // التمديد والتحقق أصبحا Superseded تلقائياً بواسطة WorkflowNewVersionExecutor
        }

        private void SeedLiquidated_AutoSuperseded()
        {
            // تخفيض + تحقق معلقان، ينفَّذ التسييل فيُسقطان تلقائياً
            var g = BuildGuarantee(1, 55, 1_900_000m, GuaranteeReferenceType.PurchaseOrder, "PO-8400");
            var current = Save(g, attachmentCount: 0);

            _workflowService.CreateVerificationRequest(current.Id, "تحقق سيُسقَط بالتسييل", "سعود المالكي");
            _workflowService.CreateReductionRequest(
                current.Id,
                900_000m,
                "تخفيض سيُسقَط بالتسييل",
                "سعود المالكي");

            var liqReq = _workflowService.CreateLiquidationRequest(
                current.Id,
                "تسييل فوري — يُسقط جميع الطلبات المعلقة تلقائياً",
                "سعود المالكي");
            _workflowService.RecordBankResponse(liqReq.Id, RequestStatus.Executed, "تم التسييل");
            // التحقق والتخفيض أصبحا Superseded تلقائياً
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
        // مفرج عنها
        // ══════════════════════════════════════════════════════════════════════

        private void SeedReleased_Simple()
        {
            var g = BuildGuarantee(7, -10, 800_000m, GuaranteeReferenceType.Contract, "عقد-6688");
            var current = Save(g, attachmentCount: 1);

            var relReq = _workflowService.CreateReleaseRequest(current.Id, "انتهاء العقد وتسوية كافة الالتزامات", "أحمد السلمي");
            _workflowService.RecordBankResponse(relReq.Id, RequestStatus.Executed, "تم الإفراج رسمياً");
        }

        // ══════════════════════════════════════════════════════════════════════
        // مسيّلة
        // ══════════════════════════════════════════════════════════════════════

        private void SeedLiquidated_Simple()
        {
            var g = BuildGuarantee(4, 75, 1_600_000m, GuaranteeReferenceType.Contract, "عقد-9922");
            var current = Save(g, attachmentCount: 2);

            var liqReq = _workflowService.CreateLiquidationRequest(current.Id, "تسييل بسبب إخلال المقاول بالتزاماته", "عمر العمري");
            _workflowService.RecordBankResponse(liqReq.Id, RequestStatus.Executed, "تم التسييل وحوّل المبلغ للجهة المستفيدة");
        }

        // ══════════════════════════════════════════════════════════════════════
        // مستبدلة
        // ══════════════════════════════════════════════════════════════════════

        private void SeedReplaced_Simple()
        {
            var g = BuildGuarantee(7, 30, 1_400_000m, GuaranteeReferenceType.Contract, "عقد-1144");
            var current = Save(g, attachmentCount: 1);

            string newNo = NextGuaranteeNo();
            var repReq = _workflowService.CreateReplacementRequest(
                current.Id,
                newNo,
                current.Supplier,
                _banks[2],
                1_400_000m,
                DateTime.Today.AddDays(180),
                current.GuaranteeType,
                current.Beneficiary,
                GuaranteeReferenceType.Contract,
                "عقد-1144-R",
                "استبدال الضمان لتغيير البنك الضامن",
                "جواهر الشهري");
            _workflowService.RecordBankResponse(repReq.Id, RequestStatus.Executed, "تم الاستبدال وأُصدر الضمان الجديد");
        }

        private void SeedReplaced_NewGuaranteeExtended()
        {
            var g = BuildGuarantee(0, 20, 3_000_000m, GuaranteeReferenceType.Contract, "عقد-1255");
            var current = Save(g, attachmentCount: 0);

            string newNo = NextGuaranteeNo();
            var repReq = _workflowService.CreateReplacementRequest(
                current.Id,
                newNo,
                current.Supplier,
                _banks[1],
                3_000_000m,
                DateTime.Today.AddDays(60),
                current.GuaranteeType,
                current.Beneficiary,
                GuaranteeReferenceType.Contract,
                "عقد-1255-R",
                "استبدال وتجديد الضمان",
                "راشد المنصوري");
            _workflowService.RecordBankResponse(repReq.Id, RequestStatus.Executed, "تم استبدال الضمان بنجاح");

            Guarantee? newGuarantee = _databaseService.GetCurrentGuaranteeByNo(newNo);
            if (newGuarantee != null)
            {
                var extReq = _workflowService.CreateExtensionRequest(
                    newGuarantee.Id,
                    DateTime.Today.AddDays(180),
                    "تمديد الضمان البديل",
                    "راشد المنصوري");
                _workflowService.RecordBankResponse(extReq.Id, RequestStatus.Executed, "موافقة التمديد على الضمان البديل");
            }
        }

        private void SeedReplaced_NewGuaranteeHasPendingVerification()
        {
            // G1 مستبدل، والضمان الجديد G2 لديه تحقق معلق
            var g = BuildGuarantee(1, 50, 2_300_000m, GuaranteeReferenceType.PurchaseOrder, "PO-1366");
            var current = Save(g, attachmentCount: 1);

            string newNo = NextGuaranteeNo();
            var repReq = _workflowService.CreateReplacementRequest(
                current.Id,
                newNo,
                current.Supplier,
                _banks[4],
                2_300_000m,
                DateTime.Today.AddDays(180),
                current.GuaranteeType,
                current.Beneficiary,
                GuaranteeReferenceType.PurchaseOrder,
                "PO-1366-R",
                "استبدال الضمان بضمان جديد",
                "نوره المنصور");
            _workflowService.RecordBankResponse(repReq.Id, RequestStatus.Executed, "تم الاستبدال");

            Guarantee? newGuarantee = _databaseService.GetCurrentGuaranteeByNo(newNo);
            if (newGuarantee != null)
            {
                _workflowService.CreateVerificationRequest(
                    newGuarantee.Id,
                    "تحقق أولي من الضمان البديل",
                    "نوره المنصور");
            }
        }

        private void SeedChainReplacement_ThreeGenerations()
        {
            // G1 → Replaced → G2 → Replaced → G3 (نشط) — سلسلة استبدال ثلاثية
            var g1 = BuildGuarantee(2, 40, 1_700_000m, GuaranteeReferenceType.Contract, "عقد-1477");
            var current = Save(g1, attachmentCount: 1);

            // استبدال G1 بـ G2
            string no2 = NextGuaranteeNo();
            var rep1 = _workflowService.CreateReplacementRequest(
                current.Id,
                no2,
                current.Supplier,
                _banks[3],
                1_700_000m,
                DateTime.Today.AddDays(180),
                current.GuaranteeType,
                current.Beneficiary,
                GuaranteeReferenceType.Contract,
                "عقد-1477-R1",
                "استبدال G1 بـ G2 لتغيير البنك",
                "مشعل الدوسري");
            _workflowService.RecordBankResponse(rep1.Id, RequestStatus.Executed, "تم الاستبدال الأول");

            // استبدال G2 بـ G3
            Guarantee? g2 = _databaseService.GetCurrentGuaranteeByNo(no2);
            if (g2 != null)
            {
                string no3 = NextGuaranteeNo();
                var rep2 = _workflowService.CreateReplacementRequest(
                    g2.Id,
                    no3,
                    g2.Supplier,
                    _banks[6],
                    1_700_000m,
                    DateTime.Today.AddDays(365),
                    g2.GuaranteeType,
                    g2.Beneficiary,
                    GuaranteeReferenceType.Contract,
                    "عقد-1477-R2",
                    "استبدال G2 بـ G3 للتجديد السنوي",
                    "مشعل الدوسري");
                _workflowService.RecordBankResponse(rep2.Id, RequestStatus.Executed, "تم الاستبدال الثاني");
                // G3 نشط الآن — G1 وG2 بحالة مستبدل
            }
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
            int beni = _counter % _beneficiaries.Length;

            return new Guarantee
            {
                Supplier = _suppliers[si],
                Bank = _banks[bi],
                GuaranteeNo = NextGuaranteeNo(),
                Amount = amount,
                ExpiryDate = DateTime.Today.AddDays(expiryDaysFromToday),
                GuaranteeType = _guaranteeTypes[ti],
                Beneficiary = _beneficiaries[beni],
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
            using var transaction = connection.BeginTransaction();
            try
            {
                var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM Attachments; DELETE FROM WorkflowRequests; DELETE FROM Guarantees;";
                cmd.ExecuteNonQuery();
                transaction.Commit();
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
