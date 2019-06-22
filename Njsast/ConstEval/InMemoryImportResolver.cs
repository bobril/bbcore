using System;
using System.Collections.Generic;
using Njsast.Ast;
using Njsast.Utils;

namespace Njsast.ConstEval
{
    public class InMemoryImportResolver : IImportResolver
    {
        readonly Dictionary<string, string> _content = new Dictionary<string, string>();

        public InMemoryImportResolver Add(string name, string content)
        {
            _content.Add(name, content);
            return this;
        }

        public (string fileName, string content) ResolveAndLoad(JsModule module)
        {
            if (module.Name.StartsWith("./", StringComparison.Ordinal) ||
                module.Name.StartsWith("../", StringComparison.Ordinal))
            {
                var fileName = PathUtils.Join(PathUtils.Parent(module.ImportedFrom), module.Name);
                _content.TryGetValue(fileName, out var res);
                return (fileName, res);
            }
            return (null, null);
        }
    }
}