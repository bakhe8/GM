#if DEBUG
using System;
using GuaranteeManager.Models;

namespace GuaranteeManager.Development
{
    public partial class DataSeedingService
    {
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

        private void SeedReleased_Simple()
        {
            var g = BuildGuarantee(7, -10, 800_000m, GuaranteeReferenceType.Contract, "عقد-6688");
            var current = Save(g, attachmentCount: 1);

            var relReq = _workflowService.CreateReleaseRequest(current.Id, "انتهاء العقد وتسوية كافة الالتزامات", "أحمد السلمي");
            _workflowService.RecordBankResponse(relReq.Id, RequestStatus.Executed, "تم الإفراج رسمياً");
        }

        private void SeedLiquidated_Simple()
        {
            var g = BuildGuarantee(4, 75, 1_600_000m, GuaranteeReferenceType.Contract, "عقد-9922");
            var current = Save(g, attachmentCount: 2);

            var liqReq = _workflowService.CreateLiquidationRequest(current.Id, "تسييل بسبب إخلال المقاول بالتزاماته", "عمر العمري");
            _workflowService.RecordBankResponse(liqReq.Id, RequestStatus.Executed, "تم التسييل وحوّل المبلغ للجهة المستفيدة");
        }

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
    }
}
#endif
