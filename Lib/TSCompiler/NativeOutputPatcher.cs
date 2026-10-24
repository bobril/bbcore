using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Njsast;
using Njsast.Ast;
using Njsast.Reader;
using Njsast.SourceMap;

namespace Lib.TSCompiler;

// bbcore patches TypeScript emit (see ToolsDir.TypeScriptJsContent). tsrs and TypeScript 7 are native and cannot be
// patched, so the same changes are applied to their CommonJS output here, keeping source map in sync:
// 1. `(0, a.b)(...)` indirect calls are emitted as `a.b(...)` (also needed for bobril-g11n t calls detection), indirect
//    calls already written in source (like `(0, eval)(code)` or prebuilt JavaScript) are kept
// 2. `Object.defineProperty(exports, "__esModule", { value: true });` is not emitted
// 3. live binding reexports get setter, so reexported values can be mocked
// 4. `exports.a = ... = void 0;` initialization is split into chunks of 50 names
// 5. destructured exports `({ a: exports.a, b: exports.b } = f());` are emitted as `_a = f(), exports.a = _a.a, ...`
public static class NativeOutputPatcher
{
    const int ExportedNamesChunkSize = 50;

    public static (string JavaScript, string? SourceMap) Patch(string javaScript, string? sourceMap,
        string source = "")
    {
        AstToplevel toplevel;
        try
        {
            toplevel = new Parser(new Options(), javaScript).Parse();
        }
        catch (SyntaxError)
        {
            // Output with syntax errors is reported by later parsing with proper diagnostics
            return (javaScript, sourceMap);
        }

        var collector = new ModificationCollector(javaScript, source);
        collector.Walk(toplevel);
        collector.CollectTopLevel(toplevel);
        collector.AddTempVariables();
        if (collector.Modifications.Count == 0)
            return (javaScript, sourceMap);

        // Insertions go before removals starting at same position
        collector.Modifications.Sort((a, b) => a.From != b.From ? a.From.CompareTo(b.From) : a.To.CompareTo(b.To));
        var replacer = new SourceReplacer();
        foreach (var m in collector.Modifications)
            replacer.Replace(m.FromLine, m.FromCol, m.ToLine, m.ToCol, m.Content);

        var builder = new SourceMapBuilder();
        var parsedSourceMap = sourceMap != null ? Njsast.SourceMap.SourceMap.Parse(sourceMap, null) : null;
        var adder = builder.CreateSourceAdder(javaScript, parsedSourceMap);
        replacer.Apply(adder);
        return (builder.Content(), parsedSourceMap != null ? builder.Build(".", ".").ToString() : null);
    }

    record struct Modification(int From, int To, int FromLine, int FromCol, int ToLine, int ToCol, string Content);

    sealed class ModificationCollector : TreeWalker
    {
        readonly string _text;
        readonly string _source;
        readonly int[] _lineStarts;
        readonly HashSet<string> _usedNames = new();
        readonly List<string> _tempNames = new();
        int _tempCounter;
        public readonly List<Modification> Modifications = new();

        public ModificationCollector(string text, string source)
        {
            _text = text;
            _source = source;
            var lineStarts = new List<int> { 0 };
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                    lineStarts.Add(i + 1);
            }

            _lineStarts = lineStarts.ToArray();
        }

        protected override void Visit(AstNode node)
        {
            if (node is AstSymbol symbol)
                _usedNames.Add(symbol.Name);
            if (node is AstCall { Expression: AstSequence { Expressions.Count: 2 } sequence } &&
                sequence.Expressions[0] is AstNumber { Value: 0 } zero)
                UnwrapIndirectCall(zero, sequence.Expressions[1]);
        }

        // `(0, a.b)(` -> `a.b(`
        void UnwrapIndirectCall(AstNode zero, AstNode callee)
        {
            var open = zero.Start.Index - 1;
            if (open < 0 || _text[open] != '(') return;
            var close = callee.End.Index;
            while (close < _text.Length && char.IsWhiteSpace(_text[close])) close++;
            if (close >= _text.Length || _text[close] != ')') return;
            if (IsIndirectCallInSource(_text[callee.Start.Index..callee.End.Index])) return;
            Remove(open, callee.Start.Index);
            Remove(close, close + 1);
        }

        bool IsIndirectCallInSource(string callee)
        {
            if (!_source.Contains(callee, StringComparison.Ordinal)) return false;
            return Regex.IsMatch(_source, @"\(\s*0\s*,\s*" + Regex.Escape(callee) + @"\s*\)");
        }

