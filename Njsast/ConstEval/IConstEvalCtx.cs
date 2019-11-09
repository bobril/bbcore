using Njsast.Ast;

namespace Njsast.ConstEval
{
    public interface IConstEvalCtx
    {
        string SourceName { get; }
        JsModule ResolveRequire(string name);

        /// export will be usually string, could be JsSymbol in ES6
        object? ConstValue(IConstEvalCtx ctx, JsModule module, object export);

        bool AllowEvalObjectWithJustConstKeys { get; }
        bool JustModuleExports { get; set; }

        string ConstStringResolver(string str);

        IConstEvalCtx StripPathResolver();

        IConstEvalCtx CreateForSourceName(string sourceName);
    }
}
