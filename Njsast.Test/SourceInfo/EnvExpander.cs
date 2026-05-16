using System.Collections.Generic;
using Njsast.Ast;
using Njsast.Bobril;
using Njsast.Reader;
using Xunit;

namespace Test.SourceInfo;

public class EnvExpander
{
    [Fact]
    public void NontrivialExample()
    {
        var program = Parser.Parse("DEBUG?\"debug\":env.BBVERSION");
        var clone = program.DeepClone();
        clone.FigureOutScope();
        var consts = new Dictionary<string, AstNode>();
        consts["DEBUG"] = AstTrue.Instance;
        clone = (AstToplevel) new EnvExpanderTransformer(consts, name => name == "BBVERSION" ? "42" : null, null)
            .Transform(clone);
        Assert.Equal("debug", (clone.Body.Last as AstSimpleStatement)?.Body.ConstValue());
        clone = program.DeepClone();
        clone.FigureOutScope();
        consts["DEBUG"] = AstFalse.Instance;
        clone = (AstToplevel) new EnvExpanderTransformer(consts, name => name == "BBVERSION" ? "42" : null, null)
            .Transform(clone);
        Assert.Equal("42", (clone.Body.Last as AstSimpleStatement)?.Body.ConstValue());
    }
}