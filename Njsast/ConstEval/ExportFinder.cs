using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Njsast.Ast;
using Njsast.Reader;
using Njsast.Runtime;

namespace Njsast.ConstEval;

public class ExportFinder : TreeWalker
{
    readonly string _export;
    readonly IConstEvalCtx _ctx;
    public AstNode? Result;
    public bool CompleteResult;

    public ExportFinder(string export, IConstEvalCtx ctx)
    {
        _export = export;
        _ctx = ctx;
    }

    internal static bool IsExportsAssignVoid0(AstNode? node)
    {
        while (true)
        {
            if (node == null) return false;
            var pea = node.IsExportsAssign();
            if (!pea.HasValue) return node is AstUnary unary && unary.Operator == Operator.Void;
            node = pea!.Value.value;
        }
    }

    protected override void Visit(AstNode node)
    {
        if (IsExportsAssignVoid0(node))
        {
            StopDescending();
            return;
        }
        if (node is AstDot dot)
        {
            StopDescending();
            if (IsExports(dot.Expression) && (string)dot.Property == _export)
            {
                var parent = Parent();
                if (parent is AstAssign assign && assign.Operator == Operator.Assignment && assign.Left == node)
                {
                    Result = assign.Right;
                    CompleteResult = false;
                }
            }
        }

        if (node is AstCall call && IsExportStarCall(call))
        {
            var module = call.Args[0].ConstValue(_ctx);
            if (module is JsModule)
            {
                var res = _ctx.ConstValue(_ctx, (JsModule)module, _export);
                if (res != null)
                {
                    Result = TypeConverter.ToAst(res);
                    CompleteResult = false;
                }
            }
        }

        if (node is AstAssign assign2 && assign2.Operator == Operator.Assignment && IsExports(assign2.Left))
        {
            StopDescending();
            Result = assign2.Right;
            CompleteResult = true;
        }
    }

    internal static bool IsExportStarCall(AstCall call) =>
        call.Expression is AstSymbol symbol &&
        (symbol.Name == "__exportStar" && call.Args.Count == 2 || symbol.Name == "__export" && call.Args.Count == 1) &&
        call.Args[0] is AstCall;

    internal static bool IsExports(AstNode node)
    {
        if (node is AstDot dot && dot.Property as string == "exports")
        {
            if (dot.Expression is AstSymbol symbolModule)
            {
                var def2 = symbolModule.Thedef;
                if (def2 == null) return false;
                return def2.Undeclared && def2.Global && def2.Name == "module";
            }

            return false;
        }

        if (!(node is AstSymbolRef symbol))
            return false;
        var def = symbol.Thedef;
        if (def == null) return false;
        return def.Undeclared && def.Global && def.Name == "exports";
    }
}
/// Everything <see cref="ExportFinder"/> reacts to in one module, in source order. Finding an export then needs only the
/// entries of that export and the ones which can set any export, instead of walking the whole module for each export.
public sealed class ExportIndex
{
    static readonly ConditionalWeakTable<AstToplevel, ExportIndex> Cache = new();

    enum EntryKind
    {
        Named,
        ExportStar,
        ModuleExports
    }

    readonly List<(EntryKind Kind, AstNode Node)> _entries = [];
    readonly Dictionary<string, List<int>> _named = new();
    readonly List<int> _anyExport = [];
    // Walk of ExportFinder would fail on such module for every export
    bool _unsupported;

    public static ExportIndex For(AstToplevel module) => Cache.GetValue(module, static m => new ExportIndex(m));

    ExportIndex(AstToplevel module)
    {
        new Builder(this).Walk(module);
    }

    /// Same result as <c>ExportFinder.Walk(module)</c> for the export name, including evaluating exports of other modules
    /// through <paramref name="ctx"/> in the same order.
    public (AstNode? Result, bool CompleteResult) Find(string export, IConstEvalCtx ctx)
    {
        if (_unsupported) throw new InvalidCastException();
        AstNode? result = null;
        var complete = false;
        _named.TryGetValue(export, out var named);
        var n = 0;
        var a = 0;
        while (true)
        {
            var hasNamed = named != null && n < named.Count;
            var hasAny = a < _anyExport.Count;
            if (!hasNamed && !hasAny) break;
            int index;
            if (hasNamed && (!hasAny || named![n] < _anyExport[a]))
                index = named![n++];
            else
                index = _anyExport[a++];
            var (kind, node) = _entries[index];
            switch (kind)
            {
                case EntryKind.Named:
                    result = ((AstAssign)node).Right;
                    complete = false;
                    break;
                case EntryKind.ExportStar:
                    var module = ((AstCall)node).Args[0].ConstValue(ctx);
                    if (module is JsModule jsModule)
                    {
                        var res = ctx.ConstValue(ctx, jsModule, export);
                        if (res != null)
                        {
                            result = TypeConverter.ToAst(res);
                            complete = false;
                        }
                    }

                    break;
                default:
                    result = ((AstAssign)node).Right;
                    complete = true;
                    break;
            }
        }

        return (result, complete);
    }

    sealed class Builder(ExportIndex owner) : TreeWalker
    {
        protected override void Visit(AstNode node)
        {
            if (ExportFinder.IsExportsAssignVoid0(node))
            {
                StopDescending();
                return;
            }

            if (node is AstDot dot)
            {
                StopDescending();
                if (ExportFinder.IsExports(dot.Expression))
                {
                    if (dot.Property is not string name)
                    {
                        owner._unsupported = true;
                    }
                    else if (Parent() is AstAssign assign && assign.Operator == Operator.Assignment &&
                             assign.Left == node)
                    {
                        if (!owner._named.TryGetValue(name, out var list))
                            owner._named[name] = list = [];
                        list.Add(owner._entries.Count);
                        owner._entries.Add((EntryKind.Named, assign));
                    }
                }
            }

            if (node is AstCall call && ExportFinder.IsExportStarCall(call))
            {
                owner._anyExport.Add(owner._entries.Count);
                owner._entries.Add((EntryKind.ExportStar, call));
            }

            if (node is AstAssign assign2 && assign2.Operator == Operator.Assignment &&
                ExportFinder.IsExports(assign2.Left))
            {
                StopDescending();
                owner._anyExport.Add(owner._entries.Count);
                owner._entries.Add((EntryKind.ModuleExports, assign2));
            }
        }
    }
}
