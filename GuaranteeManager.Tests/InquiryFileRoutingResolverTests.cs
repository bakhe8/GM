using GuaranteeManager.Models;
using GuaranteeManager.Utils;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class InquiryFileRoutingResolverTests
    {
        [Theory]
        [InlineData("last-event:10", GuaranteeFileFocusArea.Series, null)]
        [InlineData("extension-timing:10", GuaranteeFileFocusArea.Requests, 42)]
        [InlineData("outstanding-Extension:10", GuaranteeFileFocusArea.Requests, 42)]
        [InlineData("expired-no-extension:10", GuaranteeFileFocusArea.Requests, 42)]
        [InlineData("reduction-source:10", GuaranteeFileFocusArea.Requests, 42)]
        [InlineData("release-evidence:10", GuaranteeFileFocusArea.Outputs, 42)]
        [InlineData("liquidation-evidence:10", GuaranteeFileFocusArea.Outputs, 42)]
        [InlineData("response-link:10", GuaranteeFileFocusArea.Outputs, 42)]
        public void TryResolve_RoutesKnownInquiryResultsToFileSections(
            string inquiryKey,
            GuaranteeFileFocusArea expectedArea,
            int? expectedRequestId)
        {
            OperationalInquiryResult result = new()
            {
                InquiryKey = inquiryKey,
                RelatedRequest = expectedRequestId.HasValue ? new WorkflowRequest { Id = expectedRequestId.Value } : null
            };

            bool resolved = InquiryFileRoutingResolver.TryResolve(result, out GuaranteeFileFocusArea focusArea, out int? requestIdToFocus);

            Assert.True(resolved);
            Assert.Equal(expectedArea, focusArea);
            Assert.Equal(expectedRequestId, requestIdToFocus);
        }

        [Fact]
        public void TryResolve_ForUnknownInquiryResultFallsBackWithoutClaimingResolution()
        {
            OperationalInquiryResult result = new()
            {
                InquiryKey = "summary-unknown:10"
            };

            bool resolved = InquiryFileRoutingResolver.TryResolve(result, out GuaranteeFileFocusArea focusArea, out int? requestIdToFocus);

            Assert.False(resolved);
            Assert.Equal(GuaranteeFileFocusArea.Series, focusArea);
            Assert.Null(requestIdToFocus);
        }
    }
}
