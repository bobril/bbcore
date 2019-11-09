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
            using var reader = new StreamReader(stream ?? throw new NotImplementedException("Resource missing "+name), Encoding.UTF8);
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
            AstSymbol symbol;
            if (PathUtils.GetExtension(name) == "json")
            {
                (toplevel, symbol) = Helpers.EmitVarDefineJson(content, name);
                toplevel.FigureOutScope();
                return new SourceFile(name, toplevel) {WholeExport = symbol};
            }

            var commentListener = new CommentListener();
            toplevel =
                new Parser(new Options {SourceFile = name, OnComment = commentListener.OnComment}, content).Parse();
            commentListener.Walk(toplevel);
            sourceMap?.ResolveInAst(toplevel);
            toplevel.FigureOutScope();
            if (toplevel.Globals!.ContainsKey("module"))
            {
                (toplevel, symbol) = Helpers.EmitCommonJsWrapper(toplevel);
                toplevel.FigureOutScope();
                return new SourceFile(name, toplevel) {WholeExport = symbol};
            }

            var sourceFile = new SourceFile(name, toplevel);
            new ImportExportTransformer(sourceFile, resolver).Transform(toplevel);
            return sourceFile;
        }

        public static void AppendToplevelWithRename(AstToplevel main, AstToplevel add, string suffix, Action<AstToplevel>? beforeAdd = null)
        {
            if (main.Body.Count == 0)
            {
                beforeAdd?.Invoke(add);
                main.Body.AddRange(add.Body.AsSpan());
                main.Variables = add.Variables;
                main.Globals = add.Globals;
                return;
            }
            var renameWalker = new ToplevelRenameWalker(main.Variables!, suffix);
            renameWalker.Walk(add);

            beforeAdd?.Invoke(add);
            main.Body.AddRange(add.Body.AsSpan());
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
            string? suffix)
        {
            if (!existing.ContainsKey(name)) return name;
            var prefix = suffix != null ? name + suffix : name;
            string newName;
            var index = suffix == null ? 1 : 0;
            do
            {
                index++;
                newName = prefix;
                if (index > 1) newName += index.ToString();
            } while (existing.ContainsKey(newName));

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

        public static void WrapByIIFE(AstToplevel topLevelAst)
        {
            var func = new AstFunction();
            func.ArgNames.Add(new AstSymbolFunarg("undefined"));
            func.HasUseStrictDirective = true;
            func.Body.TransferFrom(ref topLevelAst.Body);
            topLevelAst.Body.Add(new AstSimpleStatement(new AstUnaryPrefix(Operator.LogicalNot,new AstCall(func))));
        }

        public static string FileNameToIdent(string fn)
        {
            if (fn.LastIndexOf('/') >= 0) fn = fn.Substring(fn.LastIndexOf('/') + 1);
            if (fn.IndexOf('.') >= 0) fn = fn.Substring(0, fn.IndexOf('.'));
            fn = fn.Replace('-', '_');
            return fn;
        }
    }
}
