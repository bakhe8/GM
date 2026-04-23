namespace GuaranteeManager.Services
{
    internal readonly record struct ExcelExportResult(bool Exported, string? OutputPath)
    {
        public static ExcelExportResult Cancelled => new(false, null);

        public static ExcelExportResult Saved(string outputPath) => new(true, outputPath);
    }
}
