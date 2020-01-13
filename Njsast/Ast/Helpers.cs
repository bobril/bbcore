using System.Collections.Generic;
using System.Linq;
using Njsast.Reader;
using Njsast.Runtime;

namespace Njsast.Ast
{
    public static class Helpers
    {
        public static AstVar EmitVarDefines(IReadOnlyDictionary<string, object> defines)
        {
            var defs = new StructList<AstVarDef>();
            defs.Reserve((uint) defines.Count);
            foreach (var (name, value) in defines)
            {
                defs.Add(new AstVarDef(new AstSymbolVar(name), TypeConverter.ToAst(value)));
            }

            return new AstVar(ref defs);
        }

        public static (AstToplevel toplevel, AstSymbolVar varContent) EmitVarDefineJson(string json, string? fileName,
            string? varName = null)
        {
            varName ??= "content";
            var parser = new Parser(new Options {SourceFile = fileName}, $"var {varName}={json}");
            var toplevel = parser.Parse();
            return (toplevel, (AstSymbolVar) ((AstVar) toplevel.Body[0]).Definitions[0].Name);
        }

        public static (AstToplevel toplevel, AstSymbolVar varExports) EmitCommonJsWrapper(AstBlock code,
            string? varName = null)
        {
            varName ??= "exports";
            var toplevel = new Parser(new Options(),
                    $"var {varName}=(function(){{ var exports = {{}}; var module = {{ exports: exports }}; var global = this; return module.exports; }}).call(window);")
                .Parse();
            var mainFunc =
                (AstFunction) ((AstDot) ((AstCall) ((AstVar) toplevel.Body[0]).Definitions[0].Value!).Expression)
                .Expression;
            mainFunc.Body.InsertRange(^1, code.Body.AsReadOnlySpan());
            return (toplevel, (AstSymbolVar) ((AstVar) toplevel.Body[0]).Definitions[0].Name);
        }

        public static void RenameSymbol(SymbolDef symbol, string newName)
        {
            symbol.Name = newName;
            foreach (var orig in symbol.Orig)
            {
                orig.Name = newName;
            }

            foreach (var reference in symbol.References)
            {
                reference.Name = newName;
            }
        }

        public static (AstToplevel toplevel, AstSymbolVar? symbol) IfPossibleEmitModuleExportsJsWrapper(
            AstToplevel toplevel,
            string? varName = null)
        {
            varName ??= "exports";
            if (toplevel.Globals!["module"].References.Count != 1)
                return (toplevel, null);
            var tt = new ModuleExportsTreeTransformer(varName);
            toplevel = (AstToplevel) tt.Transform(toplevel);
            return (toplevel, tt.ResultingSymbol);
        }

        public class ModuleExportsTreeTransformer : TreeTransformer
        {
            readonly string _varName;
            public AstSymbolVar? ResultingSymbol;
            bool _shadowedWindow;

            public ModuleExportsTreeTransformer(string varName)
            {
                _varName = varName;
            }

            protected override AstNode? Before(AstNode node, bool inList)
            {
                if (node.IsSymbolDef().IsGlobalSymbol() is {} symbolName)
                {
                    if (symbolName == "global")
                    {
                        foreach (var parent in Parents())
                        {
                            if (parent is AstScope scope)
                            {
                                if (scope.Variables!.ContainsKey("window") || scope.Functions!.ContainsKey("window"))
                                {
                                    _shadowedWindow = true;
                                    return null;
                                }
                            }
                        }

                        return new AstSymbolRef(node, "window");
                    }
                }

                if (node is AstSimpleStatement simpleStatement && simpleStatement.Body is AstAssign assigment &&
                    assigment.Operator == Operator.Assignment)
                {
                    if (assigment.Left is AstDot dot && dot.PropertyAsString == "exports" &&
                        dot.Expression.IsSymbolDef().IsGlobalSymbol() == "module")
                    {
                        var res = new AstVar(node);
                        var newVar = new AstSymbolVar(assigment.Left, _varName);
                        ResultingSymbol = newVar;
                        res.Definitions.Add(new AstVarDef(assigment.Left, newVar, assigment.Right));
                        return Transform(res);
                    }
                }

                return null;
            }

            protected override AstNode? After(AstNode node, bool inList)
            {
                if (node is AstToplevel toplevel && _shadowedWindow)
                {
                    var newVar = new AstVar(toplevel);
                    var name = new AstSymbolVar("global");
                    newVar.Definitions.Add(new AstVarDef(toplevel, name, new AstSymbolRef("window")));
                    toplevel.Body.Insert(0) = newVar;
                }

                return node;
            }
        }
    }
}
