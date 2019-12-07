using Njsast.Ast;

namespace Njsast.Scope
{
    public class ScopeParser
    {
        readonly ScopeOptions _options;

        public ScopeParser(ScopeOptions? options = null)
        {
            _options = options ?? new ScopeOptions();
        }

        public void FigureOutScope(AstToplevel toplevel)
        {
            TreeWalker treeWalker = new SetupScopeChainingAndHandleDefinitionsTreeWalker(_options, toplevel);
            treeWalker.Walk(toplevel);
            treeWalker = new FindBackReferencesAndEvalTreeWalker(_options, toplevel);
            treeWalker.Walk(toplevel);
            _options.BeforeMangling?.Invoke(toplevel);
        }
    }
}
