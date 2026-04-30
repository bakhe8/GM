using System.Linq;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class ReferenceTablePagerControllerTests
    {
        [Fact]
        public void BuildVisiblePageNumbers_ReturnsEveryPageFromFilteredTotal()
        {
            int totalPages = ReferenceTablePagerController.CalculateTotalPages(totalItems: 82, pageSize: 10);

            int[] pages = ReferenceTablePagerController.BuildVisiblePageNumbers(currentPage: 1, totalPages)
                .ToArray();

            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, pages);
        }

        [Fact]
        public void BuildVisiblePageNumbers_CollapsesToSinglePageWhenFilteredRowsFitOnePage()
        {
            int totalPages = ReferenceTablePagerController.CalculateTotalPages(totalItems: 9, pageSize: 10);

            int page = Assert.Single(ReferenceTablePagerController.BuildVisiblePageNumbers(currentPage: 1, totalPages));
            Assert.Equal(1, page);
        }
    }
}
