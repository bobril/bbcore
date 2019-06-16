using System.Collections.Generic;
using Njsast.Ast;
using Njsast.Reader;

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
            return new JsModule {ImportedFrom = SourceName, Name = name};
        }

        public object ConstValue(JsModule module, object export)
        {
            if (JustModuleExports)
                return null;
            var fileName = _resolver.ResolveName(module);
            if (fileName == null) return null;
            var content = _resolver.LoadContent(fileName);
            if (content == null || !(export is string)) return null;
            try
            {
                var parser = new Parser(new Options(), content);
                var toplevel = parser.Parse();
                toplevel.FigureOutScope();
                var treeWalker = new ExportFinder((string) export);
                treeWalker.Walk(toplevel);
                if (treeWalker.Result == null)
                    return null;
                var ctx = CreateForSourceName(fileName);
                var result = treeWalker.Result.ConstValue(ctx);
                if (result == null) return null;
                if (treeWalker.CompleteResult)
                {
                    if (result is IReadOnlyDictionary<object, object> dict)
                    {
                        if (dict.TryGetValue((string) export, out result))
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

        public bool UseStringPathResolver => false;

        public string SourceName { get; }
        public bool JustModuleExports { get; set; }
    }
}
