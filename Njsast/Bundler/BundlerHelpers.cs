using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Njsast.Ast;
using Njsast.Bobril;
using Njsast.Reader;
using Njsast.Utils;

namespace Njsast.Bundler;

public static class BundlerHelpers
{
    public static string GetText(string name)
    {
        using var stream = typeof(BundlerHelpers).Assembly.GetManifestResourceStream(name);
        using var reader = new StreamReader(stream ?? throw new NotImplementedException("Resource missing " + name),
            Encoding.UTF8);
        return reader.ReadToEnd();
    }
    
    private static readonly string _assemblyName = Assembly.GetExecutingAssembly().GetName().Name!;
    private static string TslibJs = GetText($"{_assemblyName}.Bundler.JsHeaders.tslib.js");
    private static string ImportJs = GetText($"{_assemblyName}.Bundler.JsHeaders.import.js");

    public static string JsHeaders(bool withImport)
    {
        if (withImport)
            return TslibJs + ImportJs;
        return TslibJs;
    }

    public static SourceFile BuildSourceFile(string name, string content, SourceMap.SourceMap? sourceMap,
        Func<string, string, string> resolver)
    {
        AstToplevel toplevel;
        AstSymbol? symbol;
        if (PathUtils.GetExtension(name) == "json")
        {
            (toplevel, symbol) = Helpers.EmitVarDefineJson(content, name);
            toplevel.FigureOutScope();
            var exports = new StringTrie<AstNode>();
            exports.Add(new(), symbol);
            return new(name, toplevel) { Exports = exports };
        }

        var commentListener = new CommentListener();
        toplevel =
            new Parser(new() { SourceFile = name, OnComment = commentListener.OnComment }, content).Parse();
        commentListener.Walk(toplevel);
        sourceMap?.ResolveInAst(toplevel);
        UnwrapIIFE(toplevel);
        toplevel.FigureOutScope();
        if (toplevel.Globals!.ContainsKey("module"))
        {
            (toplevel, symbol) = Helpers.IfPossibleEmitModuleExportsJsWrapper(toplevel);
            if (symbol == null)
            {
                (toplevel, symbol) = Helpers.EmitCommonJsWrapper(toplevel);
            }

            toplevel.FigureOutScope();
            var exports = new StringTrie<AstNode>();
            exports.Add(new(), symbol);
            var sourceFile = new SourceFile(name, toplevel)
            {
                Exports = exports,
                OnlyWholeExport = true
            };
            sourceFile.Ast = (AstToplevel)new ImportExportTransformer(sourceFile, resolver).Transform(toplevel);
            return sourceFile;
        }
        else
        {
            var sourceFile = new SourceFile(name, toplevel);
            sourceFile.Ast = (AstToplevel)new ImportExportTransformer(sourceFile, resolver).Transform(toplevel);
            return sourceFile;
        }
    }

    public static void AppendToplevelWithRename(AstToplevel main, AstToplevel add, string suffix, HashSet<string> knownDeclaredGlobals,
        Func<AstToplevel, AstToplevel>? beforeAdd = null)
    {
        if (main.Body.Count == 0)
        {
            add = beforeAdd?.Invoke(add) ?? add;
            main.Body.AddRange(add.Body.AsReadOnlySpan());
            main.Variables = add.Variables;
            main.Globals = add.Globals;
            main.NonRootSymbolNames = add.NonRootSymbolNames;
            return;
        }

        var nonRootSymbolNames = main.CalcNonRootSymbolNames();
        nonRootSymbolNames.UnionWith(add.CalcNonRootSymbolNames());
        var renameWalker = new ToplevelRenameWalker(main.Variables!, nonRootSymbolNames, suffix);
        renameWalker.Walk(add);
        nonRootSymbolNames = nonRootSymbolNames.ToHashSet();
        nonRootSymbolNames.ExceptWith(add.Globals!.Keys);
        nonRootSymbolNames.ExceptWith(knownDeclaredGlobals);
        renameWalker = new(new Dictionary<string, SymbolDef>(), nonRootSymbolNames, "m");
        renameWalker.Walk(main);

        add = beforeAdd?.Invoke(add) ?? add;
        main.Body.AddRange(add.Body.AsReadOnlySpan());
        foreach (var (_, symbolDef) in add.Variables!)
        {
            main.Variables!.TryAdd(symbolDef.Name, symbolDef);
        }

        foreach (var (_, symbolDef) in add.Globals!)
        {
            main.Globals!.TryAdd(symbolDef.Name, symbolDef);
        }
    }

    public static string MakeUniqueName(string name, IReadOnlyDictionary<string, SymbolDef> existing,
        HashSet<string> nonRootSymbolNames,
        string? suffix)
    {
        if (!existing.ContainsKey(name) && !nonRootSymbolNames.Contains(name)) return name;
        var prefix = suffix != null ? name + suffix : name;
        string newName;
        var index = suffix == null ? 1 : 0;
        do
        {
            index++;
            newName = prefix;
            if (index > 1) newName += index.ToString();
        } while (existing.ContainsKey(newName) || nonRootSymbolNames.Contains(newName));

        return newName;
    }

