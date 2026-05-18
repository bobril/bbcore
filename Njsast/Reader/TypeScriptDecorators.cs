using System.Collections.Generic;
using Njsast.Ast;
using Njsast.Output;

namespace Njsast.Reader;

public sealed partial class Parser
{
    List<AstNode> TsParseDecorators()
    {
        var decorators = new List<AstNode>();
        while (Type == TokenType.Decorator)
        {
            Next();
            decorators.Add(TsParseDecoratorExpression());
        }
        return decorators;
    }

    AstNode TsParseDecoratorExpression()
    {
        var start = Start;
        var expression = ParseExpressionAtom(start);
        for (;;)
        {
            if (Eat(TokenType.Dot))
            {
                var prop = ParseIdent(true);
                expression = new AstDot(SourceFile, start, _lastTokEnd, expression, prop.Name);
            }
            else if (Eat(TokenType.ParenL))
            {
                expression = BuildCallExpression(start, expression, false, false);
            }
            else
            {
                return expression;
            }
        }
    }

    bool TsDecoratorIsFollowedByClass()
    {
        if (Type != TokenType.Decorator)
            return false;
        var index = TsSkipDecoratorExpressionText(End.Index);
        index = TsSkipWhitespaceAndComments(index);
        while (index < _input.Length && _input[index] == '@')
        {
            index = TsSkipDecoratorExpressionText(index + 1);
            index = TsSkipWhitespaceAndComments(index);
        }
        return TsTextStartsKeyword(index, "class");
    }

    int TsSkipDecoratorExpressionText(int index)
    {
        index = TsSkipWhitespaceAndComments(index);
        while (index < _input.Length)
        {
            var ch = _input[index];
            if (ch == '.')
            {
                index = TsSkipWhitespaceAndComments(index + 1);
                while (index < _input.Length && IsIdentifierChar(_input[index], true))
                    index++;
                continue;
            }
            if (ch == '(')
            {
                var close = TsFindMatchingSkippingLiterals(index, '(', ')');
                if (close < 0) return index;
                index = TsSkipWhitespaceAndComments(close + 1);
                continue;
            }
            if (!IsIdentifierChar(ch, true))
                break;
            index++;
        }
        return index;
    }

    void TsEmitDecoratedClass(AstToplevel topLevel, List<AstNode> decorators, AstDefClass classDecl,
        bool omitClassExpressionName = false)
    {
        TsEmitDecoratedClassToBody(ref topLevel.Body, decorators, classDecl, omitClassExpressionName);
    }

    void TsEmitDecoratedClassToBody(ref StructList<AstNode> targetBody, List<AstNode> decorators, AstDefClass classDecl,
        bool omitClassExpressionName = false)
    {
        var name = classDecl.Name!;
        var nameRef = new AstSymbolRef(SourceFile, name.Start, name.End, name.Name);

        var classNameSym = omitClassExpressionName ? null : new AstSymbolDefClass(name);
        var classBody = new StructList<AstNode>();
        classBody.TransferFrom(ref classDecl.Properties);
        var classSelfAlias = TsDecoratedClassSelfReferenceAlias(classBody, name.Name);
        if (classSelfAlias != null)
        {
            classBody = TsRewriteDecoratedClassSelfReferences(classBody, name.Name, classSelfAlias);
            classBody.Insert(0) = TsBuildDecoratedClassSelfReferenceStaticBlock(name, classSelfAlias);
        }

        var classExpr = new AstClassExpression(SourceFile, classDecl.Start, classDecl.End,
            classNameSym, classDecl.Extends, ref classBody);

        var varSym = new AstSymbolLet(name);
        var varDef = new AstVarDef(SourceFile, classDecl.Start, classDecl.End, varSym, classExpr);

        var declarations = new StructList<AstVarDef>();
        declarations.Add(varDef);
        var varStmt = new AstLet(SourceFile, classDecl.Start, classDecl.End, ref declarations);

        if (_tsPendingClassDecorators != null)
        {
            decorators.AddRange(_tsPendingClassDecorators);
            _tsPendingClassDecorators = null;
        }

        var decoratorArray = BuildDecoratorArray(decorators, name);
        var decoratorCall = BuildDecorateCall(decoratorArray, nameRef);
        AstNode decoratedValue = decoratorCall;
        if (classSelfAlias != null)
            decoratedValue = new AstAssign(SourceFile, classDecl.Start, classDecl.End,
                new AstSymbolRef(SourceFile, name.Start, name.End, classSelfAlias), decoratorCall,
                Operator.Assignment);
        var assign = new AstAssign(SourceFile, classDecl.Start, classDecl.End, nameRef, decoratedValue,
            Operator.Assignment);
        var assignStmt = new AstSimpleStatement(SourceFile, classDecl.Start, classDecl.End, assign);

        if (classSelfAlias != null)
            targetBody.Add(TsBuildDecoratedClassSelfReferenceVar(classDecl, classSelfAlias));
        targetBody.Add(varStmt);
        if (_tsPendingClassDecoratorStatements != null)
        {
            foreach (var decoratorStatement in _tsPendingClassDecoratorStatements)
                targetBody.Add(decoratorStatement);
            _tsPendingClassDecoratorStatements = null;
        }
        targetBody.Add(assignStmt);
    }

