using System;
using System.Collections.Generic;
using System.Linq;
using Njsast.Ast;
using Njsast.Reader;
using Njsast.Utils;

namespace Njsast.Compress
{
    public class VariableHoistingTreeTransformer : CompressModuleTreeTransformerBase
    {
        class VariableDefinition
        {
            AstBlock ParentBlock { get; }
            AstNode Parent { get; }
            AstVar AstVar { get; }
            int OriginalIndexInVar { get; }
            public AstVarDef AstVarDef { get; }
            public bool CanMoveInitialization { get; }

            public static readonly HashSet<AstBlock> CreatedIfBlocks = new HashSet<AstBlock>();

            public VariableDefinition(AstBlock parentBlock, AstNode parent, AstVar astVar, AstVarDef astVarDef, bool canMoveInitialization, int originalIndexInVar)
            {
                ParentBlock = parentBlock;
                Parent = parent;
                AstVar = astVar;
                CanMoveInitialization = canMoveInitialization;
                OriginalIndexInVar = originalIndexInVar;
                AstVarDef = astVarDef;
            }

            public void TryRemoveVarFromForIn()
            {
                if (Parent is AstForIn astForIn)
                {
                    if (astForIn.Init != AstVar)
                        throw new NotImplementedException();
                    if (AstVar.Definitions.Count > 1)
                        throw new NotSupportedException();

                    astForIn.Init = new AstSymbolRef((AstSymbol)AstVarDef.Name);
                }

                RemoveVarDefFromVar();
            }

            public void ConvertToAssignmentAndInsertBeforeVarNode()
            {
                if (AstVarDef.Value == null)
                    throw new InvalidOperationException("Can not convert to assignment if there is no initial value");
                if (Parent != ParentBlock)
                {
                    if (Parent is AstFor astFor)
                    {
                        if (astFor.Init == AstVar)
                        {
                            if (AstVar.Definitions.Count == 1)
                            {
                                astFor.Init = ConvertVariableDefinitionToAssignStatement(false);
                                RemoveVarDefFromVar();
                                return;
                            }
                            var expressionsPlaceholders = new StructList<AstNode>();
                            expressionsPlaceholders.AddRange(VarDefsWithInitialization());
                            astFor.Init = new AstSequence(null, AstVar.Start, AstVar.End, ref expressionsPlaceholders);
                        }

                        if (astFor.Init is AstSequence astSequence)
                        {
                            astSequence.Expressions.ReplaceItem(AstVarDef, ConvertVariableDefinitionToAssignStatement(false));
                            RemoveVarDefFromVar();
                            return;
                        }
                    }

                    if (Parent is AstIf astIf)
                    {
                        if (astIf.Body == AstVar)
                        {
                            var block = new AstBlock(AstVar);
                            CreatedIfBlocks.Add(block);
                            block.Body.AddRange(VarDefsWithInitialization());
                            block.Body.ReplaceItem(AstVarDef, ConvertVariableDefinitionToAssignStatement());
                            astIf.Body = block;
                            RemoveVarDefFromVar();
                            return;
                        }

                        if (astIf.Alternative == AstVar)
                        {
                            var block = new AstBlock(AstVar);
                            CreatedIfBlocks.Add(block);
                            block.Body.AddRange(VarDefsWithInitialization());
                            block.Body.ReplaceItem(AstVarDef, ConvertVariableDefinitionToAssignStatement());
                            astIf.Alternative = block;
                            RemoveVarDefFromVar();
                            return;
                        }

                        if (astIf.Body is AstBlock bodyBlock &&
                            CreatedIfBlocks.Contains(bodyBlock))
                        {
                            bodyBlock.Body.ReplaceItem(AstVarDef, ConvertVariableDefinitionToAssignStatement());
                            RemoveVarDefFromVar();
                            return;
                        }

                        if (astIf.Alternative is AstBlock alternativeBlock &&
                            CreatedIfBlocks.Contains(alternativeBlock))
                        {
                            alternativeBlock.Body.ReplaceItem(AstVarDef, ConvertVariableDefinitionToAssignStatement());
                            RemoveVarDefFromVar();
                            return;
                        }
                    }
                    throw new NotImplementedException();
                }
                ParentBlock.Body.Insert(ParentBlock.Body.IndexOf(AstVar)) = ConvertVariableDefinitionToAssignStatement();
                RemoveVarDefFromVar();
            }

            public void RemoveVarDefFromVar()
            {
                AstVar.Definitions.RemoveItem(AstVarDef);
                if (AstVar.Definitions.Count == 0 && Parent == ParentBlock)
                {
                    ParentBlock.Body.RemoveItem(AstVar);
                }
            }

