using System.Collections.Generic;
using Njsast.AstDump;
using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;
using Njsast.Runtime;

namespace Njsast.Ast
{
    /// Base class for property access expressions, i.e. `a.foo` or `a["foo"]`
    public abstract class AstPropAccess : AstNode
    {
        /// [AstNode] the “container” expression
        public AstNode Expression;

        /// [AstNode|string] the property to access.  For AstDot this is always a plain string, while for AstSub it's an arbitrary AstNode
        public object Property;

        public AstPropAccess(string? source, Position startLoc, Position endLoc, AstNode expression, object property) :
            base(source, startLoc, endLoc)
        {
            Expression = expression;
            Property = property;
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
                if (Property is string str)
                    return str;
                if (Property is AstString str2)
                    return str2.Value;
                return null;
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
            if (Property is string)
                writer.PrintProp("Property", (string) Property);
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

        public override object? ConstValue(IConstEvalCtx? ctx = null)
        {
            var expr = Expression.ConstValue(ctx);
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
    }
}
