using System.Collections.Generic;
using Njsast.Ast;

namespace Njsast.ConstEval
{
    public class ResolvingConstEvalCtx : IConstEvalCtx
    {
        readonly IImportResolver _resolver;

        public ResolvingConstEvalCtx(string currentFilePath, IImportResolver resolver)
        {
            SourceName = currentFilePath;
            _resolver = resolver;
        }

        public JsModule ResolveRequire(string name)
        {
            return new JsModule(SourceName, name);
        }

        public object? ConstValue(IConstEvalCtx ctx, JsModule module, object export)
        {
            if (JustModuleExports)
                return null;
            if (!(export is string)) return null;
            var (fileName, content) = _resolver.ResolveAndLoad(module);
            if (fileName == null || content == null)
                return null;
            try
            {
                var ctx2 = ctx.CreateForSourceName(fileName);
                var treeWalker = new ExportFinder((string)export, ctx2);
                treeWalker.Walk(content);
                if (treeWalker.Result == null)
                    return null;
                var result = treeWalker.Result.ConstValue(ctx2);
                if (result == null) return null;
                if (treeWalker.CompleteResult)
                {
                    if (result is IReadOnlyDictionary<object, object> dict)
                    {
                        if (dict.TryGetValue((string)export, out result))
                            return result;
                        return AstUndefined.Instance;
                    }
                    return null;
                }
                return result;
            }
            catch
            {
                return null;
            }
        }

        public IConstEvalCtx StripPathResolver()
        {
            return this;
        }

        public string ConstStringResolver(string str)
        {
            return str;
        }

        public IConstEvalCtx CreateForSourceName(string sourceName)
        {
            return new ResolvingConstEvalCtx(sourceName, _resolver);
        }

        public bool AllowEvalObjectWithJustConstKeys => false;

        public string SourceName { get; }
        public bool JustModuleExports { get; set; }
    }
}