    string? TsDecoratedClassSelfReferenceAlias(StructList<AstNode> classBody, string className)
    {
        foreach (var property in classBody.AsReadOnlySpan())
            if (new ContainsSymbolRefTreeWalker(className).HasReference(property))
                return className + "_1";
        return null;
    }

    StructList<AstNode> TsRewriteDecoratedClassSelfReferences(StructList<AstNode> classBody, string className,
        string alias)
    {
        var rewritten = new StructList<AstNode>();
        var transformer = new DecoratedClassSelfReferenceTransformer(SourceFile, className, alias);
        foreach (var property in classBody.AsReadOnlySpan())
            rewritten.Add(transformer.Transform(property));
        return rewritten;
    }

    AstVar TsBuildDecoratedClassSelfReferenceVar(AstDefClass classDecl, string alias)
    {
        var definitions = new StructList<AstVarDef>();
        definitions.Add(new AstVarDef(SourceFile, classDecl.Start, classDecl.End,
            new AstSymbolVar(SourceFile, classDecl.Start, classDecl.End, alias, null), null));
        return new AstVar(SourceFile, classDecl.Start, classDecl.End, ref definitions);
    }

    AstStaticBlock TsBuildDecoratedClassSelfReferenceStaticBlock(AstSymbol className, string alias)
    {
        var aliasRef = new AstSymbolRef(SourceFile, className.Start, className.End, alias);
        var thisRef = new AstThis(SourceFile, className.Start, className.End);
        var assign = new AstAssign(SourceFile, className.Start, className.End, aliasRef, thisRef,
            Operator.Assignment);
        var statements = new StructList<AstNode>();
        statements.Add(new AstSimpleStatement(SourceFile, className.Start, className.End, assign));
        return new AstStaticBlock(SourceFile, className.Start, className.End, ref statements);
    }

    void TsEmitDecoratedExportedClass(AstToplevel topLevel, List<AstNode> decorators, AstExport export,
        AstDefClass classDecl)
    {
        TsEmitDecoratedClass(topLevel, decorators, classDecl);

        var name = classDecl.Name!;
        var specifiers = new StructList<AstNameMapping>();
        specifiers.Add(new AstNameMapping(SourceFile, name.Start, name.End,
            new AstSymbolExportForeign(SourceFile, name.Start, name.End, name.Name),
            new AstSymbolExport(SourceFile, name.Start, name.End, name.Name))
        {
            TypeScriptGeneratedNamespaceExport = Options.ParseTypeScriptNamespaceBody
        });
        topLevel.Body.Add(new AstExport(SourceFile, export.Start, export.End, null, null, ref specifiers));
    }

