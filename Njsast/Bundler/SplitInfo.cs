using System.Collections.Generic;
using Njsast.Ast;
using Njsast.Utils;

namespace Njsast.Bundler
{
    public class SplitInfo
    {
        public SplitInfo(string fullName)
        {
            FullName = fullName;
            ExportsUsedFromLazyBundles = new Dictionary<AstNode, string>();
            ImportsFromOtherBundles = new Dictionary<AstNode, ImportFromOtherBundle>();
            ExportsAllUsedFromLazyBundles = new Dictionary<string, string>();
            DirectSplitsForcedLazy = new HashSet<SplitInfo>();
            PlainJsDependencies = new OrderedHashSet<string>();
        }
        /// file path
        public string FullName;
        /// shortened output name
        public string? ShortName;
        /// name for __bbb property
        public string? PropName;
        /// __bbb.Value = Key;
        public IDictionary<AstNode,string> ExportsUsedFromLazyBundles;
        /// from split, from file, export name, new AST_SymbolRef
        public IDictionary<AstNode, ImportFromOtherBundle> ImportsFromOtherBundles;
        /// map from fileName lower cased to __bbb property name
        public IDictionary<string,string> ExportsAllUsedFromLazyBundles;
        public ISet<SplitInfo> DirectSplitsForcedLazy;
        public StructList<SplitInfo> ExpandedSplitsForcedLazy;
        public OrderedHashSet<string> PlainJsDependencies;
        public bool IsMainSplit;
    }

    public class ImportFromOtherBundle
    {
        public ImportFromOtherBundle(SplitInfo fromSplit, SourceFile fromFile, string? name)
        {
            FromSplit = fromSplit;
            FromFile = fromFile;
            Name = name;
        }

        public SplitInfo FromSplit;
        public SourceFile FromFile;
        // Name is null when whole module import
        public string? Name;
        public AstSymbolRef? Ref;
    }
}
