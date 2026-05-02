using System;
using GuaranteeManager.Models;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class BanksWorkspaceDataServiceTests
    {
        [Fact]
        public void BuildDetailState_UsesCurrencyAwareAmountHeadlineAndCaption()
        {
            var service = new BanksWorkspaceDataService();
            BankWorkspaceItem item = Assert.Single(service.BuildItems(
                new[]
                {
                    new Guarantee
                    {
                        Id = 1,
                        RootId = 1,
                        IsCurrent = true,
                        VersionNumber = 1,
                        GuaranteeNo = "BG-BANK-001",
                        Supplier = "مورد اختبار",
                        Bank = "بنك اختبار",
                        Amount = 23_900_000m,
                        ExpiryDate = DateTime.Today.AddDays(30),
                        GuaranteeType = "ابتدائي",
                        LifecycleStatus = GuaranteeLifecycleStatus.Active
                    }
                },
                Array.Empty<string>()));

            BanksWorkspaceDetailState state = service.BuildDetailState(item);

            Assert.Equal("23,900,000 ريال", state.AmountHeadline);
            Assert.Equal("ثلاثة وعشرون مليون وتسعمئة ألف ريال سعودي", state.AmountCaption);
            Assert.Equal("23,900,000 ريال", item.AmountDisplay);
        }

        [Fact]
        public void BuildFilteredItems_UsesTotalAmountWithCurrency()
        {
            var service = new BanksWorkspaceDataService();
            var items = service.BuildItems(
                new[]
                {
                    new Guarantee
                    {
                        Id = 1,
                        RootId = 1,
                        IsCurrent = true,
                        VersionNumber = 1,
                        GuaranteeNo = "BG-BANK-001",
                        Supplier = "مورد اختبار",
                        Bank = "بنك اختبار",
                        Amount = 1_000m,
                        ExpiryDate = DateTime.Today.AddDays(30),
                        GuaranteeType = "ابتدائي",
                        LifecycleStatus = GuaranteeLifecycleStatus.Active
                    },
                    new Guarantee
                    {
                        Id = 2,
                        RootId = 2,
                        IsCurrent = true,
                        VersionNumber = 1,
                        GuaranteeNo = "BG-BANK-002",
                        Supplier = "مورد آخر",
                        Bank = "بنك آخر",
                        Amount = 2_500m,
                        ExpiryDate = DateTime.Today.AddDays(45),
                        GuaranteeType = "ابتدائي",
                        LifecycleStatus = GuaranteeLifecycleStatus.Active
                    }
                },
                Array.Empty<string>());

            BanksWorkspaceFilterResult result = service.BuildFilteredItems(
                items,
                string.Empty,
                "الأعلى قيمة");

            Assert.Equal("3,500 ريال", result.Metrics.Amount);
        }
    }
}
