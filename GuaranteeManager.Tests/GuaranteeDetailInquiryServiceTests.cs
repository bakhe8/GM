using System;
using System.Collections.Generic;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using Xunit;

namespace GuaranteeManager.Tests
{
    [Collection("Database")]
    public sealed class GuaranteeDetailInquiryServiceTests
    {
        private readonly TestEnvironmentFixture _fixture;

        public GuaranteeDetailInquiryServiceTests(TestEnvironmentFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void GetLastEventForGuarantee_ReleaseAfterExtension_DatesLifecycleEndFromBankResponseOnly()
        {
            DatabaseService database = _fixture.CreateDatabaseService();
            WorkflowService workflow = _fixture.CreateWorkflowService(database);
            Guarantee seed = _fixture.CreateGuarantee();

            database.SaveGuarantee(seed, new List<string>());
            Guarantee original = database.GetCurrentGuaranteeByNo(seed.GuaranteeNo)!;
            WorkflowRequest extensionRequest = workflow.CreateExtensionRequest(
                original.Id,
                original.ExpiryDate.AddDays(60),
                "extend before release",
                "tester");
            workflow.RecordBankResponse(extensionRequest.Id, RequestStatus.Executed, "extended");

            Guarantee extended = database.GetCurrentGuaranteeByRootId(original.RootId ?? original.Id)!;
            WorkflowRequest releaseRequest = workflow.CreateReleaseRequest(
                extended.Id,
                "release after extension",
                "tester");
            workflow.RecordBankResponse(releaseRequest.Id, RequestStatus.Executed, "released");

            var inquiry = new GuaranteeDetailInquiryService(database);
            OperationalInquiryResult result = inquiry.GetLastEventForGuarantee(extended.Id);

            Assert.Contains("طلب إفراج", result.Answer);
            Assert.DoesNotContain(
                result.Timeline,
                item => item.Title.Contains("إنهاء دورة حياة الضمان", StringComparison.Ordinal));
            Assert.Contains(
                result.Timeline,
                item => item.Title == "تسجيل استجابة البنك على طلب إفراج"
                    && item.Details.Contains("تم إنهاء دورة حياة الضمان بالإفراج", StringComparison.Ordinal));
            Assert.Contains(
                result.Timeline,
                item => item.Title == $"إنشاء الإصدار {extended.VersionLabel}"
                    && item.Details.Contains("الشروط المحفوظة لهذا الإصدار", StringComparison.Ordinal));
        }
    }
}
