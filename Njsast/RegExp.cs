using System;

namespace Njsast
{
    public class RegExp
    {
        public string Pattern = string.Empty;
        public RegExpFlags Flags;

        public static RegExpFlags String2Flags(string mods)
        {
            var res = (RegExpFlags) 0;
            if (mods.Contains('g', StringComparison.Ordinal)) res |= RegExpFlags.GlobalMatch;
            if (mods.Contains('i', StringComparison.Ordinal)) res |= RegExpFlags.IgnoreCase;
            if (mods.Contains('m', StringComparison.Ordinal)) res |= RegExpFlags.Multiline;
            if (mods.Contains('u', StringComparison.Ordinal)) res |= RegExpFlags.Unicode;
            if (mods.Contains('y', StringComparison.Ordinal)) res |= RegExpFlags.Sticky;
            return res;
        }
    }
}