    void TsEmitDecoratedDefaultExportedClass(AstToplevel topLevel, List<AstNode> decorators, AstExport export,
        AstDefClass classDecl)
    {
        TsEmitDecoratedClass(topLevel, decorators, classDecl, classDecl.Name!.Name == "default_1");

        var name = classDecl.Name!;
        if (Options.ParseTypeScriptNamespaceBody)
        {
            var specifiers = new StructList<AstNameMapping>();
            specifiers.Add(new AstNameMapping(SourceFile, name.Start, name.End,
                new AstSymbolExportForeign(SourceFile, name.Start, name.End, name.Name),
                new AstSymbolExport(SourceFile, name.Start, name.End, name.Name))
            {
                TypeScriptGeneratedNamespaceExport = true
            });
            topLevel.Body.Add(new AstExport(SourceFile, export.Start, export.End, null, null, ref specifiers));
            return;
        }

        topLevel.Body.Add(new AstExport(SourceFile, export.Start, export.End,
            new AstSymbolRef(SourceFile, name.Start, name.End, name.Name), true));
    }

    AstArray BuildDecoratorArray(List<AstNode> decorators, AstNode positionHint)
    {
        var elements = new StructList<AstNode>();
        elements.Reserve((uint)decorators.Count);
        foreach (var d in decorators)
            elements.Add(d);
        return new AstArray(SourceFile, positionHint.Start, positionHint.End, ref elements);
    }

    AstCall BuildDecorateCall(AstArray decoratorArray, AstSymbolRef target)
    {
        var args = new StructList<AstNode>();
        args.Add(decoratorArray);
        args.Add(target);

        var decorateSym = new AstSymbolRef(SourceFile, target.Start, target.End, "__decorate");
        return new AstCall(SourceFile, target.Start, target.End, decorateSym, ref args, false);
    }

    sealed class ContainsSymbolRefTreeWalker : TreeWalker
    {
        readonly string _name;
        bool _found;

        public ContainsSymbolRefTreeWalker(string name)
        {
            _name = name;
        }

        public bool HasReference(AstNode node)
        {
            _found = false;
            Walk(node);
            return _found;
        }

        protected override void Visit(AstNode node)
        {
            if (node is AstSymbolRef { Name: var name } && name == _name)
                _found = true;
            if (!_found)
                Descend();
        }
    }

    sealed class DecoratedClassSelfReferenceTransformer : TreeTransformer
    {
        readonly string? _sourceFile;
        readonly string _className;
        readonly string _alias;

        public DecoratedClassSelfReferenceTransformer(string? sourceFile, string className, string alias)
        {
            _sourceFile = sourceFile;
            _className = className;
            _alias = alias;
        }

        protected override AstNode? Before(AstNode node, bool inList)
        {
            return node is AstSymbolRef symbolRef && symbolRef.Name == _className
                ? new AstSymbolRef(_sourceFile, symbolRef.Start, symbolRef.End, _alias)
                : null;
        }

        protected override AstNode? After(AstNode node, bool inList) => null;
    }

    AstSimpleStatement TsBuildMemberDecorateStatement(List<AstNode> decorators, string className, AstNode key,
        bool @static, bool isProperty, bool computed = false)
    {
        var target = TsBuildDecoratorTarget(className, @static, key);
        var args = new StructList<AstNode>();
        args.Add(BuildDecoratorArray(decorators, key));
        args.Add(target);
        args.Add(computed ? key : TsBuildDecoratorKey(key));
        args.Add(isProperty
            ? new AstUnaryPrefix(SourceFile, key.Start, key.End, Operator.Void, new AstNumber(SourceFile, key.Start, key.End, 0, "0"))
            : new AstNull(SourceFile, key.Start, key.End));

        var decorateSym = new AstSymbolRef(SourceFile, key.Start, key.End, "__decorate");
        var call = new AstCall(SourceFile, key.Start, key.End, decorateSym, ref args, false);
        return new AstSimpleStatement(SourceFile, key.Start, key.End, call);
    }

