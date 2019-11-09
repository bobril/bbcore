using System.Collections.Generic;
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
            var mainFunc = (AstFunction) ((AstDot) ((AstCall) ((AstVar) toplevel.Body[0]).Definitions[0].Value!).Expression).Expression;
            mainFunc.Body.InsertRange(^1, code.Body.AsSpan());
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
    }
}
