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
        uint MaxPasses { get; }
    }
}