        public void CollectTopLevel(AstToplevel toplevel)
        {
            var constModules = new HashSet<string>();
            var reexports = new List<(AstNode GetFunction, string ExportName, string Module, string Member)>();
            for (var i = 0u; i < toplevel.Body.Count; i++)
            {
                var statement = toplevel.Body[i];
                if (statement is AstConst constDefinitions)
                {
                    for (var j = 0u; j < constDefinitions.Definitions.Count; j++)
                    {
                        if (constDefinitions.Definitions[j].Name is AstSymbol symbol)
                            constModules.Add(symbol.Name);
                    }

                    continue;
                }

                if (statement is not AstSimpleStatement { Body: var body })
                    continue;
                if (IsDefinePropertyOnExports(body, out var exportName, out var descriptor))
                {
                    if (exportName == "__esModule")
                    {
                        RemoveWholeLines(statement);
                    }
                    else if (TryGetLiveBindingGetter(descriptor, out var getFunction, out var module,
                                 out var member))
                    {
                        reexports.Add((getFunction, exportName, module, member));
                    }

                    continue;
                }

                if (body is AstAssign { Operator: Operator.Assignment, Left: AstDestructuring destructuring } assign)
                    PatchExportedDestructuring(statement, destructuring, assign.Right);
                else if (body is AstAssign)
                    SplitExportedNamesInitialization(statement, body);
            }

            foreach (var (getFunction, exportName, module, member) in reexports)
            {
                var setter = new StringBuilder(", set: function (v) { ");
                if (constModules.Contains(module))
                {
                    // Imported binding exported again, TypeScript updates all its exported names on write
                    foreach (var other in reexports)
                    {
                        if (other.Module == module && other.Member == member)
                            setter.Append("exports.").Append(other.ExportName).Append(" = ");
                    }
                }

                setter.Append(module).Append('.').Append(member).Append(" = v; }");
                Insert(getFunction.End.Index, setter.ToString());
            }
        }

        void PatchExportedDestructuring(AstNode statement, AstDestructuring destructuring, AstNode value)
        {
            var accesses = new List<(string ExportName, string Access)>();
            for (var i = 0u; i < destructuring.Names.Count; i++)
            {
                var item = destructuring.Names[i];
                if (destructuring.IsArray)
                {
                    if (item is AstHole) continue;
                    if (!TryGetExportsName(item, out var exportName)) return;
                    accesses.Add((exportName, "[" + i + "]"));
                }
                else
                {
                    if (item is not AstObjectKeyVal { Key: var key, Value: var target } ||
                        !TryGetExportsName(target, out var exportName))
                        return;
                    switch (key)
                    {
                        case AstSymbol symbol:
                            accesses.Add((exportName, "." + symbol.Name));
                            break;
                        case AstString or AstNumber:
                            accesses.Add((exportName, "[" + _text[key.Start.Index..key.End.Index] + "]"));
                            break;
                        default:
                            return;
                    }
                }
            }

            if (accesses.Count == 0) return;
            var prefixEnd = value.Start.Index;
            var suffixStart = value.End.Index;
            var expectedSuffix = destructuring.IsArray ? ";" : ");";
            if (_text[statement.Start.Index] != (destructuring.IsArray ? '[' : '(') ||
                _text[suffixStart..statement.End.Index] != expectedSuffix)
                return;
            if (accesses.Count == 1)
            {
                var parens = value is not (AstCall or AstSymbolRef or AstPropAccess or AstArray or AstObject);
                Replace(statement.Start.Index, prefixEnd, "exports." + accesses[0].ExportName + " = " +
                                                          (parens ? "(" : ""));
                Replace(suffixStart, statement.End.Index, (parens ? ")" : "") + accesses[0].Access + ";");
                return;
            }

            var temp = AllocateTempName();
            var suffix = new StringBuilder();
            foreach (var (exportName, access) in accesses)
                suffix.Append(", exports.").Append(exportName).Append(" = ").Append(temp).Append(access);
            suffix.Append(';');
            Replace(statement.Start.Index, prefixEnd, temp + " = ");
            Replace(suffixStart, statement.End.Index, suffix.ToString());
        }

        static bool TryGetExportsName(AstNode node, out string exportName)
        {
            if (node is AstDot { Expression: AstSymbolRef { Name: "exports" }, PropertyAsString: { } name })
            {
                exportName = name;
                return true;
            }

            exportName = "";
            return false;
        }

