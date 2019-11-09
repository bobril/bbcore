using System.Collections.Generic;
using Njsast.Ast;

namespace Njsast.Compress
{
    public class NodeFinderTreeWalker<TNode> : TreeWalker where TNode : AstNode
    {
        List<TNode> _foundNodes = new List<TNode>(); 
        protected override void Visit(AstNode node)
        {
            if (node is TNode tNode)
                _foundNodes.Add(tNode);
        }

        public IList<TNode> FindNodes(AstNode start)
        {
            _foundNodes = new List<TNode>();
            Walk(start);
            return _foundNodes;
        }
    }
}