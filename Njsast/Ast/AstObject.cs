using System.Collections.Generic;
using Njsast.ConstEval;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// An object literal
    public class AstObject : AstNode
    {
        /// [AstObjectProperty*] array of properties
        public StructList<AstObjectProperty> Properties;

        public AstObject(Parser parser, Position startLoc, Position endLoc,
            ref StructList<AstObjectProperty> properties) : base(parser, startLoc, endLoc)
        {
            Properties.TransferFrom(ref properties);
        }

        public AstObject()
        {
            Properties = new StructList<AstObjectProperty>();
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.WalkList(Properties);
        }

        public override void CodeGen(OutputContext output)
        {
            if (Properties.Count > 0)
            {
                output.Print("{");
                output.Newline();
                output.Indentation += output.Options.IndentLevel;
                for (var i = 0u; i < Properties.Count; i++)
                {
                    if (i > 0)
                    {
                        output.Print(",");
                        output.Newline();
                    }

                    output.Indent();
                    Properties[i].Print(output);
                }

                output.Newline();
                output.Indentation -= output.Options.IndentLevel;
                output.Indent();
                output.Print("}");
            }
            else
            {
                output.Print("{}");
            }
        }

        public override bool NeedParens(OutputContext output)
        {
            // object literal could need parens, because it would be interpreted as a block of code.
            return output.FirstInStatement();
        }

        public override bool IsConstValue(IConstEvalCtx ctx = null)
        {
            var allowEvalObjectWithJustConstKeys = ctx?.AllowEvalObjectWithJustConstKeys ?? false;
            for (var i = 0u; i < Properties.Count; i++)
            {
                var prop = Properties[i];
                if (!(prop is AstObjectKeyVal keyVal))
                    return false;
                if (!keyVal.Key.IsConstValue(ctx)) return false;
                if (!allowEvalObjectWithJustConstKeys && !keyVal.Value.IsConstValue(ctx)) return false;
            }

            return true;
        }

        public override object ConstValue(IConstEvalCtx ctx = null)
        {
            var allowEvalObjectWithJustConstKeys = ctx?.AllowEvalObjectWithJustConstKeys ?? false;
            var res = new Dictionary<object, object>();
            for (var i = 0u; i < Properties.Count; i++)
            {
                var prop = Properties[i];
                if (!(prop is AstObjectKeyVal keyVal))
                    return null;
                var key = keyVal.Key.ConstValue(ctx?.StripPathResolver());
                if (key == null) return null;
                var val = keyVal.Value.ConstValue(ctx);
                if (val == null && !allowEvalObjectWithJustConstKeys) return null;
                res.Add(key, val);
            }

            return res;
        }
    }
}
