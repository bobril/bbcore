using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast
{
    /// An ES6 concise method inside an object or class
    public class AstConciseMethod : AstObjectProperty
    {
        /// is this method static (classes only)
        public bool Static;

        /// is this a generator method
        public bool IsGenerator;

        /// is this method async
        public bool Async;

        public AstConciseMethod(string? source, Position startLoc, Position endLoc, AstNode key, AstNode value,
            bool @static, bool isGenerator, bool async) : base(source, startLoc, endLoc, key, value)
        {
            Static = @static;
            IsGenerator = isGenerator;
            Async = async;
        }

        public override void DumpScalars(IAstDumpWriter writer)
        {
            base.DumpScalars(writer);
            writer.PrintProp("Static", Static);
            writer.PrintProp("IsGenerator", IsGenerator);
            writer.PrintProp("Async", Async);
        }

        public override AstNode ShallowClone()
        {
            return new AstConciseMethod(Source, Start, End, Key, Value, Static, IsGenerator, Async);
        }

        public override void CodeGen(OutputContext output)
        {
            string? type = null;
            if (IsGenerator && Async)
            {
                type = "async*";
            }
            else if (IsGenerator)
            {
                type = "*";
            }
            else if (Async)
            {
                type = "async";
            }

            PrintGetterSetter(output, type, Static);
        }
    }
}