            ReadOnlySpan<AstNode> VarDefsWithInitialization()
            {
                return AstVar.Definitions.Where(x => x.Value != null).Cast<AstNode>().ToArray();
            }

            AstNode ConvertVariableDefinitionToAssignStatement(bool wrapToSimpleStatement = true)
            {
                var assignStatement = new AstAssign(
                    null,
                    AstVarDef.Start,
                    AstVarDef.End,
                    new AstSymbolRef((AstSymbol) AstVarDef.Name),
                    AstVarDef.Value!,
                    Operator.Assignment
                );
                return wrapToSimpleStatement ? (AstNode) new AstSimpleStatement(assignStatement) : assignStatement;
            }
        }

        interface IVariableInitialization
        {
            AstNode Parent { get; }
            AstNode Initialization { get; }
        }

        class VariableInitialization : IVariableInitialization
        {
            public AstNode Parent { get; }
            public AstNode Initialization { get; }

            public VariableInitialization(AstNode parent, AstNode initialization)
            {
                Parent = parent;
                Initialization = initialization;
            }
        }

        class NonHoistableVariableInitialization : IVariableInitialization
        {
            public AstNode Parent => throw new InvalidOperationException();

            public AstNode Initialization => throw new InvalidOperationException();

            public static readonly IVariableInitialization Instance = new NonHoistableVariableInitialization();
        }

        class ScopeVariableUsageInfo
        {
            public List<VariableDefinition> Definitions { get; } = new List<VariableDefinition>();
            public bool IsUsedInConditionalStatement { get; set; }
            public int UnknownReferencesCount { get; set; }
            public int ReadReferencesCount { get; set; }
            public int WriteReferencesCount { get; set; }
            public bool IsPossiblyUsedInCall { get; set; }
            public bool IsUsedOnRightSideOfBinary { get; set; }

            public bool CanMoveInitialization =>
                !IsPossiblyUsedInCall &&
                !IsUsedInConditionalStatement &&
                !IsUsedOnRightSideOfBinary &&
                ReadReferencesCount == 0 &&
                UnknownReferencesCount == 0;

            public IVariableInitialization? FirstHoistableInitialization { get; set; }
        }

        bool _isInScope;
        bool _isInConditional;
        bool _isAfterCall;
        bool _canPerformMergeDefAndInit;
        bool _isInRightSideOfBinary;
        int _astVarCount;
        List<string> _variableWriteOrder = new List<string>();
        readonly NodeFinderTreeWalker<AstCall> _callNodeFinderTreeWalker = new NodeFinderTreeWalker<AstCall>();
        readonly NodeFinderTreeWalker<AstSymbolRef> _symbolRefNodeFinderTreeWalker = new NodeFinderTreeWalker<AstSymbolRef>();

        Dictionary<string, ScopeVariableUsageInfo> _scopeVariableUsages =
            new Dictionary<string, ScopeVariableUsageInfo>();

        public VariableHoistingTreeTransformer(ICompressOptions options) : base(options)
        {
        }

        protected override AstNode? Before(AstNode node, bool inList)
        {
            if (node is AstScope astScope)
                return ProcessScopeNode(astScope);

            if (node is AstCall && !_isAfterCall)
                SetIsAfterCall();

            if (node is AstBinary astBinary)
                return ProcessBinaryNode(astBinary, inList);

            if (node is AstSymbolRef astSymbolRef)
            {
                ProcessSymbolRefNode(astSymbolRef);
                return null;
            }

            // statement with body (body is also conditional) or conditional (ternary operator)
            if (node is AstIterationStatement ||
                node is AstIf ||
                node is AstConditional)
                return ProcessConditional(node);

            if (node is AstVar astVar)
                return ProcessAstVarNode(astVar);

            return null;
        }

        protected override AstNode? After(AstNode node, bool inList)
        {
            return null;
        }

        protected override void ResetState()
        {
            base.ResetState();
            _isAfterCall = false;
            _scopeVariableUsages = new Dictionary<string, ScopeVariableUsageInfo>();
            _isInConditional = false;
            _isInScope = false;
            _canPerformMergeDefAndInit = false;
            _astVarCount = 0;
            VariableDefinition.CreatedIfBlocks.Clear();
            _variableWriteOrder = new List<string>();
        }

        protected override bool CanProcessNode(ICompressOptions options, AstNode node)
        {
            return options.EnableVariableHoisting && node is AstScope && !(node is AstClass);
        }

