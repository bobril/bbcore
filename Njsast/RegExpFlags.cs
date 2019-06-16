using System;

namespace Njsast
{
    [Flags]
    public enum RegExpFlags
    {
        /// g
        GlobalMatch = 1,
        /// i
        IgnoreCase = 2,
        /// m
        Multiline = 4,
        /// u
        Unicode = 8,
        /// y
        Sticky = 16,
    }
}