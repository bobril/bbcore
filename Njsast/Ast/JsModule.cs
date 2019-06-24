using System;

namespace Njsast.Ast
{
    public class JsModule : IEquatable<JsModule>
    {
        public string ImportedFrom;
        public string Name;
        public override bool Equals(object obj)
        {
            return Equals(obj as JsModule);
        }

        public bool Equals(JsModule other)
        {
            return other != null &&
                   ImportedFrom == other.ImportedFrom &&
                   Name == other.Name;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ImportedFrom, Name);
        }
    }
}