    void TsOrderMemberDecoratorStatements(List<AstStatement> statements)
    {
        if (statements.Count < 2)
            return;

        var ordered = new List<AstStatement>(statements.Count);
        foreach (var statement in statements)
        {
            if (TsDecorateStatementTargetsPrototype(statement))
                ordered.Add(statement);
        }
        foreach (var statement in statements)
        {
            if (!TsDecorateStatementTargetsPrototype(statement))
                ordered.Add(statement);
        }
        statements.Clear();
        statements.AddRange(ordered);
    }

    bool TsDecorateStatementTargetsPrototype(AstStatement statement)
    {
        return statement is AstSimpleStatement { Body: AstCall call } &&
               call.Expression is AstSymbolRef { Name: "__decorate" } &&
               call.Args.Count >= 2 &&
               call.Args[1] is AstDot { Property: "prototype" };
    }

    bool TsTryAddAbstractMemberDecorator(AstSymbol? className, List<AstNode> decorators,
        List<AstStatement> memberDecoratorStatements, bool isStatic)
    {
        if (IsContextual("accessor") && TsAccessorKeywordIsFollowedByClassElementName(End.Index))
            Next();

        var (computed, key) = ParsePropertyName();
        TsTrySkipTypeParameters();

        var isGetSetSignature = !computed && key is AstSymbol { Name: "get" or "set" } &&
                                Type != TokenType.Colon && Type != TokenType.Eq &&
                                Type != TokenType.Semi;
        var isMethodSignature = Type == TokenType.ParenL || isGetSetSignature;

        while (Type != TokenType.Semi && Type != TokenType.Eof)
            Next();
        Eat(TokenType.Semi);

        if (isMethodSignature || key is AstSymbolPrivate)
            return true;

        if (TsClassDecoratorTargetName(className) is { } decoratorClassName)
            memberDecoratorStatements.Add(TsBuildMemberDecorateStatement(decorators, decoratorClassName, key,
                isStatic, true, computed));
        return true;
    }

    List<AstNode> TsBuildParameterDecoratorCalls(List<(int Index, AstNode Decorator)> decorators)
    {
        var decoratorCalls = new List<AstNode>();
        foreach (var decorator in decorators)
            decoratorCalls.Add(TsBuildParamCall(decorator.Index, decorator.Decorator));
        return decoratorCalls;
    }

    AstSimpleStatement TsBuildParameterDecorateStatement(List<(int Index, AstNode Decorator)> decorators,
        string className, AstNode key, bool @static, bool computed = false)
    {
        return TsBuildMemberDecorateStatement(TsBuildParameterDecoratorCalls(decorators), className, key, @static,
            false, computed);
    }

    bool TsTryAppendParameterDecoratorsToExistingMemberStatement(List<AstStatement> statements,
        List<(int Index, AstNode Decorator)> decorators, string className, AstNode key, bool @static,
        bool computed = false)
    {
        var parameterDecorators = TsBuildParameterDecoratorCalls(decorators);
        if (parameterDecorators.Count == 0)
            return true;
        var target = TsBuildDecoratorTarget(className, @static, key);
        var decoratorKey = computed ? key : TsBuildDecoratorKey(key);

        for (var i = statements.Count - 1; i >= 0; i--)
        {
            if (statements[i] is not AstSimpleStatement
                {
                    Body: AstCall
                    {
                        Expression: AstSymbolRef { Name: "__decorate" },
                        Args.Count: 4
                    } call
                } ||
                call.Args[0] is not AstArray decoratorArray ||
                !TsDecoratorArgumentMatches(call.Args[1], target) ||
                !TsDecoratorArgumentMatches(call.Args[2], decoratorKey) ||
                call.Args[3] is not AstNull)
                continue;

            foreach (var parameterDecorator in parameterDecorators)
                decoratorArray.Elements.Add(parameterDecorator);
            return true;
        }

        return false;
    }

