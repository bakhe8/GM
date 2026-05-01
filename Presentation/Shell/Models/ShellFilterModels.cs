using GuaranteeManager.Services;

namespace GuaranteeManager
{
    public enum GuaranteeStatusFilter
    {
        Active,
        ExpiringSoon,
        NeedsFollowUp,
        Expired
    }

    public sealed class FilterOption
    {
        public static readonly FilterOption AllTimeStatuses = new("كل الحالات", null);

        public FilterOption(string label, GuaranteeTimeStatus? value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }
        public GuaranteeTimeStatus? Value { get; }

        public override string ToString() => Label;
    }

    public sealed class OperationalInquiryOption
    {
        public OperationalInquiryOption(string id, string section, string label, string description)
        {
            Id = id;
            Section = section;
            Label = label;
            Description = description;
        }

        public string Id { get; }
        public string Section { get; }
        public string Label { get; }
        public string Description { get; }
        public string Display => $"{Section} | {Label}";

        public override string ToString() => Display;
    }
}
