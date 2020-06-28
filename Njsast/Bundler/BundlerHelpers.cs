using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Njsast.Ast;
using Njsast.Bobril;
using Njsast.Reader;
using Njsast.Utils;

namespace Njsast.Bundler
{
    public static class BundlerHelpers
    {
        public static string GetText(string name)
        {
            using var stream = typeof(BundlerHelpers).Assembly.GetManifestResourceStream(name);
            using var reader = new StreamReader(stream ?? throw new NotImplementedException("Resource missing " + name),
                Encoding.UTF8);
            return reader.ReadToEnd();
        }

        public static string TslibJs = GetText("Njsast.Bundler.JsHeaders.tslib.js");
        public static string ImportJs = GetText("Njsast.Bundler.JsHeaders.import.js");

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
                return new SourceFile(name, toplevel) { WholeExport = symbol };
            }

            var commentListener = new CommentListener();
            toplevel =
                new Parser(new Options { SourceFile = name, OnComment = commentListener.OnComment }, content).Parse();
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
                var sourceFile = new SourceFile(name, toplevel) { WholeExport = symbol, OnlyWholeExport = true };
                new ImportExportTransformer(sourceFile, resolver).Transform(toplevel);
                return sourceFile;
            }
            else
            {
                var sourceFile = new SourceFile(name, toplevel);
                new ImportExportTransformer(sourceFile, resolver).Transform(toplevel);
                return sourceFile;
            }
        }

        public static void AppendToplevelWithRename(AstToplevel main, AstToplevel add, string suffix,
            Action<AstToplevel>? beforeAdd = null)
        {
            if (main.Body.Count == 0)
            {
                beforeAdd?.Invoke(add);
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

            beforeAdd?.Invoke(add);
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

        public static string MakeUniqueName(string name, IReadOnlyDictionary<string, SymbolDef> existing, HashSet<string> nonRootSymbolNames,
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

            return new string(ret.Slice(0, pos));
        }

        public static void SimplifyJavaScriptDependency(AstToplevel jsAst)
        {
            // if (typeof module !== "undefined" && ...)
            if (jsAst.Body.Count > 0 && jsAst.Body[0] is AstIf astIf && astIf.Alternative == null &&
                astIf.Condition is AstBinary astBinary2 && astBinary2.Operator == Operator.LogicalAnd &&
                astBinary2.Left is AstBinary astBinary3 && astBinary3.Operator == Operator.StrictNotEquals
                && astBinary3.Right is AstString astString && astString.Value == "undefined" &&
                astBinary3.Left is AstUnary astUnary
                && astUnary.Operator == Operator.TypeOf &&
                astUnary.Expression.IsSymbolDef().IsGlobalSymbol() == "module")
            {
                jsAst.Body.RemoveAt(0);
                jsAst.FigureOutScope();
            }
            // is just var x = ...;
            if (jsAst.Body.Count == 1 && jsAst.Body[0] is AstVar astVar && astVar.Definitions.Count == 1 && astVar.Definitions[0] is { } astVarDef && astVarDef.Name is AstSymbolVar globalSymbol)
            {
                var globalName = globalSymbol.Name;
                // (function() { ... })()
                if (astVarDef.Value is AstCall astCall && astCall.Args.Count==0 && astCall.Expression is AstFunction astFunction && astFunction.ArgNames.Count==0 && astFunction.Body.Count>2)
                {
                    var body = astFunction.Body;
                    // ends with return x;
                    if (body.Last is AstReturn astReturn && astReturn.Value is AstSymbolRef astSymbolRef &&
                        astSymbolRef.Name == globalName)
                    {
                        // starts with window.x = window.x || ...;
                        if (body[0] is AstSimpleStatement astSimpleStatement && astSimpleStatement.Body is AstAssign astAssign && astAssign.Operator == Operator.Assignment &&
                            IsWindowX(astAssign.Left, globalName) && astAssign.Right is AstBinary astBinary && astBinary.Operator == Operator.LogicalOr && IsWindowX(astBinary.Left, globalName))
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
                   (string) astDot.Property == xName;
        }

        public static void WrapByIIFE(AstToplevel topLevelAst)
        {
            var func = new AstFunction();
            func.ArgNames.Add(new AstSymbolFunarg("undefined"));
            func.HasUseStrictDirective = true;
            func.Body.TransferFrom(ref topLevelAst.Body);
            var call = new AstCall(new AstDot(func, "call"));
            call.Args.AddRef() = new AstThis(null, new Position(), new Position());
            topLevelAst.Body.Add(new AstSimpleStatement(new AstUnaryPrefix(Operator.LogicalNot, call)));
        }

        public static void UnwrapIIFE(AstToplevel topLevelAst)
        {
            if (topLevelAst.Body.Count != 1)
                return;
            var node = topLevelAst.Body[0];
            if (node is AstSimpleStatement simple && simple.Body is AstCall call && call.Args.Count == 0 && call.Expression is AstFunction fnc &&
                fnc.ArgNames.Count == 0)
            {
                topLevelAst.Body.ReplaceItemAt(0, fnc.Body.AsReadOnlySpan());
            }
        }

        public static string FileNameToIdent(string fn)
        {
            if (fn.LastIndexOf('/') >= 0) fn = fn.Substring(fn.LastIndexOf('/') + 1);
            if (fn.IndexOf('.') >= 0) fn = fn.Substring(0, fn.IndexOf('.'));
            fn = fn.Replace('-', '_');
            fn = fn.Replace('<', '_');
            fn = fn.Replace('>', '_');
            return fn;
        }
    }
}
