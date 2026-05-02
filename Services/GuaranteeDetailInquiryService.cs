using System;
using System.Collections.Generic;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;

namespace GuaranteeManager.Services
{
    internal sealed class GuaranteeDetailInquiryService
    {
        private readonly IDatabaseService _databaseService;

        public GuaranteeDetailInquiryService(IDatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public OperationalInquiryResult GetLastEventForGuarantee(int guaranteeId)
        {
            InquiryContext context = LoadContext(guaranteeId);
            Guarantee selectedGuarantee = context.SelectedGuarantee;
            Guarantee currentGuarantee = context.CurrentGuarantee;
            List<Guarantee> history = context.History;
            List<WorkflowRequest> requests = context.Requests;

            Guarantee latestVersion = history.FirstOrDefault() ?? currentGuarantee;
            WorkflowRequest? latestResponse = requests
                .Where(r => r.ResponseRecordedAt.HasValue)
                .OrderByDescending(r => r.ResponseRecordedAt!.Value)
                .ThenByDescending(r => r.SequenceNumber)
                .FirstOrDefault();
            WorkflowRequest? latestCreatedRequest = requests
                .OrderByDescending(r => r.RequestDate)
                .ThenByDescending(r => r.SequenceNumber)
                .FirstOrDefault();

            Guarantee? resultGuarantee = latestResponse?.ResultVersionId is int resultVersionId
                ? _databaseService.GetGuaranteeById(resultVersionId)
                : null;

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = $"last-event:{context.RootId}",
                Title = "ما آخر ما حدث لهذا الضمان؟",
                Subject = $"الضمان رقم {currentGuarantee.GuaranteeNo}",
                SelectedGuarantee = selectedGuarantee,
                CurrentGuarantee = currentGuarantee
            };

            DateTime latestVersionDate = latestVersion.CreatedAt;
            DateTime latestRequestDate = latestCreatedRequest?.RequestDate ?? DateTime.MinValue;
            DateTime latestResponseDate = latestResponse?.ResponseRecordedAt ?? DateTime.MinValue;

            if (latestResponse != null && latestResponseDate >= latestVersionDate && latestResponseDate >= latestRequestDate)
            {
                BuildResponseResult(result, currentGuarantee, latestResponse, resultGuarantee);
            }
            else if (latestCreatedRequest != null && latestRequestDate > latestVersionDate)
            {
                BuildPendingRequestResult(result, currentGuarantee, latestCreatedRequest);
            }
            else
            {
                BuildVersionResult(result, currentGuarantee, latestVersion, requests.Any());
            }

            AddFacts(result, currentGuarantee, requests, history, latestCreatedRequest, latestResponse);
            AddTimeline(result, history, requests);
            return result;
        }

        public OperationalInquiryResult GetExtensionTimingForGuarantee(int guaranteeId)
        {
            InquiryContext context = LoadContext(guaranteeId);
            Guarantee currentGuarantee = context.CurrentGuarantee;

            WorkflowRequest? latestExtensionRequest = context.Requests
                .Where(r => r.Type == RequestType.Extension)
                .OrderByDescending(r => r.RequestDate)
                .ThenByDescending(r => r.SequenceNumber)
                .FirstOrDefault();

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = $"extension-timing:{context.RootId}",
                Title = "هل طلبنا تمديد هذا الضمان قبل أن ينتهي؟",
                Subject = $"الضمان رقم {currentGuarantee.GuaranteeNo}",
                SelectedGuarantee = context.SelectedGuarantee,
                CurrentGuarantee = currentGuarantee
            };

            if (latestExtensionRequest == null)
            {
                result.Answer = "لا يوجد طلب تمديد مسجل لهذا الضمان حتى الآن.";
                result.Explanation = "لم يتم العثور على أي طلب من نوع تمديد ضمن سلسلة هذا الضمان، لذلك لا يمكن إثبات أن طلب التمديد أُرسل قبل الانتهاء أو بعده.";
                result.EventDate = currentGuarantee.ExpiryDate;
                AddFacts(result, currentGuarantee, context.Requests, context.History, null, null);
                result.Facts.Add(new OperationalInquiryFact { Label = "تاريخ الانتهاء الحالي", Value = DualCalendarDateService.FormatDualDate(currentGuarantee.ExpiryDate) });
                AddTimeline(result, context.History, context.Requests);
                return result;
            }

            bool requestedBeforeExpiry = latestExtensionRequest.RequestDate.Date < currentGuarantee.ExpiryDate.Date;
            int daysBefore = (currentGuarantee.ExpiryDate.Date - latestExtensionRequest.RequestDate.Date).Days;

            result.RelatedRequest = latestExtensionRequest;
            result.EventDate = latestExtensionRequest.RequestDate;

            result.Answer = requestedBeforeExpiry
                ? $"نعم، تم إنشاء طلب التمديد بتاريخ {DualCalendarDateService.FormatGregorianDate(latestExtensionRequest.RequestDate)}، أي قبل {daysBefore} يوم/أيام من تاريخ الانتهاء {DualCalendarDateService.FormatDualDate(currentGuarantee.ExpiryDate)}."
                : $"لا، تم إنشاء طلب التمديد بتاريخ {DualCalendarDateService.FormatGregorianDate(latestExtensionRequest.RequestDate)}، وهو بعد أو في نفس يوم تاريخ الانتهاء {DualCalendarDateService.FormatDualDate(currentGuarantee.ExpiryDate)}.";
            result.Explanation = requestedBeforeExpiry
                ? "هذا يعني أن المتابعة كانت استباقية وأن الطلب أُرسل للبنك قبل انقضاء صلاحية الضمان."
                : "هذا يعني أن طلب التمديد جاء متأخرًا بعد أن انتهت صلاحية الضمان الرسمية.";

            AddFacts(result, currentGuarantee, context.Requests, context.History, latestExtensionRequest, null);
            result.Facts.Add(new OperationalInquiryFact { Label = "تاريخ الانتهاء الحالي", Value = DualCalendarDateService.FormatDualDate(currentGuarantee.ExpiryDate) });
            result.Facts.Add(new OperationalInquiryFact { Label = "تاريخ آخر طلب تمديد", Value = DualCalendarDateService.FormatGregorianDate(latestExtensionRequest.RequestDate) });
            result.Facts.Add(new OperationalInquiryFact { Label = "مقدم قبل الانتهاء؟", Value = requestedBeforeExpiry ? "نعم" : "لا" });
            result.Facts.Add(new OperationalInquiryFact
            {
                Label = "أيام الفارق",
                Value = requestedBeforeExpiry ? $"{daysBefore} يوم/أيام قبل" : $"{-daysBefore} يوم/أيام بعد"
            });
            AddTimeline(result, context.History, context.Requests);
            return result;
        }