    public static string NumberToIdent(int num)
    {
        Span<char> ret = stackalloc char[8];
        var pos = 0;
        var @base = 54;
        num++;
        do
        {
            num--;
            num = Math.DivRem(num, @base, out var rem);
            ret[pos++] = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ$_0123456789"[rem];
            @base = 64;
        } while (num > 0);

        return new(ret[..pos]);
    }

    public static void SimplifyJavaScriptDependency(AstToplevel jsAst)
    {
        // if (typeof module !== "undefined" && ...)
        if (jsAst.Body.Count > 0 && jsAst.Body[0] is AstIf
            {
                Alternative: null, Condition: AstBinary
                {
                    Operator: Operator.LogicalAnd, Left: AstBinary
                    {
                        Operator: Operator.StrictNotEquals, Right: AstString
                        {
                            Value: "undefined"
                        },
                        Left: AstUnary { Operator: Operator.TypeOf } astUnary
                    }
                }
            } && astUnary.Expression.IsSymbolDef().IsGlobalSymbol() == "module")
        {
            jsAst.Body.RemoveAt(0);
            jsAst.FigureOutScope();
        }

        // is just var x = ...;
        if (jsAst.Body.Count == 1 && jsAst.Body[0] is AstVar { Definitions.Count: 1 } astVar && astVar.Definitions[0] is
            {
                Name: AstSymbolVar globalSymbol
            } astVarDef)
        {
            var globalName = globalSymbol.Name;
            // (function() { ... })()
            if (astVarDef.Value is AstCall
                {
                    Args.Count: 0, Expression: AstFunction { ArgNames.Count: 0, Body.Count: > 2 } astFunction
                })
            {
                var body = astFunction.Body;
                // ends with return x;
                if (body.Last is AstReturn { Value: AstSymbolRef astSymbolRef } astReturn &&
                    astSymbolRef.Name == globalName)
                {
                    // starts with window.x = window.x || ...;
                    if (body[0] is AstSimpleStatement { Body: AstAssign { Operator: Operator.Assignment } astAssign } &&
                        IsWindowX(astAssign.Left, globalName) && astAssign.Right is AstBinary
                        {
                            Operator: Operator.LogicalOr
                        } astBinary && IsWindowX(astBinary.Left, globalName))
                    {
                        astVarDef.Value = astAssign;
                        body[0] = astVar;
                        body.Last = new AstSimpleStatement(new AstAssign(astAssign.Left.DeepClone(),
                            astReturn.Value));
                        jsAst.Body.TransferFrom(ref body);
                        jsAst.FigureOutScope();
                    }
                }
            }
        }
    }

    // Check for pattern window.x
    static bool IsWindowX(AstNode node, string xName)
    {
        return node is AstDot astDot && astDot.Expression.IsSymbolDef().IsGlobalSymbol() == "window" &&
               (string)astDot.Property == xName;
    }

    public static void WrapByIIFE(AstToplevel topLevelAst, bool es6 = false)
    {
        if (es6)
        {
            AstLambda func = new AstArrow();
            func.ArgNames.Add(new AstSymbolFunarg("undefined"));
            func.Body.TransferFrom(ref topLevelAst.Body);
            var call = new AstCall(func);
            topLevelAst.Body.Add(new AstSimpleStatement(call));
        }
        else
        {
            AstLambda func = new AstFunction();
            func.ArgNames.Add(new AstSymbolFunarg("undefined"));
            func.HasUseStrictDirective = true;
            func.Body.TransferFrom(ref topLevelAst.Body);
            var call = new AstCall(new AstDot(func, "call"));
            call.Args.AddRef() = new AstThis(null, new(), new());
            topLevelAst.Body.Add(new AstSimpleStatement(new AstUnaryPrefix(Operator.LogicalNot, call)));
        }
    }

    public static void UnwrapIIFE(AstToplevel topLevelAst)
    {
        if (topLevelAst.Body.Count != 1)
            return;
        var node = topLevelAst.Body[0];
        if (node is AstSimpleStatement
            {
                Body: AstCall
                {
                    Args.Count: 0, Expression: AstLambda
                    {
                        ArgNames.Count: 0, IsGenerator: false, Async: false
                    } fnc
                }
            })
        {
            topLevelAst.Body.ReplaceItemAt(0, fnc.Body.AsReadOnlySpan());
        }
    }

    public static string FileNameToIdent(string fn)
    {
        if (fn.LastIndexOf('/') >= 0) fn = fn[(fn.LastIndexOf('/') + 1)..];
        if (fn.Contains('.')) fn = fn[..fn.IndexOf('.')];
        fn = fn.Replace('-', '_');
        fn = fn.Replace('<', '_');
        fn = fn.Replace('>', '_');
        return fn;
    }
}
