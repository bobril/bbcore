using System.Collections.Generic;
using Njsast.Compress;
using Njsast.Output;
using Njsast.Reader;
using Njsast.Scope;

namespace Njsast.Ast;

/// The toplevel scope
public class AstToplevel : AstScope
{
    /// [Object/S] a map of name -> SymbolDef for all undeclared names
    public Dictionary<string, SymbolDef>? Globals;

    public HashSet<string>? NonRootSymbolNames;

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
        if (Globals!.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var global = new SymbolDef(this, symbol, null);
        global.Undeclared = true;
        global.Global = true;
        Globals!.Add(name, global);
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
        => Compress(compressOptions, scopeOptions, 6);

    public AstToplevel Compress(ICompressOptions? compressOptions, ScopeOptions? scopeOptions, int ecmaVersion)
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
                    var trDetect = new DetectPurposeTransformer();
                    transformed = (AstToplevel) trDetect.Transform(transformed);
                    var tr = new RemoveSideEffectFreeCodeTreeTransformer();
                    transformed = (AstToplevel) tr.Transform(transformed);
                    if (tr.Modified) shouldIterateAgain = true;
                    transformed._isScopeFigured = !tr.Modified && !trDetect.Modified;
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
                transformed._isScopeFigured = !shouldIterateAgain;
                if (compressOptions.EnableRemoveSideEffectFreeCode)
                {
                    transformed.FigureOutScope(scopeOptions);
                    var trDetect = new DetectPurposeTransformer();
                    transformed = (AstToplevel) trDetect.Transform(transformed);
                    var tr = new RemoveSideEffectFreeCodeTreeTransformer();
                    transformed = (AstToplevel) tr.Transform(transformed);
                    if (tr.Modified) shouldIterateAgain = true;
                    transformed._isScopeFigured = !tr.Modified && !trDetect.Modified;
                }
            } while (shouldIterateAgain && ++iteration < compressOptions.MaxPasses);
        }

        if ((ecmaVersion >= 2015 ? ecmaVersion - 2009 : ecmaVersion) >= 10)
        {
            if (!transformed._isScopeFigured) transformed.FigureOutScope(scopeOptions);
            var modern = new ModernSyntaxTreeTransformer(ecmaVersion);
            transformed = (AstToplevel)modern.Transform(transformed);
            if (modern.Modified) transformed._isScopeFigured = false;
        }

        return transformed;
    }


    class GatherSymbolNames : TreeWalker
    {
        readonly HashSet<string> _nonRootSymbolNames;

        public GatherSymbolNames(HashSet<string> nonRootSymbolNames)
        {
            _nonRootSymbolNames = nonRootSymbolNames;
        }

        protected override void Visit(AstNode node)
        {
            if (node is IMayBeBlockScope { IsBlockScope: true, BlockScope: {} blockScope })
            {
                foreach (var name in blockScope.Variables!.Keys)
                {
                    _nonRootSymbolNames.Add(name);
                }
                foreach (var name in blockScope.Functions!.Keys)
                {
                    _nonRootSymbolNames.Add(name);
                }
            }
            if (node is AstScope scope)
            {
                if (scope.ParentScope != null)
                {
                    foreach (var name in scope.Variables!.Keys)
                    {
                        _nonRootSymbolNames.Add(name);
                    }
                    foreach (var name in scope.Functions!.Keys)
                    {
                        _nonRootSymbolNames.Add(name);
                    }
                }
            }
        }
    }

    public HashSet<string> CalcNonRootSymbolNames()
    {
        if (NonRootSymbolNames != null) return NonRootSymbolNames;
        if (Globals == null) FigureOutScope();
        NonRootSymbolNames = new HashSet<string>(Globals!.Keys);
        new GatherSymbolNames(NonRootSymbolNames).Walk(this);
        return NonRootSymbolNames;
    }
}