        public OperationalInquiryResult GetOutstandingReasonForGuarantee(int guaranteeId, RequestType requestType)
        {
            InquiryContext context = LoadContext(guaranteeId);
            Guarantee currentGuarantee = context.CurrentGuarantee;

            WorkflowRequest? latestMatchingRequest = context.Requests
                .Where(r => r.Type == requestType)
                .OrderByDescending(GetRelevantRequestDate)
                .ThenByDescending(r => r.SequenceNumber)
                .FirstOrDefault();

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = $"outstanding-{requestType}:{context.RootId}",
                Title = $"لماذا ما زال طلب {latestMatchingRequest?.TypeLabel ?? requestType.ToString()} قيد الانتظار؟",
                Subject = $"الضمان رقم {currentGuarantee.GuaranteeNo}",
                SelectedGuarantee = context.SelectedGuarantee,
                CurrentGuarantee = currentGuarantee
            };

            if (latestMatchingRequest == null)
            {
                result.Answer = $"لا يوجد طلب من نوع {requestType} مسجل لهذا الضمان حتى الآن.";
                result.Explanation = "لذلك لا ينطبق تساؤل الانتظار على هذا الضمان لهذا النوع من الطلبات.";
                result.EventDate = currentGuarantee.ExpiryDate;
                AddFacts(result, currentGuarantee, context.Requests, context.History, null, null);
                AddTimeline(result, context.History, context.Requests);
                return result;
            }

            result.RelatedRequest = latestMatchingRequest;
            result.EventDate = GetRelevantRequestDate(latestMatchingRequest);

            result.Answer = latestMatchingRequest.Status switch
            {
                RequestStatus.Pending =>
                    $"طلب {latestMatchingRequest.TypeLabel} ما زال قيد الانتظار منذ {DualCalendarDateService.FormatGregorianDate(latestMatchingRequest.RequestDate)}، ولم تُسجل استجابة بنك عليه حتى الآن.",
                RequestStatus.Executed =>
                    $"آخر طلب {latestMatchingRequest.TypeLabel} لهذا الضمان لم يعد قيد الانتظار؛ تم تنفيذه وتسجيل استجابة البنك بتاريخ {FormatOptionalDate(latestMatchingRequest.ResponseRecordedAt)}.",
                RequestStatus.Rejected =>
                    $"آخر طلب {latestMatchingRequest.TypeLabel} رُفض عند تسجيل استجابة البنك بتاريخ {FormatOptionalDate(latestMatchingRequest.ResponseRecordedAt)}.",
                RequestStatus.Cancelled =>
                    $"آخر طلب {latestMatchingRequest.TypeLabel} أُلغي ولن يُسجل له رد من البنك.",
                RequestStatus.Superseded =>
                    $"آخر طلب {latestMatchingRequest.TypeLabel} أُسقط آليًا نتيجة تغيير في المسار.",
                _ =>
                    $"آخر طلب {latestMatchingRequest.TypeLabel} حالته {latestMatchingRequest.StatusLabel}."
            };

            result.Explanation = latestMatchingRequest.Status switch
            {
                RequestStatus.Pending => "طالما لم تُسجل استجابة البنك، يبقى الطلب مفتوحًا وتُحتسب الضمانة في سلة الانتظار.",
                RequestStatus.Executed => "يمكن الاطلاع على نتيجة التنفيذ من نافذة التفاصيل.",
                RequestStatus.Rejected => "البنك لم يعتمد الطلب. يلزم إنشاء طلب جديد إذا استدعى الأمر إعادة المحاولة.",
                RequestStatus.Cancelled => "الإلغاء إداري من الجهة المُعِدة. يلزم إنشاء طلب جديد إذا استدعى الأمر المتابعة.",
                RequestStatus.Superseded => "تغير المسار التشغيلي أو نُفذ طلب أحدث جعل هذا الطلب غير صالح.",
                _ => "يجب مراجعة سجل الطلب والسلسلة للتأكد من الحالة الراهنة."
            };

