using System;
using Njsast.Ast;

namespace Njsast.Compress
{
    public abstract class CompressModuleTreeTransformerBase : TreeTransformer
    {
        readonly ICompressOptions _options;
        
        public bool ShouldIterateAgain { get; protected set; }

        protected virtual void ResetState()
        {
        } 

        protected CompressModuleTreeTransformerBase(ICompressOptions options)
        {
            _options = options;
        }

        protected abstract bool CanProcessNode(ICompressOptions options, AstNode node);

        protected override AstNode? After(AstNode node, bool inList)
        {
            throw new NotSupportedException();
        }

        public new AstNode Transform(AstNode start, bool inList = false)
        {
            ShouldIterateAgain = false;
            if (!CanProcessNode(_options, start))
                return start;
            ResetState();
            return base.Transform(start, inList);
        }
    }
}