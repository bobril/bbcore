using System.Collections.Generic;
using Njsast.Ast;

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
        var assign = new AstAssign(SourceFile, classDecl.Start, classDecl.End, nameRef, decoratorCall, Operator.Assignment);
        var assignStmt = new AstSimpleStatement(SourceFile, classDecl.Start, classDecl.End, assign);

        targetBody.Add(varStmt);
        if (_tsPendingClassDecoratorStatements != null)
        {
            foreach (var decoratorStatement in _tsPendingClassDecoratorStatements)
                targetBody.Add(decoratorStatement);
            _tsPendingClassDecoratorStatements = null;
        }
        targetBody.Add(assignStmt);
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

    AstSimpleStatement TsBuildClassFieldInitializerStatement(string className, AstNode key, AstNode value,
        bool @static, bool computed)
    {
        AstNode target = @static
            ? new AstSymbolRef(SourceFile, key.Start, key.End, className)
            : new AstThis(SourceFile, key.Start, key.End);
        AstNode left = computed
            ? new AstSub(SourceFile, key.Start, key.End, target, key)
            : key switch
            {
                AstSymbol symbol => new AstDot(SourceFile, key.Start, key.End, target, symbol.Name),
                AstString str => new AstDot(SourceFile, key.Start, key.End, target, str.Value),
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
            var insertIndex = hasSuperClass ? TsConstructorSuperInsertIndex(constructorFunction.Body) : 0u;
            while (insertIndex < constructorFunction.Body.Count && TsLooksLikeThisAssignment(constructorFunction.Body[insertIndex]))
                insertIndex++;
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
            var argsSymbol = new AstSymbolFunarg("args");
            parameters.Add(new AstExpansion(SourceFile, start, end, argsSymbol));
            var superArgs = new StructList<AstNode>();
            superArgs.Add(new AstExpansion(SourceFile, start, end,
                new AstSymbolRef(SourceFile, start, end, "args")));
            body.Add(new AstSimpleStatement(SourceFile, start, end,
                new AstCall(SourceFile, start, end, new AstSuper(SourceFile, start, end), ref superArgs)));
        }
        foreach (var initializer in initializers)
            body.Add(initializer);
        var constructor = new AstFunction(SourceFile, start, end, null, ref parameters, false, false, ref body);
        var key = new AstSymbolProperty(SourceFile, start, end, "constructor");
        classBody.Add(new AstConciseMethod(SourceFile, start, end, key, constructor, false, false, false));
    }

    static uint TsConstructorSuperInsertIndex(StructList<AstNode> body)
    {
        for (var i = 0u; i < body.Count; i++)
            if (body[i] is AstSimpleStatement { Body: AstCall { Expression: AstSuper } })
                return i + 1;
        return 0;
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
