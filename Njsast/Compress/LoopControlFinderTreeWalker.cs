using Njsast.Ast;
using Njsast.Reader;

namespace Njsast.Compress
{
    public class LoopControlFinderTreeWalker : TreeWalker
    {
        protected override void Visit(AstNode node)
        {
            switch (node)
            {
                case AstLambda _:
                    // any nested functions does not need to be visited
                    StopDescending();
                    break;
                case AstIterationStatement iterationStatement:
                    iterationStatement.HasBreak = false;
                    iterationStatement.HasContinue = false;
                    break;
                case AstBreak astBreak when astBreak.Label == null:
                {
                    var parent = FindParent<AstIterationStatement, AstSwitch>();
                    if (parent is AstIterationStatement iteration)
                    {
                        iteration.HasBreak = true;
                        return;
                    }

                    if (parent != null)
                    {
                        return;
                    }

                    throw new SyntaxError("break must be inside loop or switch", node.Start);
                }
                case AstContinue astContinue when astContinue.Label == null:
                {
                    var parent = FindParent<AstIterationStatement>();
                    if (parent == null) 
                        throw new SyntaxError("continue must be inside loop", node.Start);
                    parent.HasContinue = true;
                    return;
                }
                case AstLoopControl astLoopControl:
                    var label = astLoopControl.Label?.Thedef;
                    var upToScope = label?.Scope;
                    foreach (var parent in Parents())
                    {
                        if (parent is AstIterationStatement iterationStatement)
                        {
                            if (astLoopControl is AstBreak)
                                iterationStatement.HasBreak = true;
                            else
                                iterationStatement.HasContinue = true;
                        }

                        if (parent == upToScope) break;
                    }

                    break;
            }
        }
    }
}
