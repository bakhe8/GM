using GuaranteeManager.Models;
using GuaranteeManager.Utils;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class InquiryContextRoutingResolverTests
    {
        [Theory]
        [InlineData("last-event:10", GuaranteeFocusArea.Series, null)]
        [InlineData("extension-timing:10", GuaranteeFocusArea.Requests, 42)]
        [InlineData("outstanding-Extension:10", GuaranteeFocusArea.Requests, 42)]
        [InlineData("expired-no-extension:10", GuaranteeFocusArea.Requests, 42)]
        [InlineData("reduction-source:10", GuaranteeFocusArea.Requests, 42)]
        [InlineData("release-evidence:10", GuaranteeFocusArea.Outputs, 42)]
        [InlineData("liquidation-evidence:10", GuaranteeFocusArea.Outputs, 42)]
        [InlineData("response-link:10", GuaranteeFocusArea.Outputs, 42)]
        public void TryResolve_RoutesKnownInquiryResultsToContextSections(
            string inquiryKey,
            GuaranteeFocusArea expectedArea,
            int? expectedRequestId)
        {
            OperationalInquiryResult result = new()
            {
                InquiryKey = inquiryKey,
                RelatedRequest = expectedRequestId.HasValue ? new WorkflowRequest { Id = expectedRequestId.Value } : null
            };

            bool resolved = InquiryContextRoutingResolver.TryResolve(result, out GuaranteeFocusArea focusArea, out int? requestIdToFocus);

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

            bool resolved = InquiryContextRoutingResolver.TryResolve(result, out GuaranteeFocusArea focusArea, out int? requestIdToFocus);

            Assert.False(resolved);
            Assert.Equal(GuaranteeFocusArea.Series, focusArea);
            Assert.Null(requestIdToFocus);
        }
    }
}
