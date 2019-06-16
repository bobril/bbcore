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

        public string ResolveName(JsModule module)
        {
            if (module.Name.StartsWith("./", StringComparison.Ordinal) ||
                module.Name.StartsWith("../", StringComparison.Ordinal))
            {
                return PathUtils.Join(PathUtils.Parent(module.ImportedFrom), module.Name);
            }
            return null;
            //throw new NotSupportedException("InMemoryImportResolver supports only relative paths " +
            //                                module.ImportedFrom + " " + module.Name);
        }

        public string LoadContent(string fileName)
        {
            _content.TryGetValue(fileName, out var res);
            return res;
        }
    }
}