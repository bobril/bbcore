using Microsoft.ClearScript.V8;
using Njsast.Compress;
using Njsast.Output;
using Njsast.Reader;
using Xunit;

namespace Lib.Test;

public class ModernSyntaxRuntimeTests
{
    [Theory]
    [InlineData("return JSON.stringify([null,void 0,0,false,'',NaN,3,{}].map(x=>x===null||x===void 0?'fallback':x));")]
    [InlineData("return JSON.stringify([null,void 0,0,false,'',3,{p:7}].map(x=>x!==null&&x!==void 0?x.p:void 0));")]
    [InlineData("let n=0;return JSON.stringify([null,void 0,{p:7}].map(x=>x===null||x===void 0?void 0:x[(n++,'p')]).concat(n));")]
    [InlineData("let n=0,x;const get=()=>{n++;return {p:7}};const result=(x=get())===null||x===void 0?void 0:x.p;return JSON.stringify([result,n,x.p]);")]
    [InlineData("let n=0,x;const get=()=>{n++;return null};const result=(x=get())!==null&&x!==void 0?x:++n;return JSON.stringify([result,n,x]);")]
    [InlineData("return JSON.stringify([null,void 0,x=>x+1].map(x=>x===null||x===void 0?void 0:x(3)));")]
    [InlineData("return JSON.stringify([null,{p:{m(){return this===this.self},self:null}}].map(x=>{if(x)x.p.self=x.p;return x===null||x===void 0?void 0:x.p.m()}));")]
    [InlineData("let x=null;try{return (x===null||x===void 0?void 0:x.p).q}catch(e){return e.name}")]
    [InlineData("let x={p:function(){'use strict';return this===void 0}};return (x===null||x===void 0?void 0:x.p)();")]
    [InlineData("let x={p:7};const r=delete(x===null||x===void 0?void 0:x.p);return JSON.stringify([r,x.p]);")]
    [InlineData("let x={tag:function(){'use strict';return this===void 0}};return (x===null||x===void 0?void 0:x.tag)`hi`;")]
    [InlineData("let x=function(){this.p=7};return new (x===null||x===void 0?void 0:x)().p;")]
    [InlineData("let n=0;return JSON.stringify([null,void 0,0,false,'',3].map(x=>[x||(x=++n),x]).concat(n));")]
    [InlineData("let n=0;return JSON.stringify([null,void 0,0,false,'',3].map(x=>[x&&(x=++n),x]).concat(n));")]
    [InlineData("let n=0;return JSON.stringify([null,void 0,0,false,'',3].map(x=>[x??(x=++n),x]).concat(n));")]
    [InlineData("let n=0;return JSON.stringify([null,0,3].map(x=>[x=x||++n,x=x&&++n,x=x??++n,x]).concat(n));")]
    [InlineData("return JSON.stringify([null,0,3].map(x=>{if(!x)x=4;if(x){x=5}if(x===null||x===void 0)x=6;return x}));")]
    [InlineData("let gets=0,sets=0;const o={get p(){gets++;return 3},set p(v){sets++}};const r=o.p||(o.p=7);return JSON.stringify([r,gets,sets]);")]
    [InlineData("let gets=0,sets=0;const o={get p(){gets++;return 0},set p(v){sets++}};const r=o.p||(o.p=7);return JSON.stringify([r,gets,sets]);")]
    [InlineData("let sets=0;const o={get p(){return 3},set p(v){sets++}};const r=o.p=o.p||7;return JSON.stringify([r,sets]);")]
    [InlineData("let old={},next={p:5};let o={get p(){o=next;return 0},set p(v){old.p=v}};const r=o.p||(o.p=7);return JSON.stringify([r,old,next]);")]
    [InlineData("let n=0;const key={toString(){return ++n===1?'a':'b'}},o={a:0};const r=o[key]||(o[key]=7);return JSON.stringify([r,n,o]);")]
    [InlineData("const c=1;try{return c=c||7}catch(e){return e.name}")]
    [InlineData("const c=1;return c||(c=7);")]
    [InlineData("const c=0;try{return c||(c=7)}catch(e){return e.name}")]
    [InlineData("return (function named(){try{return named=named||7}catch(e){return e.name}})();")]
    [InlineData("let n=0;const o={get x(){return ++n===1?null:{p:7}}};return JSON.stringify([o.x===null||o.x===void 0?void 0:o.x.p,n]);")]
    [InlineData("let x=0;with({get x(){return 3},set x(v){throw Error('setter')}}){return x||(x=7)}")]
    [InlineData("let x=9;function f(eval){let x=7;return eval===null||eval===void 0?void 0:eval('x')}return f(eval);")]
    [InlineData("try{throw 7}catch(e){return eval('e')}")]
    [InlineData("try{throw 7}catch(e){return 42}")]
    [InlineData("try{try{throw null}catch({p}){return 42}}catch(e){return e.name}")]
    [InlineData("var e=2;try{throw 7}catch(e){e=3}return e;")]
    [InlineData("let x=0,y=3;return ((x!==null&&x!==void 0?x:y)||y)+(x===null||x===void 0?y&&7:x);")]
    [InlineData("let x=null;return x===null||x===void 0?void 0:x(...[1,2],3);")]
    [InlineData("let __proto__={value:7};const o={__proto__:__proto__};return JSON.stringify([o.value,Object.hasOwn(o,'__proto__')]);")]
    [InlineData("let __proto__={value:7};const o={__proto__};return JSON.stringify([o.value,Object.hasOwn(o,'__proto__'),o.__proto__.value]);")]
    [InlineData("const o={['__proto__']:{value:7}};return JSON.stringify([o.value,Object.hasOwn(o,'__proto__'),o.__proto__.value]);")]
    [InlineData("function f(o){var args=arguments;Object.defineProperty(o,'p',{get(){args[0]={p:2};return 0}});o.p||(o.p=7);return args[0].p}return f({});")]
    [InlineData("let n=0;for(const x of [null,void 0,{p(){n++}}]){x===null||x===void 0||x.p();x!==null&&x!==void 0&&x.p();if(x!==null&&x!==void 0)x.p()}return n;")]
    [InlineData("let n=0;for(const x of [null,void 0,{p(){n++;return false}}]){(x===null||x===void 0||x.p())||n++;(x!==null&&x!==void 0&&x.p())&&n++}return n;")]
    public void PreservesValuesSideEffectsAndExceptions(string body)
    {
        var source = "(function(){" + body + "})()";
        using var engine = new V8ScriptEngine();
        var expected = engine.Evaluate(source);
        foreach (var options in new ICompressOptions[]
                 { new CompressOptions { MaxPasses = 1 }, CompressOptions.FastDefault })
        {
            var top = Parser.Parse(source).Compress(options, null, 2021);
            var outputOptions = new OutputOptions { Ecma = 2021 };
            var compressed = top.PrintToString(outputOptions);
            Parser.Parse(compressed, new Options { EcmaVersion = 2021 });
            Assert.Equal(expected, engine.Evaluate(compressed));
            top.Mangle(outputOptions: outputOptions);
            var mangled = top.PrintToString(outputOptions);
            Parser.Parse(mangled, new Options { EcmaVersion = 2021 });
            Assert.Equal(expected, engine.Evaluate(mangled));
        }
    }
}