        // Same sequence as TypeScript temp variables: _a, _b, ... skipping _i, _n and names used in file
        string AllocateTempName()
        {
            while (true)
            {
                var count = _tempCounter++;
                if (count is 8 or 13) continue;
                var name = count < 26 ? "_" + (char)('a' + count) : "_" + (count - 26);
                if (!_usedNames.Add(name)) continue;
                _tempNames.Add(name);
                return name;
            }
        }

        // `var _a, _b;` goes right after "use strict" prologue
        public void AddTempVariables()
        {
            if (_tempNames.Count == 0) return;
            const string useStrict = "\"use strict\";\n";
            var at = _text.StartsWith(useStrict, StringComparison.Ordinal) ? useStrict.Length : 0;
            Insert(at, "var " + string.Join(", ", _tempNames) + ";\n");
        }

        // `exports.c = exports.b = exports.a = void 0;` with more than 50 names, names are in reverse order
        void SplitExportedNamesInitialization(AstNode statement, AstNode body)
        {
            var names = new List<string>();
            var current = body;
            while (current is AstAssign { Operator: Operator.Assignment } assign &&
                   assign.Left is AstDot { Expression: AstSymbolRef { Name: "exports" } } dot &&
                   dot.PropertyAsString is { } name)
            {
                names.Add(name);
                current = assign.Right;
            }

            if (current is not AstUnaryPrefix { Operator: Operator.Void, Expression: AstNumber { Value: 0 } } ||
                names.Count <= ExportedNamesChunkSize)
                return;
            names.Reverse();
            var text = new StringBuilder();
            for (var i = 0; i < names.Count; i += ExportedNamesChunkSize)
            {
                if (i > 0) text.Append('\n');
                for (var j = Math.Min(i + ExportedNamesChunkSize, names.Count) - 1; j >= i; j--)
                    text.Append("exports.").Append(names[j]).Append(" = ");
                text.Append("void 0;");
            }

            Replace(statement.Start.Index, statement.End.Index, text.ToString());
        }

        static bool IsDefinePropertyOnExports(AstNode body, out string exportName, out AstObject descriptor)
        {
            if (body is AstCall
                {
                    Expression: AstDot
                    {
                        Expression: AstSymbolRef { Name: "Object" }, PropertyAsString: "defineProperty"
                    },
                    Args.Count: 3
                } call && call.Args[0] is AstSymbolRef { Name: "exports" } &&
                call.Args[1] is AstString exportNameString && call.Args[2] is AstObject descriptorObject)
            {
                exportName = exportNameString.Value;
                descriptor = descriptorObject;
                return true;
            }

            exportName = "";
            descriptor = null!;
            return false;
        }

        // `{ enumerable: true, get: function () { return module_1.member; } }` without setter
        static bool TryGetLiveBindingGetter(AstObject descriptor, out AstNode getFunction, out string module,
            out string member)
        {
            getFunction = null!;
            module = "";
            member = "";
            if (descriptor.Properties.Count != 2) return false;
            if (descriptor.Properties[0] is not AstObjectKeyVal { Key: var enumerableKey, Value: AstTrue } ||
                PropertyName(enumerableKey) != "enumerable")
                return false;
            if (descriptor.Properties[1] is not AstObjectKeyVal { Key: var getKey, Value: AstFunction function } ||
                PropertyName(getKey) != "get" || function.ArgNames.Count != 0 || function.Body.Count != 1)
                return false;
            if (function.Body[0] is not AstReturn
                {
                    Value: AstDot { Expression: AstSymbolRef moduleRef, PropertyAsString: { } memberName }
                })
                return false;
            getFunction = function;
            module = moduleRef.Name;
            member = memberName;
            return true;
        }

        static string? PropertyName(AstNode key) => key switch
        {
            AstSymbol symbol => symbol.Name,
            AstString str => str.Value,
            _ => null
        };

        void RemoveWholeLines(AstNode statement)
        {
            var from = _lineStarts[statement.Start.Line];
            var toLine = statement.End.Line + 1;
            var to = toLine < _lineStarts.Length ? _lineStarts[toLine] : _text.Length;
            Replace(from, to, "");
        }

        void Remove(int from, int to) => Replace(from, to, "");

        void Insert(int at, string content) => Replace(at, at, content);

        void Replace(int from, int to, string content)
        {
            var (fromLine, fromCol) = LineCol(from);
            var (toLine, toCol) = LineCol(to);
            Modifications.Add(new(from, to, fromLine, fromCol, toLine, toCol, content));
        }

        (int Line, int Col) LineCol(int index)
        {
            var line = Array.BinarySearch(_lineStarts, index);
            if (line < 0) line = ~line - 1;
            return (line, index - _lineStarts[line]);
        }
    }
}
