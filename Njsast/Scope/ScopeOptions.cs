using System;
using System.Collections.Generic;
using Njsast.Ast;

namespace Njsast.Scope
{
    public class ScopeOptions
    {
        public bool KeepFunctionNames = false;
        public bool KeepClassNames = true;
        public bool FrequencyCounting = true;
        public bool TopLevel;
        public bool IgnoreEval;
        public HashSet<string> Reserved = new HashSet<string>();
        public Action<AstNode>? BeforeMangling = null;
        internal HashSet<uint> ReservedOrIdentifier = new HashSet<uint>();

        public ScopeOptions()
        {
            Reserved.Add("arguments");
        }

        // More like context
        public char[] Chars = new char[0];
    }
}