        void SetIsAfterCall()
        {
            _isAfterCall = true;
            foreach (var scopeVariableUsageInfo in _scopeVariableUsages)
            {
                scopeVariableUsageInfo.Value.IsPossiblyUsedInCall = true;
            }
        }

        AstScope ProcessScopeNode(AstScope astScope)
        {
            if (_isInScope)
            {
                return astScope;
            }

            _isInScope = true;
            Descend();
            _isInScope = false;

            if (_astVarCount == 0 || _astVarCount == 1 && !_canPerformMergeDefAndInit && astScope.Body[0] is AstVar)
            {
                return astScope;
            }

            return HoistVariables(astScope);
        }

        AstBinary ProcessBinaryNode(AstBinary astBinary, bool inList)
        {
            var safeIsInRightSideOfBinary = _isInRightSideOfBinary;
            DescendNode(astBinary.Left);
            _isInRightSideOfBinary = true;
            DescendNode(astBinary.Right);
            _isInRightSideOfBinary = safeIsInRightSideOfBinary;
            return astBinary;

            void DescendNode(AstNode node)
            {
                Stack.Add(node);
                Before(node, inList);
                Stack.Pop();
            }
        }

        void ProcessSymbolRefNode(AstSymbolRef astSymbolRef)
        {
            var name = astSymbolRef.Name;
            if (!_scopeVariableUsages.ContainsKey(name))
                _scopeVariableUsages[name] = new ScopeVariableUsageInfo();

            var scopeVariableInfo = SetCurrentState(_scopeVariableUsages[name]);

            switch (astSymbolRef.Usage)
            {
                case SymbolUsage.Read:
                    scopeVariableInfo.ReadReferencesCount++;
                    break;
                case SymbolUsage.Unknown:
                    scopeVariableInfo.UnknownReferencesCount++;
                    break;
                case SymbolUsage.ReadWrite:
                    scopeVariableInfo.ReadReferencesCount++;
                    scopeVariableInfo.WriteReferencesCount++;
                    _variableWriteOrder.Add(name);
                    break;
                case SymbolUsage.Write:
                    scopeVariableInfo.WriteReferencesCount++;
                    _variableWriteOrder.Add(name);
                    if (scopeVariableInfo.CanMoveInitialization &&
                        scopeVariableInfo.FirstHoistableInitialization == null)
                    {
                        AstNode? parentNode;
                        AstNode? initNode;
                        (parentNode, initNode) = GetInitAndParentNode<AstBlock?, AstSimpleStatement?>();

                        if (parentNode != null &&
                            initNode != null &&
                            ((AstBlock)parentNode).Body.IndexOf(initNode) != -1 &&
                            ((AstSimpleStatement)initNode).Body is AstAssign)
                        {
                            SetVariableInitialization(parentNode, initNode);
                            break;
                        }

                        (parentNode, initNode) = GetInitAndParentNode<AstBinary?, AstAssign?>();

                        if (parentNode != null && initNode != null)
                        {
                            SetVariableInitialization(parentNode, initNode);
                            break;
                        }

                        (parentNode, initNode) = GetInitAndParentNode<AstSequence?, AstAssign?>();

                        if (parentNode != null && initNode != null)
                        {
                            SetVariableInitialization(parentNode, initNode);
                            break;
                        }

                        (parentNode, initNode) = GetInitAndParentNode<AstReturn?, AstAssign?>();

                        if (parentNode != null && initNode != null)
                        {
                            SetVariableInitialization(parentNode, initNode);
                            break;
                        }

                        if (Parent() is AstSimpleStatement)
                        {
                            break;
                        }

                        throw new NotImplementedException();
                    }

                    break;
            }

            void SetVariableInitialization(AstNode parent, AstNode initialization)
            {
                var assign = initialization is AstSimpleStatement simpleStatement &&
                                   simpleStatement.Body is AstAssign astAssign
                    ? astAssign
                    : initialization as AstAssign;

                if (assign == null)
                    throw new InvalidOperationException();

                if (_callNodeFinderTreeWalker.FindNodes(assign.Right).Count > 0)
                {
                    SetIsAfterCall();
                    return;
                }

                if (!IsValueHoistable(assign.Right))
                {
                    scopeVariableInfo.FirstHoistableInitialization = NonHoistableVariableInitialization.Instance;
                    return;
                }

                scopeVariableInfo.FirstHoistableInitialization = new VariableInitialization(parent, initialization);
                _canPerformMergeDefAndInit = true;
            }
        }

