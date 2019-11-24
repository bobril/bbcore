namespace Njsast.Compress
{
    public class CompressOptions : ICompressOptions
    {
        public bool EnableUnreachableCodeElimination { get; set; }
        public bool EnableEmptyStatementElimination { get; set; }
        public bool EnableBlockElimination { get; set; }
        public bool EnableBooleanCompress { get; set; }
        public bool EnableFunctionReturnCompress { get; set; }
        public bool EnableVariableHoisting { get; set; }
        public bool EnableUnusedFunctionElimination { get; set; }
        public bool EnableRemoveSideEffectFreeCode { get; set; }
        public uint MaxPasses { get; set; }

        public static readonly ICompressOptions Default = new CompressOptions
        {
            EnableUnreachableCodeElimination = true,
            EnableEmptyStatementElimination = true,
            EnableBlockElimination = true,
            EnableBooleanCompress = true,
            EnableFunctionReturnCompress = true,
            EnableVariableHoisting = true,
            EnableUnusedFunctionElimination = true,
            EnableRemoveSideEffectFreeCode = true,
            MaxPasses = 10
        };
    }
}
