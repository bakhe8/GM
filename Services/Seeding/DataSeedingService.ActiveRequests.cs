using System;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services.Seeding
{
    public partial class DataSeedingService
    {
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
                GuaranteeDateCalendar.Gregorian,
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
                GuaranteeDateCalendar.Gregorian,
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
                GuaranteeDateCalendar.Gregorian,
                current.GuaranteeType,
                current.Beneficiary,
                GuaranteeReferenceType.Contract,
                "عقد-8200-R",
                "طلب استبدال لتحديث البنك الضامن",
                "الإدارة المالية");
        }
    }
}
