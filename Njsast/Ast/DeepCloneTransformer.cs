using System;

namespace Njsast.Ast
{
    public class DeepCloneTransformer: TreeTransformer
    {
        protected override AstNode? Before(AstNode node, bool inList)
        {
            var clone = node.ShallowClone();
#if DEBUG
            if (clone.GetType()!=node.GetType())
                throw new InvalidOperationException("Clone must be identical type");
#endif
            clone.Transform(this);
            return clone;
        }

        protected override AstNode? After(AstNode node, bool inList)
        {
            return null;
        }
    }
}
