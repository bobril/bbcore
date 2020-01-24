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

        public AstObject(string? source, Position startLoc, Position endLoc,
            ref StructList<AstObjectProperty> properties) : base(source, startLoc, endLoc)
        {
            Properties.TransferFrom(ref properties);
        }

        public AstObject(string? source, Position startLoc, Position endLoc) : base(source, startLoc, endLoc)
        {
        }

        public AstObject()
        {
            Properties = new StructList<AstObjectProperty>();
        }

        public AstObject(AstNode from): base(from)
        {
        }

        public override void Visit(TreeWalker w)
        {
            base.Visit(w);
            w.WalkList(Properties);
        }

        public override void Transform(TreeTransformer tt)
        {
            base.Transform(tt);
            tt.TransformList(ref Properties);
        }

        public override AstNode ShallowClone()
        {
            var res = new AstObject(Source, Start, End);
            res.Properties.AddRange(Properties.AsReadOnlySpan());
            return res;
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
            return !output.HasParens() && output.FirstInStatement();
        }

        public override object? ConstValue(IConstEvalCtx? ctx = null)
        {
            var allowEvalObjectWithJustConstKeys = ctx?.AllowEvalObjectWithJustConstKeys ?? false;
            var res = new Dictionary<object, object?>();
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
