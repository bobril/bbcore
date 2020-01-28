using System.Globalization;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// A key: value object property
    public class AstObjectKeyVal : AstObjectProperty
    {
        public AstObjectKeyVal(string? source, Position startLoc, Position endLoc, AstNode key, AstNode value) : base(
            source, startLoc, endLoc, key, value)
        {
        }

        public AstObjectKeyVal(AstNode key, AstNode value) : base(key, value)
        {
        }

        public override AstNode ShallowClone()
        {
            return new AstObjectKeyVal(Source, Start, End, Key, Value);
        }

        public override void CodeGen(OutputContext output)
        {
            string GetName(AstSymbol symbol)
            {
                return symbol.Thedef?.MangledName ?? symbol.Thedef?.Name ?? symbol.Name;
            }

            var allowShortHand = output.Options.Shorthand;
            var keyString = Key switch
            {
                AstString str => str.Value,
                AstNumber num => num.Value.ToString("R", CultureInfo.InvariantCulture),
                AstSymbol key => GetName(key),
                _ => null
            };

            if (allowShortHand &&
                Value is AstSymbol && keyString != null &&
                GetName((AstSymbol) Value) == keyString &&
                OutputContext.IsIdentifierString(keyString) &&
                OutputContext.IsIdentifier(keyString)
            )
            {
                output.PrintPropertyName(keyString);
            }
            else if (allowShortHand &&
                     Value is AstDefaultAssign defAssign && keyString != null &&
                     defAssign.Left is AstSymbol &&
                     OutputContext.IsIdentifierString(keyString) &&
                     GetName((AstSymbol) defAssign.Left) == keyString
            )
            {
                output.PrintPropertyName(keyString);
                output.Space();
                output.Print("=");
                output.Space();
                defAssign.Right.Print(output);
            }
            else
            {
                if (keyString != null)
                {
                    output.PrintPropertyName(keyString);
                }
                else
                {
                    output.Print("[");
                    Key.Print(output);
                    output.Print("]");
                }

                output.Colon();
                Value.Print(output);
            }
        }
    }
}
