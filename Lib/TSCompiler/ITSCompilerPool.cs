namespace Lib.TSCompiler
{
    public interface ITSCompilerPool
    {
        ITSCompiler Get();
        void Release(ITSCompiler value);
    }
}
