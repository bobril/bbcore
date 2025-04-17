using System.Collections.Generic;
using Njsast.AstDump;
using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;
using Njsast.Runtime;

namespace Njsast.Ast;

/// Base class for property access expressions, i.e. `a.foo` or `a["foo"]`
public abstract class AstPropAccess : AstNode
{
    /// [AstNode] the “container” expression
    public AstNode Expression;

    /// [AstNode|string] the property to access.  For AstDot this is always a plain string, while for AstSub it's an arbitrary AstNode
    public object Property;

    public bool Optional;

    public AstPropAccess(string? source, Position startLoc, Position endLoc, AstNode expression, object property, bool optional) :
        base(source, startLoc, endLoc)
    {
        Expression = expression;
        Property = property;
        Optional = optional;
    }

    protected AstPropAccess(AstNode expression, object property)
    {
        Expression = expression;
        Property = property;
    }

    public string? PropertyAsString
    {
        get
        {
            return Property switch
            {
                string str => str,
                AstString str2 => str2.Value,
                _ => null
            };
        }
    }

    public override void Visit(TreeWalker w)
    {
        base.Visit(w);
        if (Property is AstNode node)
            w.Walk(node);
        w.Walk(Expression);
    }

    public override void Transform(TreeTransformer tt)
    {
        base.Transform(tt);
        if (Property is AstNode node)
            Property = tt.Transform(node);
        Expression = tt.Transform(Expression);
    }

    public override void DumpScalars(IAstDumpWriter writer)
    {
        base.DumpScalars(writer);
        if (Property is string property)
            writer.PrintProp("Property", property);
        writer.PrintProp("Optional", Optional);
    }

    class WalkForParens : TreeWalker
    {
        internal bool Parens;

        protected override void Visit(AstNode node)
        {
            if (Parens || node is AstScope)
            {
                StopDescending();
            }

            if (node is AstCall)
            {
                Parens = true;
                StopDescending();
            }
        }
    }

    public override bool NeedParens(OutputContext output)
    {
        var p = output.Parent();
        if (p is AstNew aNew && aNew.Expression == this)
        {
            // i.e. new (foo.bar().baz)
            //
            // if there's one call into this subtree, then we need
            // parens around it too, otherwise the call will be
            // interpreted as passing the arguments to the upper New
            // expression.
            var walker = new WalkForParens();
            walker.Walk(this);
            return walker.Parens;
        }

        return false;
    }

    class ConstEvalWithPropWritesForbidConstEval : IConstEvalCtx
    {
        readonly IConstEvalCtx? _parent;

        public ConstEvalWithPropWritesForbidConstEval(IConstEvalCtx? parent)
        {
            _parent = parent;
            if (parent != null)
            {
                JustModuleExports = parent.JustModuleExports;
            }
        }

        public string SourceName => _parent?.SourceName ?? "";

        public JsModule? ResolveRequire(string name)
        {
            return _parent?.ResolveRequire(name);
        }

        public object? ConstValue(IConstEvalCtx ctx, JsModule module, object export)
        {
            return _parent?.ConstValue(ctx, module, export);
        }

        public bool AllowEvalObjectWithJustConstKeys => _parent?.AllowEvalObjectWithJustConstKeys ?? false;

        public bool JustModuleExports { get; set; }

        public bool PropWritesForbidConstEval => true;

        public string ConstStringResolver(string str)
        {
            if (_parent == null) return str;
            return _parent.ConstStringResolver(str);
        }

        public IConstEvalCtx StripPathResolver()
        {
            if (_parent == null) return this;
            var res = _parent.StripPathResolver();
            return res.PropWritesForbidConstEval ? res : new ConstEvalWithPropWritesForbidConstEval(res);
        }

        public IConstEvalCtx CreateForSourceName(string sourceName)
        {
            if (_parent == null) return this;
            var res = _parent.CreateForSourceName(sourceName);
            return res.PropWritesForbidConstEval ? res : new ConstEvalWithPropWritesForbidConstEval(res);
        }
    }

    static IConstEvalCtx MakeConstEvalWithPropWritesForbidConstEval(IConstEvalCtx? ctx)
    {
        if (ctx?.PropWritesForbidConstEval == true) return ctx;
        return new ConstEvalWithPropWritesForbidConstEval(ctx);
    }

    public override object? ConstValue(IConstEvalCtx? ctx = null)
    {
        var expr = Expression.ConstValue(MakeConstEvalWithPropWritesForbidConstEval(ctx));
        object? prop = Property;
        if (prop is AstNode node) prop = node.ConstValue(ctx?.StripPathResolver());
        if (prop == null) return null;
        prop = TypeConverter.ToString(prop);
        if (expr is IReadOnlyDictionary<object, object> dict)
        {
            if (dict.TryGetValue(prop, out var res))
                return res;
            return AstUndefined.Instance;
        }

        if (expr is JsModule module && ctx != null)
        {
            return ctx.ConstValue(ctx, module, prop);
        }

        return null;
    }

    public override bool IsStructurallyEquivalentTo(AstNode? with)
    {
        if (with is AstPropAccess withPropAccess)
        {
            return Expression.IsStructurallyEquivalentTo(withPropAccess.Expression) &&
                   (Property is string str && str == (string) withPropAccess.Property ||
                   Property is AstNode node && node.IsStructurallyEquivalentTo((AstNode) withPropAccess.Property));
        }
        return false;
    }

    public override bool IsConstantLike(bool forbidPropWrite)
    {
        return Expression.IsConstantLike(true) && (Property is string || Property is AstNode node && node.IsConstantLike(false));
    }
}
