namespace GuaranteeManager.Services
{
    public sealed record BankPortfolioSummary(
        string Bank,
        int Count,
        int Active,
        int ExpiringSoon,
        int Expired,
        decimal Amount,
        string TopSupplier);
}