    static bool TsDecoratorArgumentMatches(AstNode left, AstNode right)
    {
        return left.IsStructurallyEquivalentTo(right) ||
               left.PrintToString() == right.PrintToString();
    }

    string? TsClassDecoratorTargetName(AstSymbol? className)
    {
        if (className != null)
            return className.Name;
        if (_tsDefaultExportClassName == null)
            return null;
        _tsDefaultExportClassNameUsed = true;
        return _tsDefaultExportClassName;
    }

    AstSimpleStatement TsBuildConstructorParameterDecorateStatement(List<(int Index, AstNode Decorator)> decorators,
        string className, AstNode positionHint)
    {
        var decoratorCalls = new List<AstNode>();
        foreach (var decorator in decorators)
            decoratorCalls.Add(TsBuildParamCall(decorator.Index, decorator.Decorator));
        return TsBuildClassDecorateStatement(decoratorCalls, className, positionHint);
    }

    void TsAddPendingConstructorParameterDecorators(List<(int Index, AstNode Decorator)> decorators)
    {
        _tsPendingClassDecorators ??= new List<AstNode>();
        foreach (var decorator in decorators)
            _tsPendingClassDecorators.Add(TsBuildParamCall(decorator.Index, decorator.Decorator));
    }

    AstSimpleStatement TsBuildClassDecorateStatement(List<AstNode> decorators, string className, AstNode positionHint)
    {
        var classRef = new AstSymbolRef(SourceFile, positionHint.Start, positionHint.End, className);
        var decoratorArray = BuildDecoratorArray(decorators, positionHint);
        var decoratorCall = BuildDecorateCall(decoratorArray, classRef);
        var assign = new AstAssign(SourceFile, positionHint.Start, positionHint.End,
            new AstSymbolRef(SourceFile, positionHint.Start, positionHint.End, className), decoratorCall,
            Operator.Assignment);
        return new AstSimpleStatement(SourceFile, positionHint.Start, positionHint.End, assign);
    }

    AstSimpleStatement TsBuildClassFieldInitializerStatement(AstNode target, AstNode key, AstNode value,
        bool computed)
    {
        AstNode left = computed
            ? new AstSub(SourceFile, key.Start, key.End, target, key)
            : key switch
            {
                AstSymbolPrivate symbol => new AstDot(SourceFile, key.Start, key.End, target, "#" + symbol.Name),
                AstSymbol { Name: "constructor" } symbol => new AstSub(SourceFile, key.Start, key.End, target,
                    new AstString(SourceFile, symbol.Start, symbol.End, symbol.Name)),
                AstSymbol symbol => new AstDot(SourceFile, key.Start, key.End, target, symbol.Name),
                AstString str => new AstSub(SourceFile, key.Start, key.End, target, str),
                AstNumber num => new AstSub(SourceFile, key.Start, key.End, target,
                    new AstNumber(SourceFile, key.Start, key.End, num.Value, num.Literal)),
                _ => new AstSub(SourceFile, key.Start, key.End, target, key)
            };
        var assign = new AstAssign(SourceFile, key.Start, value.End, left, value, Operator.Assignment);
        return new AstSimpleStatement(SourceFile, key.Start, value.End, assign);
    }

