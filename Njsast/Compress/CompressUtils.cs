using System;
using Njsast.Ast;

namespace Njsast.Compress
{
    public static class CompressUtils
    {
        public static bool HasSameReturnValue(AstReturn returnA, AstReturn returnB)
        {
            // Both return undefined
            if (returnA.Value == null && returnB.Value == null)
                return true;
            // Only one of them is undefined
            if (returnA.Value == null || returnB.Value == null)
                return false;
            // Both is SymbolRef
            if (returnA.Value is AstSymbolRef symbolA && returnB.Value is AstSymbolRef symbolB)
                return IsSameReference(symbolA, symbolB);
            // Both is Call
            if (returnA.Value is AstCall callA && returnB.Value is AstCall callB)
                return IsSameCall(callA, callB);
            // Both is constants with same value
            var constReturnA = returnA.Value.ConstValue();
            var constReturnB = returnB.Value.ConstValue();
            return constReturnA != null && constReturnB != null && constReturnA.Equals(constReturnB);
        }

        static bool IsSameCall(AstCall callA, AstCall callB)
        {
            // TODO write test for same call check
            if (!(callA.Expression is AstSymbolRef symbolRefA &&
                  callB.Expression is AstSymbolRef symbolRefB) ||
                !IsSameReference(symbolRefA, symbolRefB))
                return false;
            var argsA = TrimEndingUndefined(callA.Args);
            var argsB = TrimEndingUndefined(callB.Args);
            if (argsA.Count != argsB.Count)
                return false;
            for (var i = 0; i < argsA.Count; i++)
            {
                var argA = argsA[i];
                var argB = argsB[i];
                // Constant
                if (argA is AstConstant && argB is AstConstant)
                {
                    return false;
                    // TODO all possible cases => + tests
                }
                // SymbolRef
                if (argA is AstSymbolRef argSymbolRefA &&
                    argB is AstSymbolRef argSymbolRefB)
                {
                    if (IsSameReference(argSymbolRefA, argSymbolRefB))
                        continue;
                    return false;
                }
                // Call
                if (argA is AstCall argCallA &&
                    argB is AstCall argCallB)
                {
                    if (IsSameCall(argCallA, argCallB))
                        continue;
                    return false;
                }

                return false;
                // TODO another cases
            }

            return true;
        }

        static bool IsSameReference(AstSymbolRef symbolRefA, AstSymbolRef symbolRefB)
        {
            return symbolRefA.Thedef == symbolRefB.Thedef;
        }

        static StructList<AstNode> TrimEndingUndefined(StructList<AstNode> list)
        {
            var newList = new StructList<AstNode>(list);
            // TODO remove undefined at end of list
//            for (var i = newList.Count - 1; i >= 0; i--)
//            {
//                var currentNode = newList[i - 1];
//                if (currentNode != undefined) break;
//            }
            return newList;
        }
    }
}
