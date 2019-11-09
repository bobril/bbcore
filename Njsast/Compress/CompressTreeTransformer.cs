using System.Collections.Generic;
using Njsast.Ast;

namespace Njsast.Compress
{
    public class CompressTreeTransformer : TreeTransformer
    {
        bool _shouldIterateAgain;
        readonly IReadOnlyList<CompressModuleTreeTransformerBase> _compressModules; 

        public CompressTreeTransformer(ICompressOptions options)
        {
            _compressModules = new List<CompressModuleTreeTransformerBase>
            {
                new EmptyStatementEliminationTreeTransformer(options),
                new BlockEliminationTreeTransformer(options),
                new UnusedFunctionEliminationTreeTransformer(options),
                new UnreachableCodeEliminationTreeTransformer(options),
                new UnreachableFunctionCodeEliminationTreeTransformer(options),
                new FunctionReturnTreeTransformer(options),
                new UnreachableLoopCodeEliminationTreeTransformer(options),
                new UnreachableSwitchCodeEliminationTreeTransformer(options),
                new VariableHoistingTreeTransformer(options),
                new BooleanConstantsTreeTransformer(options)
            };
        }


        protected override AstNode? Before(AstNode node, bool inList)
        {
            var transformed = node;

            foreach (var compressModuleTreeTransformer in _compressModules)
            {
                transformed = compressModuleTreeTransformer.Transform(transformed, inList);
                _shouldIterateAgain = _shouldIterateAgain || compressModuleTreeTransformer.ShouldIterateAgain;
            }

            _shouldIterateAgain = _shouldIterateAgain || transformed != node; 

            return transformed != node ? transformed : null;
        }

        protected override AstNode? After(AstNode node, bool inList)
        {
            return null;
        }

        public AstNode Compress(AstNode start, out bool shouldIterateAgain)
        {
            _shouldIterateAgain = false;
            var transformed = Transform(start);
            shouldIterateAgain = _shouldIterateAgain;
            return transformed;
        }
    }
}