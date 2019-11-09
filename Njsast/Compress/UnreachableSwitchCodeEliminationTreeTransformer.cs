using System;
using Njsast.Ast;

namespace Njsast.Compress
{
    public class UnreachableSwitchCodeEliminationTreeTransformer : UnreachableAfterJumpCodeEliminationTreeTransformerBase<AstSwitch, AstBreak>
    {
        public UnreachableSwitchCodeEliminationTreeTransformer(ICompressOptions options) : base(options)
        {
            IsProcessingSwitchStatement = true;
        }

        protected override AstNode? Before(AstNode node, bool inList)
        {
            if (node is AstSwitchBranch)
            {
                IsAfterJump = false;
            }
            
            return base.Before(node, inList);
        }

        protected override bool CanProcessNode(ICompressOptions options, AstNode node)
        {
            return options.EnableUnreachableCodeElimination && node is AstSwitch;
        }

        protected override AstSwitch ProcessRootNode(AstSwitch node)
        {
            var astSwitch = base.ProcessRootNode(node);
            if (astSwitch.Body.Count > 0 &&
                astSwitch.Body.Last is AstSwitchBranch astSwitchBranch &&
                astSwitchBranch.Body.Count > 0 &&
                astSwitchBranch.Body.Last is AstBreak)
            {
                ShouldIterateAgain = true;
                astSwitchBranch.Body.RemoveAt(^1);
            }

            return astSwitch;
        }

        protected override AstSwitch ProcessSwitch(AstSwitch astSwitch)
        {
            throw new NotSupportedException();
        }
    }
}
