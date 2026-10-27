using Microsoft.ClearScript.V8;
using Njsast.Compress;
using Njsast.Reader;
using Xunit;

namespace Lib.Test;

public class NjsastCompressionTests
{
    [Theory]
    [InlineData("function even(n){return n===0||odd(n-1)}function odd(n){return n!==0&&even(n-1)}return even(8);")]
    [InlineData("const even=n=>n===0||odd(n-1);const odd=n=>n!==0&&even(n-1);return odd(7);")]
    [InlineData("function deadA(){return deadB()}function deadB(){return deadA()}return 42;")]
    [InlineData("const deadA=()=>deadB();const deadB=()=>deadA();return 42;")]
    [InlineData("function outer(){function inner(){return value}return inner}const value=7;return outer()();")]
    [InlineData("function value(){return 3}function outer(){function value(){return 8}return value}return value()+outer()();")]
    [InlineData("const factorial=function recurse(n){return n<2?1:n*recurse(n-1)};return factorial(5);")]
    [InlineData("function first(){return second()}function second(){return 7}first=()=>4;return first();")]
    [InlineData("function hidden(){return 9}return eval('hidden()');")]
    [InlineData("var hidden=function(){return 9};return eval('hidden()');")]
    [InlineData("function hidden(){return 9}with({hidden:()=>6}){return hidden()}")]
    [InlineData("let count=0;const dead=()=>1,result=++count;return count;")]
    [InlineData("let total=0;for(let f=()=>3,i=0;i<2;i++){total+=f()}return total;")]
    [InlineData("const call=()=>later();const later=()=>5;return call();")]
    [InlineData("try{return call();const call=()=>4}catch(e){return e.name}")]
    [InlineData("const object={call:()=>helper()};function helper(){return 6}return object.call();")]
    [InlineData("function call(arg=helper()){return arg}function helper(){return 6}return call();")]
    [InlineData("let calls=0;if(++calls)function deadA(){return deadB()}function deadB(){return deadA()}return calls;")]
    [InlineData("if(true)function live(){return 7}return live();")]
    public void CompressionAndManglingPreserveFunctionBehavior(string body)
    {
        var source = "(function(){" + body + "})()";
        using var engine = new V8ScriptEngine();
        var expected = engine.Evaluate(source);
        var top = Parser.Parse(source).Compress(CompressOptions.FastDefault);
        Assert.Equal(expected, engine.Evaluate(top.PrintToString()));
        top.Mangle();
        Assert.Equal(expected, engine.Evaluate(top.PrintToString()));
    }
}