    void TsInjectInstanceFieldInitializers(ref StructList<AstNode> classBody, bool hasSuperClass,
        List<AstStatement> initializers, Position start, Position end)
    {
        for (var i = 0u; i < classBody.Count; i++)
        {
            if (classBody[i] is not AstConciseMethod
                {
                    Static: false,
                    Key: AstSymbol { Name: "constructor" } or AstString { Value: "constructor" },
                    Value: AstFunction constructorFunction
                })
                continue;

            var newBody = new StructList<AstNode>();
            var insertIndex = TsConstructorFieldInitializerInsertIndex(constructorFunction.Body, hasSuperClass);
            newBody.Reserve((uint)(constructorFunction.Body.Count + initializers.Count));
            for (var j = 0u; j < insertIndex; j++)
                newBody.Add(constructorFunction.Body[j]);
            foreach (var initializer in initializers)
                newBody.Add(initializer);
            for (var j = insertIndex; j < constructorFunction.Body.Count; j++)
                newBody.Add(constructorFunction.Body[j]);
            constructorFunction.Body.TransferFrom(ref newBody);
            return;
        }

        var parameters = new StructList<AstNode>();
        var body = new StructList<AstNode>();
        if (hasSuperClass)
        {
            var superArgs = new StructList<AstNode>();
            superArgs.Add(new AstExpansion(SourceFile, start, end,
                new AstSymbolRef(SourceFile, start, end, "arguments")));
            body.Add(new AstSimpleStatement(SourceFile, start, end,
                new AstCall(SourceFile, start, end, new AstSuper(SourceFile, start, end), ref superArgs)));
        }
        foreach (var initializer in initializers)
            body.Add(initializer);
        var constructor = new AstFunction(SourceFile, start, end, null, ref parameters, false, false, ref body);
        var key = new AstSymbolProperty(SourceFile, start, end, "constructor");
        classBody.Insert(0) = new AstConciseMethod(SourceFile, start, end, key, constructor, false, false, false);
    }

    static uint TsConstructorSuperInsertIndex(StructList<AstNode> body)
    {
        for (var i = 0u; i < body.Count; i++)
            if (body[i] is AstSimpleStatement { Body: AstCall { Expression: AstSuper } })
                return i + 1;
        return 0;
    }

    static uint TsConstructorParameterPropertyInsertIndex(StructList<AstNode> body)
    {
        var i = 0u;
        for (; i < body.Count; i++)
        {
            if (body[i] is not AstTypeScriptParameterPropertyAssignment)
                break;
        }
        return i;
    }

    static uint TsConstructorFieldInitializerInsertIndex(StructList<AstNode> body, bool hasSuperClass)
    {
        var i = hasSuperClass ? TsConstructorSuperInsertIndex(body) : 0u;
        for (; i < body.Count; i++)
        {
            if (body[i] is not AstTypeScriptParameterPropertyAssignment)
                break;
        }
        return i;
    }

    static bool TsLooksLikeThisAssignment(AstNode node)
    {
        return node is AstSimpleStatement
        {
            Body: AstAssign
            {
                Left: AstDot { Expression: AstThis },
                Right: AstSymbolRef
            }
        };
    }

    AstCall TsBuildParamCall(int index, AstNode decorator)
    {
        var args = new StructList<AstNode>();
        args.Add(new AstNumber(SourceFile, decorator.Start, decorator.End, index, index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        args.Add(decorator);

        var paramSym = new AstSymbolRef(SourceFile, decorator.Start, decorator.End, "__param");
        return new AstCall(SourceFile, decorator.Start, decorator.End, paramSym, ref args, false);
    }

    AstNode TsBuildDecoratorTarget(string className, bool @static, AstNode positionHint)
    {
        var classRef = new AstSymbolRef(SourceFile, positionHint.Start, positionHint.End, className);
        return @static ? classRef : new AstDot(SourceFile, positionHint.Start, positionHint.End, classRef, "prototype");
    }

    AstNode TsBuildDecoratorKey(AstNode key)
    {
        return key switch
        {
            AstSymbol symbol => new AstString(SourceFile, key.Start, key.End, symbol.Name),
            AstString str => new AstString(SourceFile, key.Start, key.End, str.Value),
            AstNumber num => new AstNumber(SourceFile, key.Start, key.End, num.Value, num.Literal),
            _ => key
        };
    }
}
