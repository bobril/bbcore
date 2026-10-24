using System.Collections.Generic;
using Njsast.Ast;

namespace Lib.TSCompiler;

public class TranspileResult
{
    public required string JavaScript;
    public string? SourceMap;
    public List<Diagnostic>? Diagnostics;
    public AstToplevel? Ast;
}
