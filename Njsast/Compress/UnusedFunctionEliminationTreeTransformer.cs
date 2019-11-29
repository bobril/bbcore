using System.Linq;
using Njsast.Ast;

namespace Njsast.Compress
{
    public class UnusedFunctionEliminationTreeTransformer : CompressModuleTreeTransformerBase
    {
        public UnusedFunctionEliminationTreeTransformer(ICompressOptions options) : base(options)
        {
        }

        bool _isInBinaryOrVar;

        protected override AstNode? Before(AstNode node, bool inList)
        {
            if (node is AstBinary astBinary)
            {
                DescendBinaryOrVar();

                if (astBinary.Right == Remove)
                {
                    return astBinary.Left; // TODO tree transformer which remove alone symbol refs
                }

                return astBinary;
            }

            if (node is AstVar astVar)
            {
                DescendBinaryOrVar();

                return astVar;
            }

            if (node is AstScope)
            {
                var parent = Parent();
                SymbolDef? symbolDef = null;
                if (parent is AstBinary)
                {
                    if (parent is AstAssign astAssign && astAssign.Left is AstSymbolRef symbolRef)
                    {
                        symbolDef = symbolRef.Thedef!;
                        if (symbolDef.Global && symbolDef.Orig.Where(x => x is AstSymbolDeclaration).ToList().Count == 0)
                            return node;
                    }
                    else
                        return node;
                }

                if (parent is AstVarDef astVarDef)
                {
                    if (astVarDef.Name is AstSymbol symbol)
                    {
                        symbolDef = symbol.Thedef;
                    }
                    else
                        return node;
                }

                if (node is AstLambda astLambda && !(Parent() is AstCall call && call.Expression==node) && (inList || _isInBinaryOrVar))
                {
                    if (symbolDef == null && astLambda.Name == null)
                        return node;

                    if (symbolDef == null)
                        symbolDef = astLambda.Name!.Thedef!;

                    var canBeRemoved = true;
                    var shouldPreserveName = false;
                    var definitionScope = symbolDef.Scope;
                    foreach (var reference in symbolDef.References)
                    {
                        // Referenced in same scope as defined and used differently than writing
                        if (reference.Scope == definitionScope)
                        {
                            if (reference.Usage != SymbolUsage.Write)
                            {
                                canBeRemoved = false;
                                break;
                            }

                            shouldPreserveName = true;
                        }

                        // Used in scope which is not defined by this function
                        if (reference.Scope != definitionScope && !astLambda.IsParentScopeFor(reference.Scope))
                        {
                            canBeRemoved = false;
                            break;
                        }
                    }

                    if (canBeRemoved)
                    {
                        ShouldIterateAgain = true;
                        if (shouldPreserveName && !_isInBinaryOrVar)
                        {
                            var varDefs = new StructList<AstVarDef>();
                            varDefs.Add(new AstVarDef(new AstSymbolVar(symbolDef.Name)));
                            return new AstVar(ref varDefs);
                        }
                        return Remove;
                    }
                }

                Descend();
                return node;
            }

            return null;
        }

        protected override bool CanProcessNode(ICompressOptions options, AstNode node)
        {
            return options.EnableUnusedFunctionElimination && node is AstScope;
        }

        protected override AstNode? After(AstNode node, bool inList)
        {
            return null;
        }

        void DescendBinaryOrVar()
        {
            var safeIsInBinaryOrVar = _isInBinaryOrVar;
            _isInBinaryOrVar = true;
            Descend();
            _isInBinaryOrVar = safeIsInBinaryOrVar;
        }
    }
}
