using Njsast.Ast;

namespace Njsast.Compress
{
    public class DeclarationCollectorTreeWalker : TreeWalker
    {
        StructList<AstStatement> _declarations = new StructList<AstStatement>();
        protected override void Visit(AstNode node)
        {
            if (node is AstVar astVar)
            {
                _declarations.Add(astVar);
                return;
            }

            if (node is AstLambda astLambda)
            {
                // any nested functions should not be visited
                StopDescending();
                if (astLambda.Name == null)
                    return;
                _declarations.Add(astLambda);
            }
        }

        public AstVar? GetAllDeclarationsAsVar()
        {
            if (_declarations.Count == 0) 
                return null;
            var varDefs = new StructList<AstVarDef>();
            foreach (var astStatement in _declarations)
            {
                if (astStatement is AstVar astVar)
                {
                    foreach (var astVarDef in astVar.Definitions)
                    {
                        if (astVarDef.Name is AstSymbolVar astSymbolVar)
                        {
                            astSymbolVar.Usage = SymbolUsage.Unknown;
                            varDefs.Add(new AstVarDef(astSymbolVar));
                        }
                    }
                }

                if (astStatement is AstLambda astLambda)
                {
                    varDefs.Add(new AstVarDef(new AstSymbolVar(astLambda.Name!)));
                }
            }
            return new AstVar(ref varDefs);
        }
    }
}