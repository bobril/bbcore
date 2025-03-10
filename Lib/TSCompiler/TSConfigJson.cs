using System.Collections.Generic;

// ReSharper disable InconsistentNaming

namespace Lib.TSCompiler;

public class TSConfigJson
{
    public ITSCompilerOptions? compilerOptions { get; set; }
    public List<string>? files { get; set; }
    public List<string>? include { get; set; }
    public IList<string>? exclude { get; set; }
}