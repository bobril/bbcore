using System.Collections.Generic;

namespace Njsast.Scope
{
    public class ScopeOptions
    {
        public bool KeepFunctionNames = false;
        public bool KeepClassNames = true;
        public bool TopLevel;
        public bool IgnoreEval;
        public HashSet<string> Reserved = new HashSet<string>();

        public ScopeOptions()
        {
            Reserved.Add("arguments");
        }

        // More like context
        public char[] Chars;
    }
}
