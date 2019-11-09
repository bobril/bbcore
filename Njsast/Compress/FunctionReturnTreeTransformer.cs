using System;
using System.Collections.Generic;
using Njsast.Ast;

namespace Njsast.Compress
{
    public class FunctionReturnTreeTransformer : CompressModuleTreeTransformerBase
    {
        class ReturnInfo
        {
            public AstBlock ParentBlock { get; }
            public AstReturn ReturnStatement { get; }
            public AstIf? IfStatement { get; }
            public bool IsFollowedByEfficientCode { get; set; }
            public bool IsLoopReturn { get; set; }
            public bool IsInIf => IfStatement != null;
            public int NestedLevel { get; set; }
            
            public ReturnInfo(AstReturn returnStatement, AstBlock parentBlock, AstIf? ifStatement)
            {
                ReturnStatement = returnStatement;
                ParentBlock = parentBlock;
                IfStatement = ifStatement;
            }
        }
        bool _isInFunction;
        bool _isInLoop;
        bool _isInIf;
        bool _isAfterReturn;
        int _nestedLevel;
        List<ReturnInfo> _returnInfos = new List<ReturnInfo>(0);
        ReturnInfo? LastReturnInfo => _returnInfos.Count > 0 ? _returnInfos[^1] : null;
        
        public FunctionReturnTreeTransformer(ICompressOptions options) : base(options)
        {
        }

        protected override AstNode? Before(AstNode node, bool inList)
        {
            if (_isInFunction)
            {
                if (IsNonEfficientCode(node))
                    return node;
                if (node is AstStatement && 
                    !(node is AstReturn) && 
                    typeof(AstBlock) != node.GetType() && 
                    LastReturnInfo != null)
                {
                    LastReturnInfo.IsFollowedByEfficientCode = true;
                }
            }

            if (node is AstLambda astLambda)
            {
                return ProcessAstLambda(astLambda);
            }

            if (node is AstStatementWithBody astStatementWithBody)
            {
                return ProcessStatementWithBody(astStatementWithBody);
            }

            if (node is AstReturn astReturn)
            {
                return ProcessAstReturn(astReturn);
                
            }

            return null;
        }

        protected override void ResetState()
        {
            base.ResetState();
            _isInFunction = false;
            _isInLoop = false;
            _isAfterReturn = false;
            _returnInfos = new List<ReturnInfo>();
            _nestedLevel = 0;
        }

        protected override AstNode? After(AstNode node, bool inList)
        {
            return null;
        }

        protected override bool CanProcessNode(ICompressOptions options, AstNode node)
        {
            return options.EnableFunctionReturnCompress && node is AstLambda;
        }
        
        AstReturn ProcessAstReturn(AstReturn astReturn)
        {
            if (astReturn.Value?.ConstValue() is AstUndefined)
                astReturn.Value = null;

            var returnInfo = new ReturnInfo(astReturn, FindParent<AstBlock>(), _isInIf ? FindParent<AstIf>() : null)
            {
                IsLoopReturn = _isInLoop,
                NestedLevel = _nestedLevel
            };
            _returnInfos.Add(returnInfo);
            _isAfterReturn = true;
            return astReturn;
        }
        
        AstStatementWithBody ProcessStatementWithBody(AstStatementWithBody astStatementWithBody)
        {
            var safeIsAfterReturn = _isAfterReturn;
            var safeIsInLoop = _isInLoop;
            var safeIsInIf = _isInIf;
            _nestedLevel++;
            if (astStatementWithBody is AstIterationStatement)
            {
                _isInLoop = true;
            }

            if (astStatementWithBody is AstIf)
            {
                _isInIf = true;
            }
            
            Descend();
            _isAfterReturn = safeIsAfterReturn;
            _isInLoop = safeIsInLoop;
            _isInIf = safeIsInIf;
            _nestedLevel--;
            return astStatementWithBody;
        }
        
        AstLambda ProcessAstLambda(AstLambda astLambda)
        {
            _isInFunction = true;
            Descend();
            _isInFunction = false;

            if (_returnInfos.Count == 0)
                return astLambda;

            for (var i = 0; i <= _returnInfos.Count - 1; i++)
            {
                var first = _returnInfos[i];
                ReturnInfo? second = null;
                for (var j = i + 1; j < _returnInfos.Count; j++)
                {
                    var current = _returnInfos[j];
                    if (current.NestedLevel >= first.NestedLevel) 
                        continue;
                    second = current;
                    break;
                }
                if (ShouldRemoveFirstReturn(first, second))
                {
                    if (first.IsInIf && first.ParentBlock.Body.IndexOf(first.ReturnStatement) == -1)
                    {
                        if (first.IfStatement!.Body == first.ReturnStatement)
                        {
                            first.IfStatement.Body = new AstEmptyStatement();
                            ShouldIterateAgain = true;
                            continue;
                        }

                        if (first.IfStatement.Alternative == first.ReturnStatement)
                        {
                            first.IfStatement.Alternative = null;
                            ShouldIterateAgain = true;
                            continue;
                        }
                        
                        throw new NotSupportedException();
                    }
                    first.ParentBlock.Body.RemoveItem(first.ReturnStatement);
                    ShouldIterateAgain = true;
                }
            }

            if (astLambda is AstArrow astArrow)
            {
                TryInlineArrowFunction(astArrow);
            }

            return astLambda;
        }

        void TryInlineArrowFunction(AstArrow astArrow)
        {
            if (astArrow.Body.Count == 1 && astArrow.Body.Last is AstReturn astReturn && astReturn.Value != null)
            {
                astArrow.Body[astArrow.Body.Count - 1] = astReturn.Value;
            }
        }

        bool ShouldRemoveFirstReturn(ReturnInfo returnToRemove, ReturnInfo? followedReturn)
        {
            // After return is code with possible side effect
            if (returnToRemove.IsFollowedByEfficientCode) 
                return false;
            // Return is in two different if statements
            if (returnToRemove.IsInIf && 
                followedReturn != null && 
                followedReturn.IsInIf &&
                returnToRemove.IfStatement != followedReturn.IfStatement)
                return false; 
            // Return is placed inside loop 
            if (returnToRemove.IsLoopReturn)
                return false;
            // Second does not exist so at end both returns undefined
            if (followedReturn == null && returnToRemove.ReturnStatement.Value == null) 
                return true;
            // Both returns same value
            if (followedReturn != null &&
                CompressUtils.HasSameReturnValue(returnToRemove.ReturnStatement, followedReturn.ReturnStatement))
                return true;

            return false;
        }

        bool IsNonEfficientCode(AstNode node)
        {
            switch (node)
            {
                case AstLambda _:
                    return true;
                case AstDefinitions definitions:
                {
                    foreach (var astVarDef in definitions.Definitions)
                    {
                        // We check if it is performed only definition without initialization
                        if (astVarDef.Value != null)
                        {
                            return false;
                        }

                        if (astVarDef.Name is AstSymbol symbol &&
                            (symbol.Usage == SymbolUsage.Write || symbol.Usage == SymbolUsage.ReadWrite))
                        {
                            return false;
                        }
                    }
                    return true;
                }
                default:
                    return false;
            }
        }
    }
}