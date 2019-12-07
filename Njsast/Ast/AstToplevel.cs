using System.Collections.Generic;
using System.IO;
using System.Net;
using Njsast.Compress;
using Njsast.Output;
using Njsast.Reader;
using Njsast.Scope;

namespace Njsast.Ast
{
    /// The toplevel scope
    public class AstToplevel : AstScope
    {
        /// [Object/S] a map of name -> SymbolDef for all undeclared names
        public Dictionary<string, SymbolDef>? Globals;

        bool _isScopeFigured;

        public AstToplevel(string? source, Position startPos, Position endPos) : base(source, startPos, endPos)
        {
        }

        public AstToplevel()
        {
        }

        public override AstNode ShallowClone()
        {
            var res = new AstToplevel(Source, Start, End);
            res.Body.AddRange(Body.AsReadOnlySpan());
            res.HasUseStrictDirective = HasUseStrictDirective;
            return res;
        }

        public SymbolDef DefGlobal(AstSymbol symbol)
        {
            var name = symbol.Name;
            if (Globals!.ContainsKey(name))
            {
                return Globals[name];
            }

            var global = new SymbolDef(this, symbol, null);
            global.Undeclared = true;
            global.Global = true;
            Globals.Add(name, global);
            return global;
        }

        public override AstScope Resolve()
        {
            return this;
        }

        public override void CodeGen(OutputContext output)
        {
            if (HasUseStrictDirective)
            {
                output.Indent();
                output.PrintString("use strict");
                output.Print(";");
                output.Newline();
            }

            for (var i = 0u; i < Body.Count; i++)
            {
                var stmt = Body[i];
                if (!(stmt is AstEmptyStatement))
                {
                    output.Indent();
                    stmt.Print(output);
                    output.Newline();
                    output.Newline();
                }
            }
        }

        public override bool IsBlockScope => false;

        public void FigureOutScope(ScopeOptions? options = null)
        {
            if (options == null) options = new ScopeOptions();
            new ScopeParser(options).FigureOutScope(this);
            _isScopeFigured = true;
        }

        public void Mangle(ScopeOptions? options = null, OutputOptions? outputOptions = null)
        {
            options ??= new ScopeOptions();
            new ScopeParser(options).FigureOutScope(this);
            var m = new MangleTreeWalker(options, outputOptions);
            m.Mangle(this);
        }

        public AstToplevel Compress(ICompressOptions? compressOptions = null, ScopeOptions? scopeOptions = null)
        {
            compressOptions ??= CompressOptions.Default;
            scopeOptions ??= new ScopeOptions();
            var iteration = 0;
            var transformed = this;
            bool shouldIterateAgain;

            if (compressOptions.NotUsingCompressTreeTransformer)
            {
                do
                {
                    shouldIterateAgain = false;
                    if (!transformed._isScopeFigured)
                        transformed.FigureOutScope(scopeOptions);
                    if (compressOptions.EnableRemoveSideEffectFreeCode)
                    {
                        //File.WriteAllText("CompressIter" + iteration + ".js",
                        //    transformed.PrintToString(new OutputOptions {Beautify = true}));
                        var tr = new RemoveSideEffectFreeCodeTreeTransformer();
                        transformed = (AstToplevel) tr.Transform(transformed);
                        if (tr.Modified) shouldIterateAgain = true;
                        transformed._isScopeFigured = false;
                    }
                } while (shouldIterateAgain && ++iteration < compressOptions.MaxPasses);
            }
            else
            {
                var treeTransformer = new CompressTreeTransformer(compressOptions);
                do
                {
                    if (!transformed._isScopeFigured || iteration > 0)
                        transformed.FigureOutScope(scopeOptions);
                    transformed = (AstToplevel) treeTransformer.Compress(transformed, out shouldIterateAgain);
                    if (compressOptions.EnableRemoveSideEffectFreeCode)
                    {
                        transformed.FigureOutScope(scopeOptions);
                        var tr = new RemoveSideEffectFreeCodeTreeTransformer();
                        transformed = (AstToplevel) tr.Transform(transformed);
                        if (tr.Modified) shouldIterateAgain = true;
                    }
                } while (shouldIterateAgain && ++iteration < compressOptions.MaxPasses);
            }

            return transformed;
        }
    }
}
