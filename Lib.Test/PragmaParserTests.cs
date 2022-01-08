using Lib.TSCompiler;
using Xunit;

namespace Lib.Test;

public class PragmaParserTests
{
    [Fact]
    public void Positive()
    {
        Assert.Equal(
            new[] {"bobril"},
            PragmaParser.ParseIgnoreImportingObsolete("// BBIgnoreObsolete: bobril\nimport * as b from \'bobril\'")
        );
    }

    [Fact]
    public void IgnoreObsoleteMustBeBeforeLastImport()
    {
        Assert.Empty(PragmaParser.ParseIgnoreImportingObsolete("import * as b from \'bobril\'\n// BBIgnoreObsolete: bobril\n"));
    }
}