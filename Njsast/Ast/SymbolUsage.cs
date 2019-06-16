using System;

namespace Njsast.Ast
{
    [Flags]
    public enum SymbolUsage
    {
        Unknown = 0,
        Read = 1,
        /// Initialization or modification of reference/value
        Write = 2,
        /// Increment, decrement, assignments with reads of original value
        ReadWrite = 3
    }
}