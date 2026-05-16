using Njsast.Ast;
using Njsast.Reader;
using Xunit;

namespace Test.Reader;

public class AstExtensionsTest
{
    [Theory]
    [InlineData(@"Promise.resolve().then(function() {
                return require(""./lib"");
            })", "./lib")]
    [InlineData(@"Wrong.resolve().then(function() {
                return require(""./lib"");
            })", null)]
    [InlineData(@"Promise.wrong().then(function() {
                return require(""./lib"");
            })", null)]
    [InlineData(@"Promise.resolve().catch(function() {
                return require(""./lib"");
            })", null)]
    [InlineData(@"Promise.resolve().then(function() {
                return wrong(""./lib"");
            })", null)]
    [InlineData(@"Promise.resolve().then(function(a) {
                return require(""./lib"");
            })", null)]
    [InlineData(@"import(""./yes"")", "./yes")]
    public void IsLazyImportDetection(string input, string? result)
    {
        var toplevel = new Parser(new Options { SourceType = SourceType.Module }, input).Parse();
        toplevel.FigureOutScope();
        Assert.Equal(result, ((AstSimpleStatement) toplevel.Body[0]).Body.IsLazyImportCall());
    }
}