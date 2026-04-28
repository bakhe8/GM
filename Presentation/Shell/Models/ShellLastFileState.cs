namespace GuaranteeManager
{
    public sealed record ShellLastFileState(int RootId, string GuaranteeNo, string Summary)
    {
        public static ShellLastFileState Empty { get; } = new(
            0,
            "لا يوجد ضمان حديث",
            "لم يتم تحديد أي ضمان بعد داخل الجلسة الحالية");

        public bool HasLastFile => RootId > 0;
    }
}