        bool IsValueHoistable(AstNode value)
        {
            foreach (var astSymbolRef in _symbolRefNodeFinderTreeWalker.FindNodes(value))
            {
                if (_scopeVariableUsages.ContainsKey(astSymbolRef.Name))
                {
                    if (_scopeVariableUsages[astSymbolRef.Name].Definitions.Count == 0 ||
                        !_scopeVariableUsages[astSymbolRef.Name].Definitions[0].CanMoveInitialization ||
                        _scopeVariableUsages[astSymbolRef.Name].ReadReferencesCount > 0 ||
                        _scopeVariableUsages[astSymbolRef.Name].WriteReferencesCount > 0 ||
                        _scopeVariableUsages[astSymbolRef.Name].UnknownReferencesCount > 0)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        (TParent, TInit) GetInitAndParentNode<TParent, TInit>(AstNode? stopNode = null) where TParent : AstNode? where TInit : AstNode?
        {
            var parentNode = default(TParent)!;
            var initNode = default(TInit)!;
            foreach (var currentNode in Parents())
            {
                if (stopNode == currentNode)
                    break;
                if (initNode == null)
                {
                    if (currentNode is TInit tInitNode)
                        initNode = tInitNode;
                    continue;
                }

                if (currentNode is TParent tParentNode)
                    parentNode = tParentNode;
                break;
            }

            return (parentNode, initNode);
        }

        AstNode ProcessConditional(AstNode node)
        {
            var safeIsInConditional = _isInConditional;
            _isInConditional = true;
            Descend();
            _isInConditional = safeIsInConditional;
            return node;
        }

        AstVar ProcessAstVarNode(AstVar astVar)
        {
            _astVarCount++;
            var index = 0;
            foreach (var astVarDefinition in astVar.Definitions)
            {
                var name = GetAstVarDefinitionName(astVarDefinition);
                var canMoveInitialization = true;
                if (astVarDefinition.Value != null)
                {
                    _variableWriteOrder.Add(name);
                    if (_callNodeFinderTreeWalker.FindNodes(astVarDefinition.Value).Count > 0)
                    {
                        SetIsAfterCall();
                    }

                    if (!IsValueHoistable(astVarDefinition.Value))
                    {
                        canMoveInitialization = false;
                    }
                }

                if (!_scopeVariableUsages.ContainsKey(name))
                {
                    _scopeVariableUsages[name] = SetCurrentState(new ScopeVariableUsageInfo());
                }

                var usage = _scopeVariableUsages[name];
                var lastBlock = FindParent<AstBlock>();
                var parent = Parent();
                if (parent == null)
                    throw new SemanticError($"Parent for {nameof(AstVar)} node was not found", astVar);
                var variableDefinition =
                    new VariableDefinition(
                        lastBlock,
                        parent,
                        astVar,
                        astVarDefinition,
                        usage.CanMoveInitialization && canMoveInitialization,
                        index++);
                usage.Definitions.Add(variableDefinition);
            }

            return astVar;
        }

        string GetAstVarDefinitionName(AstVarDef astVarDef)
        {
            var name = (astVarDef.Name as AstSymbol)?.Name;
            if (name == null)
                throw new SemanticError($"{nameof(AstVarDef)} contains name which is not {nameof(AstSymbol)}",
                    astVarDef);
            return name;
        }

        ScopeVariableUsageInfo SetCurrentState(ScopeVariableUsageInfo variableUsageInfo)
        {
            variableUsageInfo.IsUsedInConditionalStatement =
                variableUsageInfo.IsUsedInConditionalStatement || _isInConditional;
            variableUsageInfo.IsPossiblyUsedInCall = _isAfterCall;
            variableUsageInfo.IsUsedOnRightSideOfBinary =
                variableUsageInfo.IsUsedOnRightSideOfBinary || _isInRightSideOfBinary;
            return variableUsageInfo;
        }

        IReadOnlyList<KeyValuePair<string, ScopeVariableUsageInfo>> GetSortVariableUsageInfosDictionaryAsList()
        {
            var processedVars = new HashSet<string>();
            var variableOrder = new List<string>();
            _variableWriteOrder.AddRange(_scopeVariableUsages.Keys);
            foreach (var varName in _variableWriteOrder.Where(varName => !processedVars.Contains(varName)))
            {
                variableOrder.Add(varName);
                processedVars.Add(varName);
            }

            var scopeVariableUsagesList = _scopeVariableUsages.ToList();
            scopeVariableUsagesList.Sort((pairA, pairB) => variableOrder.IndexOf(pairA.Key) - variableOrder.IndexOf(pairB.Key));
            return scopeVariableUsagesList;
        }

        AstScope HoistVariables(AstScope astScope)
        {
            var hoistedVariables = new Dictionary<string, AstVarDef>();

            foreach (var scopeVariableUsageInfo in GetSortVariableUsageInfosDictionaryAsList())
            {
                var variableName = scopeVariableUsageInfo.Key;
                var isFirst = true;
                foreach (var variableDefinitionInfo in scopeVariableUsageInfo.Value.Definitions)
                {
                    if (isFirst)
                    {
                        HoistFirstVariableDefinition(variableDefinitionInfo, scopeVariableUsageInfo.Value,
                            hoistedVariables, variableName);
                        isFirst = false;
                    }
                    else
                    {
                        HoistOtherVariableDefinition(variableDefinitionInfo);
                    }
                }
            }

            if (hoistedVariables.Count == 0)
                return astScope;
            ShouldIterateAgain = true; // TODO set should iterate again only if something has really changed

            var varDefs = new StructList<AstVarDef>();
            foreach (var hoistedVariablesValue in hoistedVariables.Values)
            {
                varDefs.Add(hoistedVariablesValue);
            }

            var astVar = new AstVar(ref varDefs);

            if (astScope.Body.Count == 0)
            {
                astScope.Body.Add(astVar);
                return astScope;
            }

            astScope.Body.Insert(0) = astVar;
            return astScope;
        }

        static void HoistFirstVariableDefinition(
            VariableDefinition variableDefinition,
            ScopeVariableUsageInfo scopeVariableUsageInfo,
            IDictionary<string, AstVarDef> hoistedVariables,
            string variableName)
        {
            // Use initialization value
            if (variableDefinition.CanMoveInitialization &&
                variableDefinition.AstVarDef.Value != null)
            {
                hoistedVariables.Add(variableName, variableDefinition.AstVarDef);
                variableDefinition.RemoveVarDefFromVar();
            }
            // Use first usable assignment (move assignment to initialization)
            else if (variableDefinition.CanMoveInitialization &&
                     scopeVariableUsageInfo.FirstHoistableInitialization != null &&
                     scopeVariableUsageInfo.FirstHoistableInitialization != NonHoistableVariableInitialization.Instance)
            {
                hoistedVariables.Add(variableName, variableDefinition.AstVarDef);
                variableDefinition.RemoveVarDefFromVar();

                var hoistableInitialization = scopeVariableUsageInfo.FirstHoistableInitialization;

                if (hoistableInitialization.Parent is AstBlock parentBlock &&
                    hoistableInitialization.Initialization is AstSimpleStatement initSimpleStatement &&
                    initSimpleStatement.Body is AstAssign simpleStatementAssign)
                {
                    variableDefinition.AstVarDef.Value = simpleStatementAssign.Right;
                    parentBlock.Body.RemoveItem(initSimpleStatement);
                    return;
                }

                if (hoistableInitialization.Parent is AstBinary parentBinary &&
                    hoistableInitialization.Initialization is AstAssign initAssign)
                {
                    variableDefinition.AstVarDef.Value = initAssign.Right;
                    if (parentBinary.Left == initAssign)
                        parentBinary.Left = initAssign.Left;
                    else if (parentBinary.Right == initAssign)
                        parentBinary.Right = initAssign.Left;
                    else
                        throw new NotImplementedException();
                    return;
                }

                if (hoistableInitialization.Parent is AstSequence parentSequence &&
                    hoistableInitialization.Initialization is AstAssign initAssign2)
                {
                    variableDefinition.AstVarDef.Value = initAssign2.Right;
                    parentSequence.Expressions.RemoveItem(initAssign2);
                    return;
                }

                throw new NotImplementedException();
            }
            // Move variable definition to top of scope and assign variable later at same place
            else if (!variableDefinition.CanMoveInitialization &&
                     variableDefinition.AstVarDef.Value != null)
            {
                hoistedVariables.Add(variableName, new AstVarDef(variableDefinition.AstVarDef.Name));
                variableDefinition.ConvertToAssignmentAndInsertBeforeVarNode();
            }
            // No initialization value or can not move initialization
            else
            {
                if (variableDefinition.AstVarDef.Value != null)
                {
                    // Based on previous conditions this should never been thrown
                    throw new NotSupportedException();
                }

                hoistedVariables.Add(variableName, variableDefinition.AstVarDef);
                variableDefinition.TryRemoveVarFromForIn();
            }
        }

        static void HoistOtherVariableDefinition(VariableDefinition variableDefinition)
        {
            // Remove redundant variable definition
            if (variableDefinition.AstVarDef.Value == null)
            {
                variableDefinition.TryRemoveVarFromForIn();
            }
            // Replace var initialization with assign statement
            else
            {
                variableDefinition.ConvertToAssignmentAndInsertBeforeVarNode();
            }
        }
    }
}
