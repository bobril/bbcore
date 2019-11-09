using Njsast.Ast;

namespace Njsast.Compress
{
    public abstract class UnreachableAfterJumpCodeEliminationTreeTransformerBase<TRootNodeType, TJumpNodeType> : CompressModuleTreeTransformerBase where TRootNodeType : AstStatement where TJumpNodeType : AstJump
    {
        protected bool IsProcessingSwitchStatement { get; set; }
        protected bool IsAfterJump { get; set; }
        bool IsProcessingRootNode { get; set; }
        
        AstCatch? _lastTryCatch;
        AstFinally? _lastTryFinally;
        AstNode? _lastIfAlternative;
        AstNode? _lastCaseExpression;

        protected UnreachableAfterJumpCodeEliminationTreeTransformerBase(ICompressOptions options) : base(options)
        {
        }

        protected override AstNode? Before(AstNode node, bool inList)
        {
            if (IsProcessingSwitchStatement)
            {
                switch (node)
                {
                    case AstCase astCase:
                        return ProcessSwitchCaseNode(astCase);
                    case AstDefault astDefault:
                        return ProcessSwitchDefaultNode(astDefault);
                }
            }

            if (IsLastIfAlternative(node) || IsLastTryCatchArgname(node))
                return node;
            
            
            if (node is TRootNodeType rootNode)
                return ProcessRootNode(rootNode);
            
            if (IsLastCaseExpression(node)) 
                IsAfterJump = false;

            if (IsLastTryCatch(node) || IsLastTryFinally(node))
                return ProcessCatchOrFinally(node);
            
            if (IsAfterJump)
                return TryRemoveNode(node);
            
            switch (node)
            {
                case AstIf astIf:
                    return ProcessIfStatement(astIf);
                case AstSwitch astSwitch:
                    return ProcessSwitch(astSwitch);
                case AstTry astTry:
                    return ProcessTry(astTry);
                case AstStatementWithBody astStatementWithBody:
                    return ProcessStatementWithBody(astStatementWithBody);
                case TJumpNodeType jumpNode:
                    return ProcessJumpNode(jumpNode);
                default:
                    return null;
            }
        }

        protected override bool CanProcessNode(ICompressOptions options, AstNode node)
        {
            return options.EnableUnreachableCodeElimination && node is TRootNodeType;
        }
        
        protected override AstNode? After(AstNode node, bool inList)
        {
            return null;
        }

        protected virtual TRootNodeType ProcessRootNode(TRootNodeType node)
        {
            if (IsProcessingRootNode)
                return node;
            IsProcessingRootNode = true;
            Descend();
            IsProcessingRootNode = false;
            return node;
        }

        protected virtual TJumpNodeType ProcessJumpNode(TJumpNodeType node)
        {
            IsAfterJump = true;
            return node;
        }

        protected override void ResetState()
        {
            base.ResetState();
            IsAfterJump = false;
            _lastTryCatch = null;
            _lastTryFinally = null;
            _lastIfAlternative = null;
            _lastCaseExpression = null;
        }

        protected AstNode TryRemoveNode(AstNode node)
        {
            switch (node)
            {
                case AstLambda _:
                    return node;
                case AstDefinitions definitions:
                {
                    foreach (var astVarDef in definitions.Definitions)
                    {
                        // We can safely remove write and value because assignment is preformed after exit
                        ShouldIterateAgain = true;
                        if (astVarDef.Name is AstSymbol symbol)
                        {
                            symbol.Usage = SymbolUsage.Unknown;
                        }
                        astVarDef.Value = null; 
                    }

                    return node;
                }
                default:
                    ShouldIterateAgain = true;
                    return Remove;
            }
        }

        bool IsLastTryCatch(AstNode node)
        {
            return node == _lastTryCatch;
        }

        bool IsLastTryFinally(AstNode node)
        {
            return node == _lastTryFinally;
        }

        bool IsLastTryCatchArgname(AstNode node)
        {
            return node == _lastTryCatch?.Argname;
        }

        bool IsLastIfAlternative(AstNode node)
        {
            return node == _lastIfAlternative;
        }

        bool IsLastCaseExpression(AstNode node)
        {
            return node == _lastCaseExpression;
        }

        AstTry ProcessTry(AstTry astTry)
        {
            var safeLastTryCatch = _lastTryCatch;
            var safeLastTryFinally = _lastTryFinally;
            _lastTryCatch = astTry.Bcatch;
            _lastTryFinally = astTry.Bfinally;
            Descend();
            _lastTryCatch = safeLastTryCatch;
            _lastTryFinally = safeLastTryFinally;
            return astTry;
        }

        AstIf ProcessIfStatement(AstIf astIf)
        {
            var safeLastIfAlternative = _lastIfAlternative;
            _lastIfAlternative = astIf.Alternative;
            Descend();
            _lastIfAlternative = safeLastIfAlternative;
            IsAfterJump = false;
            return astIf;
        }

        AstNode ProcessCatchOrFinally(AstNode catchOrFinallyNode)
        {
            var safeIsAfterJump = IsAfterJump;
            IsAfterJump = false;
            Descend();
            IsAfterJump = safeIsAfterJump;
            return catchOrFinallyNode;
        }

        AstCase ProcessSwitchCaseNode(AstCase astCase)
        {
            var safeLastCaseExpression = _lastCaseExpression;
            var safeIsAfterLoopControl = IsAfterJump;
            _lastCaseExpression = astCase.Expression;
            Descend();
            _lastCaseExpression = safeLastCaseExpression;
            IsAfterJump = safeIsAfterLoopControl;
            return astCase;
        }

        AstDefault ProcessSwitchDefaultNode(AstDefault astDefault)
        {
            var safeIsAfterLoopControl = IsAfterJump;
            Descend();
            IsAfterJump = safeIsAfterLoopControl;
            return astDefault;
        }

        protected virtual AstSwitch ProcessSwitch(AstSwitch astSwitch)
        {
            var safeIsProcessingSwitch = IsProcessingSwitchStatement;
            IsProcessingSwitchStatement = true;
            Descend();
            IsProcessingSwitchStatement = safeIsProcessingSwitch;
            return astSwitch;
        }

        AstStatementWithBody ProcessStatementWithBody(AstStatementWithBody astStatementWithBody)
        {
            Descend();
            IsAfterJump = false;
            return astStatementWithBody;
        }
    }
}