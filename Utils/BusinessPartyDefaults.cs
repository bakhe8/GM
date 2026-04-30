namespace GuaranteeManager.Utils
{
    public static class BusinessPartyDefaults
    {
        public const string DefaultBeneficiaryName = "مستشفى الملك فيصل التخصصي ومركز الأبحاث";

        public static string NormalizeBeneficiary(string? beneficiary)
        {
            return DefaultBeneficiaryName;
        }
    }
}
