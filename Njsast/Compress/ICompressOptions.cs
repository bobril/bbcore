namespace Njsast.Compress
{
    public interface ICompressOptions
    {
        bool EnableUnreachableCodeElimination { get; }
        bool EnableEmptyStatementElimination { get; }
        bool EnableBlockElimination { get; }
        bool EnableBooleanCompress { get; }
        bool EnableFunctionReturnCompress { get; }
        bool EnableVariableHoisting { get; }
        bool EnableUnusedFunctionElimination { get; }
        bool EnableRemoveSideEffectFreeCode { get; }
        uint MaxPasses { get; }
        bool NotUsingCompressTreeTransformer =>
            !EnableUnreachableCodeElimination && !EnableEmptyStatementElimination &&
            !EnableBlockElimination && !EnableBooleanCompress
            && !EnableFunctionReturnCompress && !EnableVariableHoisting && !EnableUnusedFunctionElimination;
    }
}