            AddFacts(result, currentGuarantee, context.Requests, context.History,
                latestMatchingRequest, latestMatchingRequest.Status != RequestStatus.Pending ? latestMatchingRequest : null);
            result.Facts.Add(new OperationalInquiryFact { Label = "تاريخ الطلب", Value = DualCalendarDateService.FormatGregorianDate(latestMatchingRequest.RequestDate) });
            result.Facts.Add(new OperationalInquiryFact
            {
                Label = "مدة الانتظار/الإغلاق",
                Value = latestMatchingRequest.Status == RequestStatus.Pending
                    ? $"{Math.Max(0, (DateTime.Now.Date - latestMatchingRequest.RequestDate.Date).Days)} يوم/أيام"
                    : FormatOptionalDate(latestMatchingRequest.ResponseRecordedAt)
            });
            AddTimeline(result, context.History, context.Requests);
            return result;
        }

        public OperationalInquiryResult GetExpiredWithoutExtensionReasonForGuarantee(int guaranteeId)
        {
            InquiryContext context = LoadContext(guaranteeId);
            Guarantee currentGuarantee = context.CurrentGuarantee;

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = $"expired-needing-release:{context.RootId}",
                Title = "ما وضع الضمان بعد انتهاء صلاحيته؟",
                Subject = $"الضمان رقم {currentGuarantee.GuaranteeNo}",
                SelectedGuarantee = context.SelectedGuarantee,
                CurrentGuarantee = currentGuarantee
            };

            if (!currentGuarantee.IsExpired)
            {
                result.EventDate = currentGuarantee.ExpiryDate;
                result.Answer = $"الضمان غير منتهٍ زمنيًا حاليًا، فتاريخ الانتهاء الحالي هو {DualCalendarDateService.FormatDualDate(currentGuarantee.ExpiryDate)}.";
                result.Explanation = "لذلك لا ينطبق عليه سيناريو ما بعد انتهاء الصلاحية.";
                AddFacts(result, currentGuarantee, context.Requests, context.History, null, null);
                AddTimeline(result, context.History, context.Requests);
                return result;
            }

            if (currentGuarantee.LifecycleStatus != GuaranteeLifecycleStatus.Active)
            {
                result.EventDate = currentGuarantee.ExpiryDate;
                result.Answer = $"الضمان منتهٍ زمنيًا، وحالته التشغيلية الحالية هي {currentGuarantee.LifecycleStatusLabel}.";
                result.Explanation = "إذا كانت الدورة مغلقة بالإفراج أو التسييل أو الاستبدال فلا يحتاج الضمان إجراءً جديدًا.";
                AddFacts(result, currentGuarantee, context.Requests, context.History, null, null);
                AddTimeline(result, context.History, context.Requests);
                return result;
            }

            WorkflowRequest? latestRelease = context.Requests
                .Where(r => r.Type == RequestType.Release)
                .OrderByDescending(GetRelevantRequestDate)
                .ThenByDescending(r => r.SequenceNumber)
                .FirstOrDefault();

            result.RelatedRequest = latestRelease;
            result.EventDate = latestRelease?.ResponseRecordedAt ?? latestRelease?.RequestDate ?? currentGuarantee.ExpiryDate;

            if (latestRelease == null)
            {
                result.Answer = $"الضمان منتهٍ زمنيًا منذ {DualCalendarDateService.FormatDualDate(currentGuarantee.ExpiryDate)} ولا يوجد طلب إفراج/إعادة مسجل له.";
                result.Explanation = "بعد انتهاء الصلاحية لا يُنشأ تمديد أو تسييل؛ المسار العملي هو توثيق الإفراج أو إعادة الضمان للبنك.";
            }
            else
            {
                result.Answer = latestRelease.Status switch
                {
                    RequestStatus.Pending =>
                        $"الضمان منتهٍ زمنيًا، وطلب الإفراج/الإعادة ما زال قيد الانتظار منذ {DualCalendarDateService.FormatGregorianDate(latestRelease.RequestDate)} بدون تسجيل رد بنك.",
                    RequestStatus.Rejected =>
                        $"الضمان منتهٍ زمنيًا، وآخر طلب إفراج رُفض عند تسجيل استجابة البنك بتاريخ {FormatOptionalDate(latestRelease.ResponseRecordedAt)}.",
                    RequestStatus.Cancelled =>
                        "الضمان منتهٍ زمنيًا، وآخر طلب إفراج أُلغي قبل التنفيذ.",
                    RequestStatus.Superseded =>
                        "الضمان منتهٍ زمنيًا، وآخر طلب إفراج أُسقط آليًا نتيجة تنفيذ مسار أقوى.",
                    RequestStatus.Executed =>
                        "يوجد طلب إفراج منفذ في السجل، ويجب أن تكون دورة حياة الضمان مغلقة بالإفراج.",
                    _ =>
                        $"الضمان منتهٍ زمنيًا وآخر طلب إفراج حالته {latestRelease.StatusLabel}."
                };

                result.Explanation = latestRelease.Status switch
                {
                    RequestStatus.Pending => "السبب المباشر هو وجود طلب إفراج/إعادة مفتوح دون استجابة بنك مسجلة حتى الآن.",
                    RequestStatus.Rejected => "البنك لم يعتمد الإفراج المطلوب؛ يلزم مراجعة المستندات أو إنشاء طلب جديد عند الحاجة.",
                    RequestStatus.Cancelled => "السبب المباشر هو إغلاق الطلب إداريًا قبل التنفيذ.",
                    RequestStatus.Superseded => "النظام سجل مسارًا أحدث جعل طلب الإفراج السابق غير صالح للتنفيذ.",
                    RequestStatus.Executed => "تنفيذ الإفراج ينهي دورة حياة الضمان ولا ينتج إصدار ضمان جديدًا.",
                    _ => "يجب مراجعة الطلب والسجل الكامل لتحديد السبب النهائي."
                };
            }

            AddFacts(result, currentGuarantee, context.Requests, context.History, latestRelease, latestRelease?.Status == RequestStatus.Pending ? null : latestRelease);
            result.Facts.Add(new OperationalInquiryFact { Label = "تاريخ الانتهاء الحالي", Value = DualCalendarDateService.FormatDualDate(currentGuarantee.ExpiryDate) });
            result.Facts.Add(new OperationalInquiryFact { Label = "أيام منذ الانتهاء", Value = Math.Max(0, (DateTime.Now.Date - currentGuarantee.ExpiryDate.Date).Days).ToString("N0") });
            AddTimeline(result, context.History, context.Requests);
            return result;
        }

        public OperationalInquiryResult GetReleaseEvidenceForGuarantee(int guaranteeId)
        {
            InquiryContext context = LoadContext(guaranteeId);
            Guarantee currentGuarantee = context.CurrentGuarantee;

            WorkflowRequest? executedRelease = context.Requests
                .Where(r => r.Type == RequestType.Release && r.Status == RequestStatus.Executed)
                .OrderByDescending(GetRelevantRequestDate)
                .ThenByDescending(r => r.SequenceNumber)
                .FirstOrDefault();

            Guarantee? resultGuarantee = executedRelease?.ResultVersionId is int resultVersionId
                ? _databaseService.GetGuaranteeById(resultVersionId)
                : null;

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = $"release-evidence:{context.RootId}",
                Title = "لماذا تم الإفراج عن هذا الضمان وعلى أي مستند؟",
                Subject = $"الضمان رقم {currentGuarantee.GuaranteeNo}",
                SelectedGuarantee = context.SelectedGuarantee,
                CurrentGuarantee = currentGuarantee,
                RelatedRequest = executedRelease,
                ResultGuarantee = resultGuarantee,
                EventDate = executedRelease?.ResponseRecordedAt ?? executedRelease?.RequestDate
            };

            if (executedRelease == null)
            {
                result.Answer = currentGuarantee.LifecycleStatus == GuaranteeLifecycleStatus.Released
                    ? "الحالة التشغيلية الحالية تظهر أن الضمان مفرج عنه، لكن لم يُعثر على طلب إفراج منفذ محفوظ ضمن السلسلة الحالية."
                    : "لا يوجد طلب إفراج منفذ محفوظ لهذا الضمان حتى الآن.";
                result.Explanation = currentGuarantee.LifecycleStatus == GuaranteeLifecycleStatus.Released
                    ? "هذه الحالة تحتاج مراجعة تاريخ الضمان أو السجلات القديمة للتأكد من أساس الإفراج."
                    : "لذلك لا يوجد أساس مستندي أو تشغيلي محفوظ يمكن الرجوع إليه بخصوص الإفراج.";

                AddFacts(result, currentGuarantee, context.Requests, context.History, null, null);
                AddTimeline(result, context.History, context.Requests);
                return result;
            }

            string responseDate = FormatOptionalDate(executedRelease.ResponseRecordedAt);
            result.Answer =
                $"تم الإفراج عن الضمان بناءً على طلب إفراج منفذ رقم {executedRelease.SequenceNumber}، وسُجلت استجابة البنك بتاريخ {responseDate}.";
            result.Explanation = executedRelease.HasResponseDocument
                ? "يوجد مستند رد بنك محفوظ على الطلب، ويمكن استخدامه مع خطاب الطلب وسجل النتيجة كمرجع إثبات للإفراج."
                : "لا يوجد مستند رد بنك محفوظ على الطلب، لذلك يبقى إثبات الإفراج أضعف من الحالة المثالية رغم أن التنفيذ مسجل تشغيليًا.";

            AddFacts(result, currentGuarantee, context.Requests, context.History, executedRelease, executedRelease);
            result.Facts.Add(new OperationalInquiryFact { Label = "رقم طلب الإفراج", Value = executedRelease.SequenceNumber.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "تاريخ الاستجابة", Value = responseDate });
            result.Facts.Add(new OperationalInquiryFact { Label = "خطاب الطلب", Value = executedRelease.HasLetter ? "موجود" : "غير متاح" });
            result.Facts.Add(new OperationalInquiryFact { Label = "مستند رد البنك", Value = executedRelease.HasResponseDocument ? "موجود" : "غير متاح" });
            result.Facts.Add(new OperationalInquiryFact
            {
                Label = "ملاحظات التنفيذ",
                Value = string.IsNullOrWhiteSpace(executedRelease.ResponseNotes) ? "---" : executedRelease.ResponseNotes
            });
            if (resultGuarantee != null)
            {
                result.Facts.Add(new OperationalInquiryFact { Label = "سجل الإنهاء المحفوظ", Value = resultGuarantee.VersionLabel });
                result.Facts.Add(new OperationalInquiryFact { Label = "حالة الإنهاء", Value = resultGuarantee.LifecycleStatusLabel });
            }

            AddTimeline(result, context.History, context.Requests);
            return result;
        }

        public OperationalInquiryResult GetLiquidationEvidenceForGuarantee(int guaranteeId)
        {
            InquiryContext context = LoadContext(guaranteeId);
            Guarantee currentGuarantee = context.CurrentGuarantee;

            WorkflowRequest? executedLiquidation = context.Requests
                .Where(r => r.Type == RequestType.Liquidation && r.Status == RequestStatus.Executed)
                .OrderByDescending(GetRelevantRequestDate)
                .ThenByDescending(r => r.SequenceNumber)
                .FirstOrDefault();

            Guarantee? resultGuarantee = executedLiquidation?.ResultVersionId is int resultVersionId
                ? _databaseService.GetGuaranteeById(resultVersionId)
                : null;

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = $"liquidation-evidence:{context.RootId}",
                Title = "لماذا تم تسييل هذا الضمان وعلى أي مستند؟",
                Subject = $"الضمان رقم {currentGuarantee.GuaranteeNo}",
                SelectedGuarantee = context.SelectedGuarantee,
                CurrentGuarantee = currentGuarantee,
                RelatedRequest = executedLiquidation,
                ResultGuarantee = resultGuarantee,
                EventDate = executedLiquidation?.ResponseRecordedAt ?? executedLiquidation?.RequestDate
            };

            if (executedLiquidation == null)
            {
                result.Answer = currentGuarantee.LifecycleStatus == GuaranteeLifecycleStatus.Liquidated
                    ? "الحالة التشغيلية الحالية تظهر أن الضمان مُسيّل، لكن لم يُعثر على طلب تسييل منفذ محفوظ ضمن السلسلة الحالية."
                    : "لا يوجد طلب تسييل منفذ محفوظ لهذا الضمان حتى الآن.";
                result.Explanation = currentGuarantee.LifecycleStatus == GuaranteeLifecycleStatus.Liquidated
                    ? "هذه الحالة تحتاج مراجعة تاريخ الضمان أو السجلات القديمة للتأكد من أساس التسييل."
                    : "لذلك لا يوجد أساس مستندي أو تشغيلي محفوظ يمكن الرجوع إليه بخصوص التسييل.";

                AddFacts(result, currentGuarantee, context.Requests, context.History, null, null);
                AddTimeline(result, context.History, context.Requests);
                return result;
            }

            string responseDate = FormatOptionalDate(executedLiquidation.ResponseRecordedAt);
            result.Answer =
                $"تم تسييل الضمان بناءً على طلب تسييل منفذ رقم {executedLiquidation.SequenceNumber}، وسُجلت استجابة البنك بتاريخ {responseDate}.";
            result.Explanation = executedLiquidation.HasResponseDocument
                ? "يوجد مستند رد بنك محفوظ على الطلب، ويمكن استخدامه مع خطاب الطلب وسجل النتيجة كمرجع إثبات للتسييل."
                : "لا يوجد مستند رد بنك محفوظ على الطلب، لذلك يبقى إثبات التسييل أضعف من الحالة المثالية رغم أن التنفيذ مسجل تشغيليًا.";

            AddFacts(result, currentGuarantee, context.Requests, context.History, executedLiquidation, executedLiquidation);
            result.Facts.Add(new OperationalInquiryFact { Label = "رقم طلب التسييل", Value = executedLiquidation.SequenceNumber.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "تاريخ الاستجابة", Value = responseDate });
            result.Facts.Add(new OperationalInquiryFact { Label = "خطاب الطلب", Value = executedLiquidation.HasLetter ? "موجود" : "غير متاح" });
            result.Facts.Add(new OperationalInquiryFact { Label = "مستند رد البنك", Value = executedLiquidation.HasResponseDocument ? "موجود" : "غير متاح" });
            result.Facts.Add(new OperationalInquiryFact
            {
                Label = "ملاحظات التنفيذ",
                Value = string.IsNullOrWhiteSpace(executedLiquidation.ResponseNotes) ? "---" : executedLiquidation.ResponseNotes
            });
            if (resultGuarantee != null)
            {
                result.Facts.Add(new OperationalInquiryFact { Label = "سجل الإنهاء المحفوظ", Value = resultGuarantee.VersionLabel });
                result.Facts.Add(new OperationalInquiryFact { Label = "حالة الإنهاء", Value = resultGuarantee.LifecycleStatusLabel });
            }

            AddTimeline(result, context.History, context.Requests);
            return result;
        }

        public OperationalInquiryResult GetReductionSourceForGuarantee(int guaranteeId)
        {
            InquiryContext context = LoadContext(guaranteeId);
            Guarantee currentGuarantee = context.CurrentGuarantee;

            WorkflowRequest? matchingReduction = context.Requests
                .Where(r => r.Type == RequestType.Reduction && r.Status == RequestStatus.Executed)
                .OrderByDescending(GetRelevantRequestDate)
                .ThenByDescending(r => r.SequenceNumber)
                .FirstOrDefault(r =>
                {
                    if (r.ResultVersionId is not int resultVersionId)
                    {
                        return false;
                    }

                    Guarantee? resultVersion = _databaseService.GetGuaranteeById(resultVersionId);
                    return resultVersion != null && resultVersion.Amount == currentGuarantee.Amount;
                });

            matchingReduction ??= context.Requests
                .Where(r => r.Type == RequestType.Reduction && r.Status == RequestStatus.Executed)
                .OrderByDescending(GetRelevantRequestDate)
                .ThenByDescending(r => r.SequenceNumber)
                .FirstOrDefault();

            Guarantee? resultGuarantee = matchingReduction?.ResultVersionId is int reductionResultVersionId
                ? _databaseService.GetGuaranteeById(reductionResultVersionId)
                : null;
            Guarantee? baseGuarantee = matchingReduction == null
                ? null
                : _databaseService.GetGuaranteeById(matchingReduction.BaseVersionId);

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = $"reduction-source:{context.RootId}",
                Title = "أين طلب التخفيض الذي نتج عنه هذا الأثر؟",
                Subject = $"الضمان رقم {currentGuarantee.GuaranteeNo}",
                SelectedGuarantee = context.SelectedGuarantee,
                CurrentGuarantee = currentGuarantee,
                RelatedRequest = matchingReduction,
                ResultGuarantee = resultGuarantee,
                EventDate = matchingReduction?.ResponseRecordedAt ?? matchingReduction?.RequestDate
            };

            if (matchingReduction == null)
            {
                result.Answer = "لا يوجد طلب تخفيض منفذ محفوظ لهذا الضمان حتى الآن.";
                result.Explanation = "لذلك لا يوجد طلب يمكن ربط الأثر المالي الحالي به من داخل السلسلة الحالية.";

                AddFacts(result, currentGuarantee, context.Requests, context.History, null, null);
                AddTimeline(result, context.History, context.Requests);
                return result;
            }

            string responseDate = FormatOptionalDate(matchingReduction.ResponseRecordedAt);
            decimal beforeAmount = baseGuarantee?.Amount ?? currentGuarantee.Amount;
            decimal afterAmount = resultGuarantee?.Amount ?? currentGuarantee.Amount;
            bool matchesCurrentAmount = afterAmount == currentGuarantee.Amount;

            result.Answer = matchesCurrentAmount
                ? $"الأثر المالي الحالي يعود إلى طلب تخفيض منفذ رقم {matchingReduction.SequenceNumber} سُجل بتاريخ {responseDate}، وخفّض المبلغ من {ArabicAmountFormatter.FormatSaudiRiyals(beforeAmount)} إلى {ArabicAmountFormatter.FormatSaudiRiyals(afterAmount)}."
                : $"يوجد طلب تخفيض منفذ رقم {matchingReduction.SequenceNumber} سُجل بتاريخ {responseDate}، لكنه لا يبدو المصدر المباشر للمبلغ الحالي المعروض، ويجب مراجعة تاريخ الإصدارات اللاحق.";
            result.Explanation = matchingReduction.HasResponseDocument
                ? "يمكن الوصول إلى طلب التخفيض نفسه، وخطابه، ومستند رد البنك من نافذة نتيجة الاستعلام."
                : "طلب التخفيض موجود ومُنفذ، لكن لا يوجد مستند رد بنك محفوظ عليه ضمن السلسلة الحالية.";

            AddFacts(result, currentGuarantee, context.Requests, context.History, matchingReduction, matchingReduction);
            result.Facts.Add(new OperationalInquiryFact { Label = "رقم طلب التخفيض", Value = matchingReduction.SequenceNumber.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "المبلغ قبل التخفيض", Value = ArabicAmountFormatter.FormatSaudiRiyals(beforeAmount) });
            result.Facts.Add(new OperationalInquiryFact { Label = "المبلغ المطلوب", Value = matchingReduction.RequestedAmount.HasValue ? ArabicAmountFormatter.FormatSaudiRiyals(matchingReduction.RequestedAmount.Value) : "---" });
            result.Facts.Add(new OperationalInquiryFact { Label = "المبلغ بعد التخفيض", Value = ArabicAmountFormatter.FormatSaudiRiyals(afterAmount) });
            result.Facts.Add(new OperationalInquiryFact { Label = "هل يطابق المبلغ الحالي؟", Value = matchesCurrentAmount ? "نعم" : "لا" });

            AddTimeline(result, context.History, context.Requests);
            return result;
        }

        public OperationalInquiryResult GetResponseDocumentLinkStatusForGuarantee(int guaranteeId)
        {
            InquiryContext context = LoadContext(guaranteeId);
            Guarantee currentGuarantee = context.CurrentGuarantee;

            List<WorkflowRequest> requestsWithResponseDocuments = context.Requests
                .Where(r => r.HasResponseDocument)
                .OrderByDescending(GetRelevantRequestDate)
                .ThenByDescending(r => r.SequenceNumber)
                .ToList();

            List<WorkflowRequest> problematicRequests = requestsWithResponseDocuments
                .Where(HasUnexpectedResponseDocumentLinkIssue)
                .ToList();

            List<WorkflowRequest> verificationRequestLevelOnly = requestsWithResponseDocuments
                .Where(request =>
                    request.Type == RequestType.Verification &&
                    request.Status == RequestStatus.Executed &&
                    !request.ResultVersionId.HasValue)
                .ToList();

            WorkflowRequest? focusRequest = problematicRequests.FirstOrDefault()
                ?? requestsWithResponseDocuments.FirstOrDefault();

            OperationalInquiryResult result = new OperationalInquiryResult
            {
                InquiryKey = $"response-link:{context.RootId}",
                Title = "هل يوجد مستند رد محفوظ بدون ربط رسمي؟",
                Subject = $"الضمان رقم {currentGuarantee.GuaranteeNo}",
                SelectedGuarantee = context.SelectedGuarantee,
                CurrentGuarantee = currentGuarantee,
                RelatedRequest = focusRequest,
                EventDate = focusRequest?.ResponseRecordedAt ?? focusRequest?.RequestDate
            };

            if (!requestsWithResponseDocuments.Any())
            {
                result.Answer = "لا يوجد مستند رد بنك محفوظ على أي طلب ضمن هذه السلسلة حتى الآن.";
                result.Explanation = "لذلك لا توجد حالة ربط أو ترقية مستند يمكن مراجعتها لهذا الضمان من داخل النظام.";

                AddFacts(result, currentGuarantee, context.Requests, context.History, null, null);
                AddTimeline(result, context.History, context.Requests);
                return result;
            }

            if (problematicRequests.Any())
            {
                WorkflowRequest issueRequest = problematicRequests.First();
                result.RelatedRequest = issueRequest;
                result.Answer =
                    $"يوجد {problematicRequests.Count} طلب/طلبات لديها مستند رد محفوظ لكن الربط الرسمي لا يبدو مكتملًا كما ينبغي، وأحدثها {issueRequest.TypeLabel} رقم {issueRequest.SequenceNumber}.";
                result.Explanation =
                    "هذا يعني أن مستند رد البنك موجود على الطلب، لكن أثره الرسمي أو ارتباطه بسجل النتيجة يحتاج مراجعة في السلسلة الحالية.";
            }
            else if (verificationRequestLevelOnly.Any())
            {
                WorkflowRequest verificationOnlyRequest = verificationRequestLevelOnly.First();
                result.RelatedRequest = verificationOnlyRequest;
                result.Answer =
                    $"يوجد {verificationRequestLevelOnly.Count} طلب/طلبات تحقق فيها مستند رد محفوظ على الطلب فقط بدون ربط رسمي بإصدار جديد.";
                result.Explanation =
                    "هذا لا يعد خللًا بالضرورة؛ فهو يطابق سياسة التحقق عندما يُغلق الطلب منفذًا بدون ترقية مستند رد البنك إلى مرفق رسمي.";
            }
            else
            {
                result.Answer = "كل مستندات الرد المحفوظة لهذا الضمان تبدو مرتبطة كما ينبغي وفق السياسات الحالية.";
                result.Explanation = "لم يتم العثور على حالات شاذة في ربط مستندات الرد ضمن السلسلة الحالية.";
            }

            AddFacts(result, currentGuarantee, context.Requests, context.History, focusRequest, focusRequest);
            result.Facts.Add(new OperationalInquiryFact { Label = "إجمالي مستندات الرد المحفوظة", Value = requestsWithResponseDocuments.Count.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "حالات تحتاج مراجعة", Value = problematicRequests.Count.ToString("N0") });
            result.Facts.Add(new OperationalInquiryFact { Label = "تحقق بدون ترقية رسمية", Value = verificationRequestLevelOnly.Count.ToString("N0") });

            foreach (WorkflowRequest request in requestsWithResponseDocuments.Take(10))
            {
                result.Timeline.Add(new OperationalInquiryTimelineEntry
                {
                    Timestamp = request.ResponseRecordedAt ?? request.RequestDate,
                    Title = $"{request.TypeLabel} رقم {request.SequenceNumber}",
                    Details = request.Type == RequestType.Verification && request.Status == RequestStatus.Executed && !request.ResultVersionId.HasValue
                        ? "مستند رد محفوظ على الطلب فقط حسب سياسة التحقق"
                        : HasUnexpectedResponseDocumentLinkIssue(request)
                            ? "يحتاج مراجعة ربط مستند رد البنك"
                            : "مستند رد البنك محفوظ ومربوط كما هو متوقع"
                });
            }

            return result;
        }

        private InquiryContext LoadContext(int guaranteeId)
        {
            Guarantee selectedGuarantee = _databaseService.GetGuaranteeById(guaranteeId)
                ?? throw new InvalidOperationException("تعذر العثور على الضمان المطلوب.");

            int rootId = selectedGuarantee.RootId ?? selectedGuarantee.Id;
            Guarantee currentGuarantee = _databaseService.GetCurrentGuaranteeByRootId(rootId) ?? selectedGuarantee;

            List<Guarantee> history = _databaseService
                .GetGuaranteeHistory(guaranteeId)
                .OrderByDescending(g => g.CreatedAt)
                .ThenByDescending(g => g.VersionNumber)
                .ToList();

            List<WorkflowRequest> requests = _databaseService
                .GetWorkflowRequestsByRootId(rootId)
                .OrderByDescending(GetRelevantRequestDate)
                .ThenByDescending(r => r.SequenceNumber)
                .ToList();

            return new InquiryContext(rootId, selectedGuarantee, currentGuarantee, history, requests);
        }

        private static DateTime GetRelevantRequestDate(WorkflowRequest request)
        {
            return request.ResponseRecordedAt ?? request.RequestDate;
        }

        private bool HasUnexpectedResponseDocumentLinkIssue(WorkflowRequest request)
        {
            if (!request.HasResponseDocument)
            {
                return false;
            }

            if (request.Status != RequestStatus.Executed)
            {
                return false;
            }

            if (request.Type == RequestType.Verification && !request.ResultVersionId.HasValue)
            {
                return false;
            }

            if (!request.ResultVersionId.HasValue)
            {
                return true;
            }

            Guarantee? resultGuarantee = _databaseService.GetGuaranteeById(request.ResultVersionId.Value);
            return resultGuarantee == null ||
                   !resultGuarantee.Attachments.Any(attachment =>
                       string.Equals(attachment.SavedFileName, request.ResponseSavedFileName, StringComparison.OrdinalIgnoreCase));
        }

        private static void BuildResponseResult(
            OperationalInquiryResult result,
            Guarantee currentGuarantee,
            WorkflowRequest request,
            Guarantee? resultGuarantee)
        {
            result.RelatedRequest = request;
            result.ResultGuarantee = resultGuarantee;
            result.EventDate = request.ResponseRecordedAt;

            string dateText = FormatOptionalDate(request.ResponseRecordedAt);
            result.Explanation = "تم الاعتماد على أحدث استجابة بنك محفوظة لهذا الضمان باعتبارها آخر حدث تشغيلي مؤثر.";

            if (request.Status == RequestStatus.Executed)
            {
                result.Answer = request.Type switch
                {
                    RequestType.Extension when resultGuarantee != null =>
                        $"آخر ما حدث هو تنفيذ طلب تمديد بتاريخ {dateText}، ونتج عنه تحديث تاريخ الانتهاء إلى {DualCalendarDateService.FormatDualDate(resultGuarantee.ExpiryDate)} في الإصدار {resultGuarantee.VersionLabel}.",
                    RequestType.Reduction when resultGuarantee != null =>
                        $"آخر ما حدث هو تنفيذ طلب تخفيض بتاريخ {dateText}، ونتج عنه تحديث مبلغ الضمان إلى {ArabicAmountFormatter.FormatSaudiRiyals(resultGuarantee.Amount)} في الإصدار {resultGuarantee.VersionLabel}.",
                    RequestType.Release =>
                        $"آخر ما حدث هو تنفيذ طلب إفراج بتاريخ {dateText}، وأصبحت الحالة التشغيلية الحالية للضمان {currentGuarantee.LifecycleStatusLabel}.",
                    RequestType.Liquidation =>
                        $"آخر ما حدث هو تنفيذ طلب تسييل بتاريخ {dateText}، وأصبحت الحالة التشغيلية الحالية للضمان {currentGuarantee.LifecycleStatusLabel}.",
                    RequestType.Verification when request.ResultVersionId.HasValue =>
                        $"آخر ما حدث هو تنفيذ طلب تحقق بتاريخ {dateText} مع اعتماد مستند رسمي مرتبط بالرد البنكي.",
                    RequestType.Verification =>
                        $"آخر ما حدث هو تنفيذ طلب تحقق بتاريخ {dateText}، وأُغلق الطلب بدون إنشاء إصدار جديد.",
                    RequestType.Replacement when resultGuarantee != null =>
                        $"آخر ما حدث هو تنفيذ طلب استبدال بتاريخ {dateText}، وتم إنشاء الضمان البديل رقم {resultGuarantee.GuaranteeNo}.",
                    _ =>
                        $"آخر ما حدث هو تنفيذ {request.TypeLabel} بتاريخ {dateText}."
                };
                return;
            }

            result.Answer =
                $"آخر ما حدث هو تسجيل استجابة البنك على {request.TypeLabel} بتاريخ {dateText}، وكانت النتيجة {request.StatusLabel}.";
        }

        private static void BuildPendingRequestResult(
            OperationalInquiryResult result,
            Guarantee currentGuarantee,
            WorkflowRequest request)
        {
            result.RelatedRequest = request;
            result.EventDate = request.RequestDate;
            result.Answer =
                $"آخر ما حدث هو إنشاء {request.TypeLabel} بتاريخ {DualCalendarDateService.FormatGregorianDate(request.RequestDate)}، وما زالت حالته الحالية {request.StatusLabel}.";
            result.Explanation =
                $"الحالة التشغيلية الحالية للضمان هي {currentGuarantee.LifecycleStatusLabel}، ولم تُسجل استجابة بنك أحدث من هذا الطلب بعد.";
        }

        private static void BuildVersionResult(
            OperationalInquiryResult result,
            Guarantee currentGuarantee,
            Guarantee latestVersion,
            bool hasRequests)
        {
            result.EventDate = latestVersion.CreatedAt;
            result.ResultGuarantee = latestVersion;

            if (latestVersion.VersionNumber <= 1 && !hasRequests)
            {
                result.Answer =
                    $"آخر ما حدث هو تسجيل الضمان لأول مرة بتاريخ {DualCalendarDateService.FormatGregorianDate(latestVersion.CreatedAt)}، وما زال السجل الحالي في الإصدار {latestVersion.VersionLabel}.";
                result.Explanation = "لا توجد طلبات محفوظة مرتبطة بهذا الضمان بعد.";
                return;
            }

            if (IsTerminalLifecycle(latestVersion.LifecycleStatus))
            {
                result.Answer =
                    $"آخر ما حدث هو {GetTerminalLifecycleEventName(latestVersion.LifecycleStatus)} بتاريخ {DualCalendarDateService.FormatDateTime(latestVersion.CreatedAt)}.";
                result.Explanation =
                    $"الحالة التشغيلية الحالية هي {currentGuarantee.LifecycleStatusLabel}، وهذا حدث دورة حياة وليس إصدار ضمان جديدًا.";
                return;
            }

            result.Answer =
                $"آخر ما حدث هو تحديث السجل الرسمي إلى الإصدار {latestVersion.VersionLabel} بتاريخ {DualCalendarDateService.FormatDateTime(latestVersion.CreatedAt)}.";
            result.Explanation =
                $"الحالة الزمنية الحالية هي {currentGuarantee.StatusLabel}، والحالة التشغيلية الحالية هي {currentGuarantee.LifecycleStatusLabel}.";
        }

        private static bool IsTerminalLifecycle(GuaranteeLifecycleStatus status)
            => status is GuaranteeLifecycleStatus.Released
                or GuaranteeLifecycleStatus.Liquidated
                or GuaranteeLifecycleStatus.Replaced;

        private static string GetTerminalLifecycleEventName(GuaranteeLifecycleStatus status) => status switch
        {
            GuaranteeLifecycleStatus.Released => "إنهاء دورة حياة الضمان بالإفراج",
            GuaranteeLifecycleStatus.Liquidated => "إنهاء دورة حياة الضمان بالتسييل",
            GuaranteeLifecycleStatus.Replaced => "استبدال الضمان بضمان بديل",
            _ => "تغيير حالة الضمان"
        };

        private static string BuildVersionTimelineTitle(Guarantee version)
        {
            if (version.VersionNumber <= 1)
            {
                return "تسجيل السجل الرسمي";
            }

            return $"إنشاء الإصدار {version.VersionLabel}";
        }

        private static string BuildVersionTimelineDetails(Guarantee version)
        {
            string financialSummary = $"الانتهاء: {DualCalendarDateService.FormatDualDate(version.ExpiryDate)} | المبلغ: {ArabicAmountFormatter.FormatSaudiRiyals(version.Amount)}";
            return $"الشروط المحفوظة لهذا الإصدار | {financialSummary}";
        }

        private static string FormatOptionalDate(DateTime? date)
        {
            return date.HasValue ? DualCalendarDateService.FormatGregorianDate(date.Value) : "---";
        }

        private static string BuildResponseTimelineDetails(WorkflowRequest request)
        {
            string detail = $"النتيجة: {request.StatusLabel}";
            if (request.Status != RequestStatus.Executed)
            {
                return detail;
            }

            string effect = request.Type switch
            {
                RequestType.Extension when request.ResultVersionId.HasValue => "نتج إصدار ضمان جديد",
                RequestType.Reduction when request.ResultVersionId.HasValue => "نتج إصدار ضمان جديد",
                RequestType.Verification when request.ResultVersionId.HasValue => "تم اعتماد مستند رسمي على سجل الضمان",
                RequestType.Release => "تم إنهاء دورة حياة الضمان بالإفراج",
                RequestType.Liquidation => "تم إنهاء دورة حياة الضمان بالتسييل",
                RequestType.Replacement => string.IsNullOrWhiteSpace(request.ReplacementGuaranteeNo)
                    ? "تم استبدال الضمان بضمان بديل"
                    : $"تم استبدال الضمان بالضمان {request.ReplacementGuaranteeNo}",
                _ => string.Empty
            };

            return string.IsNullOrWhiteSpace(effect)
                ? detail
                : $"{detail} | {effect}";
        }

        private static void AddFacts(
            OperationalInquiryResult result,
            Guarantee currentGuarantee,
            IReadOnlyCollection<WorkflowRequest> requests,
            IReadOnlyCollection<Guarantee> history,
            WorkflowRequest? latestCreatedRequest,
            WorkflowRequest? latestResponse)
        {
            result.Facts.Add(new OperationalInquiryFact { Label = "رقم الضمان", Value = currentGuarantee.GuaranteeNo });
            result.Facts.Add(new OperationalInquiryFact { Label = "المورد", Value = currentGuarantee.Supplier });
            result.Facts.Add(new OperationalInquiryFact { Label = "البنك", Value = currentGuarantee.Bank });
            result.Facts.Add(new OperationalInquiryFact { Label = "الإصدار الحالي", Value = currentGuarantee.VersionLabel });
            result.Facts.Add(new OperationalInquiryFact { Label = "الحالة الزمنية", Value = currentGuarantee.StatusLabel });
            result.Facts.Add(new OperationalInquiryFact { Label = "الحالة التشغيلية", Value = currentGuarantee.LifecycleStatusLabel });
            result.Facts.Add(new OperationalInquiryFact { Label = "عدد الإصدارات", Value = history.Count.ToString() });
            result.Facts.Add(new OperationalInquiryFact { Label = "عدد الطلبات", Value = requests.Count.ToString() });
            result.Facts.Add(new OperationalInquiryFact
            {
                Label = "آخر طلب",
                Value = latestCreatedRequest == null
                    ? "---"
                    : $"{latestCreatedRequest.TypeLabel} - {latestCreatedRequest.StatusLabel} - {DualCalendarDateService.FormatGregorianDate(latestCreatedRequest.RequestDate)}"
            });
            result.Facts.Add(new OperationalInquiryFact
            {
                Label = "آخر استجابة بنك",
                Value = latestResponse == null
                    ? "---"
                    : $"{latestResponse.TypeLabel} - {latestResponse.StatusLabel} - {FormatOptionalDate(latestResponse.ResponseRecordedAt)}"
            });
            result.Facts.Add(new OperationalInquiryFact
            {
                Label = "خطاب الطلب",
                Value = result.RelatedRequest?.HasLetter == true ? "موجود" : "غير متاح"
            });
            result.Facts.Add(new OperationalInquiryFact
            {
                Label = "مستند رد البنك",
                Value = result.RelatedRequest?.HasResponseDocument == true ? "موجود" : "غير متاح"
            });
        }

        private static void AddTimeline(
            OperationalInquiryResult result,
            IReadOnlyCollection<Guarantee> history,
            IReadOnlyCollection<WorkflowRequest> requests)
        {
            IEnumerable<OperationalInquiryTimelineEntry> versionEntries = history.Select(version => new OperationalInquiryTimelineEntry
            {
                Timestamp = version.CreatedAt,
                Title = BuildVersionTimelineTitle(version),
                Details = BuildVersionTimelineDetails(version)
            });

            IEnumerable<OperationalInquiryTimelineEntry> requestCreatedEntries = requests.Select(request => new OperationalInquiryTimelineEntry
            {
                Timestamp = request.RequestDate,
                Title = $"إنشاء {request.TypeLabel}",
                Details = $"الحالة عند الإنشاء: {request.StatusLabel} | القيمة المطلوبة: {request.RequestedValueLabel}"
            });

            IEnumerable<OperationalInquiryTimelineEntry> responseEntries = requests
                .Where(request => request.ResponseRecordedAt.HasValue)
                .Select(request => new OperationalInquiryTimelineEntry
                {
                    Timestamp = request.ResponseRecordedAt!.Value,
                    Title = $"تسجيل استجابة البنك على {request.TypeLabel}",
                    Details = BuildResponseTimelineDetails(request)
                });

            foreach (OperationalInquiryTimelineEntry entry in versionEntries
                         .Concat(requestCreatedEntries)
                         .Concat(responseEntries)
                         .OrderByDescending(item => item.Timestamp)
                         .Take(8))
            {
                result.Timeline.Add(entry);
            }
        }

        private sealed class InquiryContext
        {
            public InquiryContext(int rootId, Guarantee selectedGuarantee, Guarantee currentGuarantee, List<Guarantee> history, List<WorkflowRequest> requests)
            {
                RootId = rootId;
                SelectedGuarantee = selectedGuarantee;
                CurrentGuarantee = currentGuarantee;
                History = history;
                Requests = requests;
            }

            public int RootId { get; }
            public Guarantee SelectedGuarantee { get; }
            public Guarantee CurrentGuarantee { get; }
            public List<Guarantee> History { get; }
            public List<WorkflowRequest> Requests { get; }
        }
    }
}
