using System;
using System.Collections.Generic;
using GuaranteeManager.Models;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class GuaranteeActionProfileTests
    {
        [Fact]
        public void Build_ForActiveGuarantee_DisablesAnnulmentAction()
        {
            Guarantee guarantee = CreateGuarantee(GuaranteeLifecycleStatus.Active);

            GuaranteeActionProfile profile = GuaranteeActionProfile.Build(guarantee, new List<WorkflowRequest>());

            Assert.False(profile.AnnulmentAction.IsEnabled);
            Assert.Equal("طلب النقض متاح للضمانات المفرج عنها أو المسيّلة فقط.", profile.AnnulmentAction.Hint);
        }

        [Fact]
        public void Build_ForExpiredLifecycle_DisablesCreationActions()
        {
            Guarantee guarantee = CreateGuarantee(GuaranteeLifecycleStatus.Expired);

            GuaranteeActionProfile profile = GuaranteeActionProfile.Build(guarantee, new List<WorkflowRequest>());

            Assert.False(profile.ExtensionAction.IsEnabled);
            Assert.False(profile.ReleaseAction.IsEnabled);
            Assert.False(profile.ReductionAction.IsEnabled);
            Assert.False(profile.LiquidationAction.IsEnabled);
            Assert.False(profile.VerificationAction.IsEnabled);
            Assert.False(profile.ReplacementAction.IsEnabled);
            Assert.Contains("منتهية الصلاحية", profile.ReleaseAction.Hint);
        }

        [Fact]
        public void Build_WithPendingRequest_RoutesToRequestsWorkspace()
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

            Assert.Equal(GuaranteeFileFocusArea.Requests, profile.SuggestedFocusArea);
            Assert.Contains("شاشة الطلبات", profile.OpenFileAction.Hint);
        }

        [Fact]
        public void Build_WithWorkflowOutputs_RoutesToRequestsWorkspace()
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

            Assert.Equal(GuaranteeFileFocusArea.Outputs, profile.SuggestedFocusArea);
            Assert.Contains("المخرجات", profile.OpenFileAction.Hint);
        }

        [Theory]
        [InlineData(GuaranteeLifecycleStatus.Released)]
        [InlineData(GuaranteeLifecycleStatus.Liquidated)]
        public void Build_ForEligibleLifecycle_EnablesAnnulmentAction(GuaranteeLifecycleStatus status)
        {
            Guarantee guarantee = CreateGuarantee(status);

            GuaranteeActionProfile profile = GuaranteeActionProfile.Build(guarantee, new List<WorkflowRequest>());

            Assert.True(profile.AnnulmentAction.IsEnabled);
        }

        [Fact]
        public void Build_ForEligibleLifecycleWithPendingAnnulment_DisablesAnnulmentAction()
        {
            Guarantee guarantee = CreateGuarantee(GuaranteeLifecycleStatus.Released);
            List<WorkflowRequest> requests = new()
            {
                new WorkflowRequest
                {
                    Type = RequestType.Annulment,
                    Status = RequestStatus.Pending,
                    RequestDate = DateTime.Today
                }
            };

            GuaranteeActionProfile profile = GuaranteeActionProfile.Build(guarantee, requests);

            Assert.False(profile.AnnulmentAction.IsEnabled);
            Assert.Equal("يوجد طلب نقض معلق بالفعل لهذا الضمان.", profile.AnnulmentAction.Hint);
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
                Beneficiary = "مستفيد اختبار",
                LifecycleStatus = status,
                IsCurrent = true,
                VersionNumber = 1
            };
        }
    }
}
