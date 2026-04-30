using System;
using System.Collections.Generic;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class GuaranteeActionProfileTests
    {
        [Fact]
        public void RequestTypes_DoNotExposeAnnulment()
        {
            Assert.DoesNotContain("Annulment", Enum.GetNames<RequestType>());
        }

        [Fact]
        public void Build_ForExpiredLifecycle_AllowsReleaseOnly()
        {
            Guarantee guarantee = CreateGuarantee(GuaranteeLifecycleStatus.Expired);

            GuaranteeActionProfile profile = GuaranteeActionProfile.Build(guarantee, new List<WorkflowRequest>());

            Assert.False(profile.ExtensionAction.IsEnabled);
            Assert.True(profile.ReleaseAction.IsEnabled);
            Assert.False(profile.ReductionAction.IsEnabled);
            Assert.False(profile.LiquidationAction.IsEnabled);
            Assert.False(profile.VerificationAction.IsEnabled);
            Assert.False(profile.ReplacementAction.IsEnabled);
            Assert.Contains("الإفراج", profile.ReleaseAction.Hint);
        }

        [Fact]
        public void Build_WithPendingRequest_RoutesToTimelineRequests()
        {
            Guarantee guarantee = CreateGuarantee(GuaranteeLifecycleStatus.Active);
            List<WorkflowRequest> requests = new()
            {
                new WorkflowRequest
                {
                    Type = RequestType.Liquidation,
                    Status = RequestStatus.Pending,
                    RequestDate = DateTime.Today
                }
            };

            GuaranteeActionProfile profile = GuaranteeActionProfile.Build(guarantee, requests);

            Assert.Equal(GuaranteeFocusArea.Requests, profile.SuggestedFocusArea);
            Assert.Contains("السجل", profile.SuggestedFocusLabel);
        }

        [Fact]
        public void Build_WithWorkflowOutputs_RoutesToTimelineOutputs()
        {
            Guarantee guarantee = CreateGuarantee(GuaranteeLifecycleStatus.Active);
            List<WorkflowRequest> requests = new()
            {
                new WorkflowRequest
                {
                    Type = RequestType.Release,
                    Status = RequestStatus.Executed,
                    RequestDate = DateTime.Today,
                    LetterSavedFileName = "letter.docx"
                }
            };

            GuaranteeActionProfile profile = GuaranteeActionProfile.Build(guarantee, requests);

            Assert.Equal(GuaranteeFocusArea.Outputs, profile.SuggestedFocusArea);
            Assert.Contains("مخرجات", profile.SummaryTitle);
        }

        private static Guarantee CreateGuarantee(GuaranteeLifecycleStatus status)
        {
            return new Guarantee
            {
                Id = 1,
                RootId = 1,
                Supplier = "اختبار",
                Bank = "بنك ساب",
                GuaranteeNo = "BG-TEST-0001",
                Amount = 1000,
                ExpiryDate = DateTime.Today.AddDays(30),
                GuaranteeType = "ابتدائي",
                Beneficiary = BusinessPartyDefaults.DefaultBeneficiaryName,
                LifecycleStatus = status,
                IsCurrent = true,
                VersionNumber = 1
            };
        }
    }
}
