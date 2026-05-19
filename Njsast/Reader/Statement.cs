using System;
using System.Collections.Generic;
using System.Diagnostics;
using Njsast.Ast;

namespace Njsast.Reader;

public sealed partial class Parser
{
    // Parse a program. Initializes the parser, reads any number of
    // statements, and wraps them in a Program node.  Optionally takes a
    // `program` argument.  If present, the statements will be appended
    // to its body instead of creating a new node.
    void ParseTopLevel(AstToplevel node)
    {
        var exports = new Dictionary<string, bool>();
        _canBeDirective = true;
        if (IsTypeScript && !Options.ParseTypeScriptNamespaceBody)
            TsReserveTopLevelUsingTemps();
        while (Type != TokenType.Eof)
        {
            if (IsTypeScript && !Options.ParseTypeScriptNamespaceBody && Type == TokenType.Export &&
                TsExportStartsUsingDeclaration())
            {
                _tsErasedTypeOnlyModuleSyntaxUsed = true;
                Next();
                var usingStatements = TsParseUsingScope(topLevel: true, () => Type == TokenType.Eof);
                foreach (var usingStatement in usingStatements)
                {
                    node.Body.Add(usingStatement);
                }
                break;
            }

            if (IsTypeScript && TsIsUsingDeclarationStart())
            {
                var usingStatements = TsParseUsingScope(topLevel: !Options.ParseTypeScriptNamespaceBody,
                    () => Type == TokenType.Eof);
                foreach (var usingStatement in usingStatements)
                {
                    node.Body.Add(usingStatement);
                }
                break;
            }

            if (IsTypeScript && TsTryParseNamespaceStatements(out var namespaceStatements,
                    local: Options.ParseTypeScriptNamespaceBody))
            {
                TsAddNamespaceStatements(ref node.Body, namespaceStatements);
                continue;
            }

            if (IsTypeScript && TsTryParseEnumStatements(out var enumStatements,
                    local: Options.ParseTypeScriptNamespaceBody,
                    preserveConstEnum: Options.ParseTypeScriptNamespaceBody))
            {
                TsAddEnumStatements(ref node.Body, enumStatements);
                continue;
            }

            AstStatement stmt;
            if (IsTypeScript && Type == TokenType.Decorator)
            {
                var decorators = TsParseDecorators();
                var oldForceAnonymousDefaultClass = _tsForceAnonymousStaticAccessorNameForDefaultExportClass;
                _tsForceAnonymousStaticAccessorNameForDefaultExportClass =
                    TsNextDecoratedDefaultExportClassIsAnonymous();
                try
                {
                    stmt = ParseStatement(true, true, exports);
                }
                finally
                {
                    _tsForceAnonymousStaticAccessorNameForDefaultExportClass = oldForceAnonymousDefaultClass;
                }
                if (stmt is AstDefClass classDecl)
                {
                    TsEmitDecoratedClass(node, decorators, classDecl, TsIsSyntheticDefaultClassName(classDecl));
                    continue;
                }
                if (stmt is AstExport { ExportedDefinition: AstDefClass exportedClass, IsDefault: false } export)
                {
                    TsEmitDecoratedExportedClass(node, decorators, export, exportedClass);
                    continue;
                }
                if (stmt is AstExport { ExportedDefinition: AstDefClass defaultExportedClass, IsDefault: true } defaultExport)
                {
                    TsEmitDecoratedDefaultExportedClass(node, decorators, defaultExport, defaultExportedClass);
                    continue;
                }
            }
            else
            {
                if (IsTypeScript && TsIsImportEqualsStatementStart())
                {
                    stmt = TsParseImportEqualsStatement(Start);
                }
                else
                {
                stmt = ParseStatement(true, true, exports);
                }
            }

            if (_canBeDirective)
            {
                if (IsDirectiveCandidate(stmt))
                {
                    if (IsUseStrictDirective(stmt))
                    {
                        node.HasUseStrictDirective = true;
                        _strict = true;
                        continue;
                    }
                }
                else
                {
                    _canBeDirective = false;
                }
            }

            if (IsTypeScript && _tsPendingClassDecorators != null &&
                stmt is AstExport { ExportedDefinition: AstDefClass pendingExportedClass, IsDefault: false } pendingExport)
            {
                var decorators = _tsPendingClassDecorators;
                _tsPendingClassDecorators = null;
                TsEmitDecoratedExportedClass(node, decorators, pendingExport, pendingExportedClass);
                continue;
            }
            if (IsTypeScript && _tsPendingClassDecorators != null &&
                stmt is AstExport { ExportedDefinition: AstDefClass pendingDefaultExportedClass, IsDefault: true } pendingDefaultExport)
            {
                var decorators = _tsPendingClassDecorators;
                _tsPendingClassDecorators = null;
                TsEmitDecoratedDefaultExportedClass(node, decorators, pendingDefaultExport, pendingDefaultExportedClass);
                continue;
            }
            if (IsTypeScript && _tsPendingClassDecorators != null && stmt is AstDefClass decoratedClass)
            {
                var decorators = _tsPendingClassDecorators;
                _tsPendingClassDecorators = null;
                TsEmitDecoratedClass(node, decorators, decoratedClass);
                continue;
            }

            if (IsTypeScript && stmt is AstImport or AstExport)
                _tsRuntimeModuleSyntaxUsed = true;
            node.Body.Add(stmt);
            if (_tsPendingClassDecoratorStatements != null)
            {
                foreach (var decoratorStatement in _tsPendingClassDecoratorStatements)
                {
                    node.Body.Add(decoratorStatement);
                }
                _tsPendingClassDecoratorStatements = null;
            }

        }

        TsInsertPendingClassComputedKeyStatements(ref node.Body);
        if (!Options.ParseTypeScriptNamespaceBody)
            TsInsertUsingHelperStatements(ref node.Body);
        if (IsTypeScript && !Options.ParseTypeScriptNamespaceBody &&
            _tsErasedTypeOnlyModuleSyntaxUsed && !_tsRuntimeModuleSyntaxUsed)
            TsInsertEmptyExportModuleMarker(ref node.Body);

        Next();
        node.End = _lastTokEnd;
    }

    bool IsLet()
    {
        if (Type != TokenType.Name || !"let".Equals(Value)) return false;
        var skip = SkipWhiteSpace.Match(_input, _pos.Index);
        var next = _pos.Index + skip.Groups[0].Length;
        if (next >= _input.Length)
            return false;
        var nextCh = _input[next];
        if (nextCh == CharCode.LeftSquareBracket || nextCh == CharCode.LeftCurlyBracket) return true; // '{' and '['
        if (IsIdentifierStart(nextCh, true))
        {
            var pos = next + 1;
            while (IsIdentifierChar(_input.Get(pos), true))
                ++pos;
            var ident = _input.Substring(next, pos - next);
            if (!KeywordRelationalOperator.IsMatch(ident))
                return true;
        }

        return false;
    }

    // check 'async [no LineTerminator here] function'
    // - 'async /*foo*/ function' is OK.
    // - 'async /*\n*/ function' is invalid.
    bool IsAsyncFunction()
    {
        if (Type != TokenType.Name || !"async".Equals(Value))
            return false;

        var skip = SkipWhiteSpace.Match(_input, _pos.Index);
        var next = _pos.Index + skip.Groups[0].Length;
        return !LineBreak.IsMatch(_input.Substring(_pos.Index, next - _pos.Index)) &&
               _input.Length >= next + 8 && _input.AsSpan(next, 8).SequenceEqual("function") &&
               (next + 8 == _input.Length || !IsIdentifierChar(_input.Get(next + 8)));
    }

    // Parse a single statement.
    //
    // If expecting a statement and finding a slash operator, parse a
    // regular expression literal. This is to handle cases like
    // `if (foo) /blah/.exec(foo)`, where looking at the previous token
    // does not help.
    AstStatement ParseStatement(bool declaration, bool topLevel = false,
        IDictionary<string, bool>? exports = null)
    {
        var starttype = Type;
        var startLocation = Start;
        VariableKind? kind = null;

        if (IsLet())
        {
            starttype = TokenType.Var;
            kind = VariableKind.Let;
        }

        if (IsTypeScript && TsIsAwaitArrayUsingExpressionStart())
            return TsParseAwaitArrayUsingExpressionStatementAsRaw(startLocation);
        if (IsTypeScript && TsIsUsingDeclarationStart())
            return TsParseUsingDeclarationAsPreservedStatement();
        if (TsIsTypeOnlyStatementStart())
            return TsParseTypeOnlyStatement(startLocation);
        if (TsIsDeclareStatementStart())
            return TsParseDeclareStatement(startLocation);
        if (TsIsQuotedModuleDeclarationStart())
            return TsParseQuotedModuleDeclaration(startLocation);
        if (TsIsGlobalAugmentationStatementStart())
            return TsParseGlobalAugmentationStatement(startLocation);
                if (TsIsImportEqualsStatementStart())
                    return TsParseImportEqualsStatement(startLocation);
        if (IsTypeScript && Type == TokenType.Decorator)
            return TsParseDecoratedClassAsBlockStatement(startLocation);
        if (IsTypeScript && TsTryParseEnumStatements(out var enumStatements,
                local: !topLevel && declaration && !_tsParsingLabelBody,
                preserveConstEnum: !topLevel))
        {
            if (enumStatements.Count == 0)
                return new AstEmptyStatement(SourceFile, startLocation, _lastTokEnd);
            if (enumStatements.Count == 1)
                return enumStatements[0];

            var body = new StructRefList<AstNode>();
            foreach (var statement in enumStatements)
                body.Add(statement);
            return new AstBlockStatement(SourceFile, startLocation, _lastTokEnd, ref body);
        }
        if (starttype == TokenType.Function && TsTryParseFunctionOverloadStatement(startLocation, out var typeOnlyStatement))
            return typeOnlyStatement;
        if (TsIsNamespaceStatementStart())
        {
            if (TsTryParseNamespaceStatements(out var namespaceStatements,
                    local: !topLevel && declaration && !_tsParsingLabelBody))
            {
                if (namespaceStatements.Count == 0)
                    return new AstTypeScriptOnly(SourceFile, startLocation, _lastTokEnd);
                if (namespaceStatements.Count == 1)
                    return namespaceStatements[0];

                var body = new StructRefList<AstNode>();
                foreach (var statement in namespaceStatements)
                    body.Add(statement);
                return new AstBlockStatement(SourceFile, startLocation, _lastTokEnd, ref body);
            }
            Raise(startLocation, "Unexpected token");
        }
        if (TsIsGlobalAugmentationStatementStart())
            return TsParseGlobalAugmentationStatement(startLocation);
        if (IsTypeScript && IsContextual("abstract"))
        {
            if (!TsHasLineBreakAfterCurrentToken() && TsIsClassFollowing())
            {
                Next();
                return ParseClass(startLocation, true, false);
            }
        }
        if (IsTypeScript && TsDefaultIsFollowedByClass())
        {
            Next();
            var oldDefaultClassName = _tsDefaultExportClassName;
            var oldDefaultClassNameUsed = _tsDefaultExportClassNameUsed;
            var oldAllowAnonymousExportClassStatement = _tsAllowAnonymousExportClassStatement;
            _tsDefaultExportClassName = TsNextInvalidDefaultExportName();
            _tsDefaultExportClassNameUsed = true;
            _tsAllowAnonymousExportClassStatement = true;
            try
            {
                return ParseClass(Start, true, false);
            }
            finally
            {
                _tsDefaultExportClassName = oldDefaultClassName;
                _tsDefaultExportClassNameUsed = oldDefaultClassNameUsed;
                _tsAllowAnonymousExportClassStatement = oldAllowAnonymousExportClassStatement;
            }
        }
        if (IsTypeScript && TsDefaultIsFollowedByFunction())
        {
            Next();
            var isAsync = IsAsyncFunction();
            var startLoc = Start;
            if (isAsync)
                Next();
            if (Type != TokenType.Function)
                Raise(Start, "Unexpected token");
            Next();
            var oldDefaultClassName = _tsDefaultExportClassName;
            var oldDefaultClassNameUsed = _tsDefaultExportClassNameUsed;
            _tsDefaultExportClassName = TsNextInvalidDefaultExportName();
            _tsDefaultExportClassNameUsed = true;
            try
            {
                return ParseFunction(startLoc, true, true, false, isAsync);
            }
            finally
            {
                _tsDefaultExportClassName = oldDefaultClassName;
                _tsDefaultExportClassNameUsed = oldDefaultClassNameUsed;
            }
        }

        // Most types of statements are recognized by the keyword they
        // start with. Many are trivial to parse, some require a bit of
        // complexity.

        switch (starttype)
        {
            case TokenType.Break:
            case TokenType.Continue:
                return ParseBreakContinueStatement(startLocation,
                    TokenInformation.Types[starttype].Keyword ?? string.Empty);
            case TokenType.Debugger:
                return ParseDebuggerStatement(startLocation);
            case TokenType.Do:
                return ParseDoStatement(startLocation);
            case TokenType.For:
                return ParseForStatement(startLocation);
            case TokenType.Function:
                if (!declaration && Options.EcmaVersion >= 6)
                {
                    Raise(startLocation, "Unexpected token");
                }

                return ParseFunctionStatement(startLocation, false);
            case TokenType.Class:
                if (!declaration)
                {
                    Raise(startLocation, "Unexpected token");
                }

                return ParseClass(startLocation, true, false);
            case TokenType.If:
                return ParseIfStatement(startLocation);
            case TokenType.Return:
                return ParseReturnStatement(startLocation);
            case TokenType.Switch:
                return ParseSwitchStatement(startLocation);
            case TokenType.Throw:
                return ParseThrowStatement(startLocation);
            case TokenType.Try:
                return ParseTryStatement(startLocation);
            case TokenType.Const:
            case TokenType.Var:
                var realKind = kind ?? ToVariableKind((string)GetValue());
                if (!declaration && realKind != VariableKind.Var && !IsTypeScript)
                {
                    Raise(startLocation, "Unexpected token");
                }

                return ParseVarStatement(startLocation, realKind);
            case TokenType.While:
                return ParseWhileStatement(startLocation);
            case TokenType.With:
                return ParseWithStatement(startLocation);
            case TokenType.BraceL:
                return ParseBlock();
            case TokenType.Semi:
                return ParseEmptyStatement(startLocation);
            case TokenType.Export:
            case TokenType.Import:
                Next();
                if (IsTypeScript && starttype == TokenType.Import && IsContextual("type"))
                {
                    if (topLevel)
                        _tsErasedTypeOnlyModuleSyntaxUsed = true;
                    return TsParseTypeOnlyStatement(startLocation);
                }
                if (IsTypeScript && starttype == TokenType.Export &&
                    (IsContextual("type") || IsContextual("interface")))
                {
                    if (topLevel)
                        _tsErasedTypeOnlyModuleSyntaxUsed = true;
                    return TsParseTypeOnlyStatement(startLocation);
                }
                if (starttype == TokenType.Import && (Type is TokenType.ParenL or TokenType.Dot ||
                                                      IsTypeScript && Type == TokenType.Relational && "<".Equals(Value)))
                {
                    _wasImportKeyword = true;
                    break;
                }

                if (!Options.AllowImportExportEverywhere)
                {
                    if (!topLevel && !IsTypeScript)
                        Raise(startLocation, "'import' and 'export' may only appear at the top level");
                    if (!_inModule && !IsTypeScript)
                        Raise(startLocation, "'import' and 'export' may appear only with 'sourceType: module'");
                }

                return starttype == TokenType.Import
                    ? ParseImport(startLocation)
                    : ParseExport(startLocation, exports, topLevel);
        }

        if (!_wasImportKeyword && IsAsyncFunction() && declaration)
        {
            Next();
            return ParseFunctionStatement(startLocation, true);
        }

        var maybeName = Value;
        var expr = ParseExpression(startLocation);
        if (starttype == TokenType.Name && expr is AstSymbol identifierNode && Eat(TokenType.Colon))
            return ParseLabelledStatement(startLocation, maybeName is string stringName ? stringName : string.Empty,
                identifierNode);
        return ParseExpressionStatement(startLocation, expr);
    }

    static VariableKind ToVariableKind(string s)
    {
        switch (s)
        {
            case "var":
                return VariableKind.Var;
            case "let":
                return VariableKind.Let;
            case "const":
                return VariableKind.Const;
            default:
                throw new ArgumentException();
        }
    }

    AstLoopControl ParseBreakContinueStatement(Position nodeStart, string keyword)
    {
        var isBreak = keyword == "break";
        AstLabelRef? label = null;
        Next();
        if (Eat(TokenType.Semi) || InsertSemicolon())
        {
        }
        else if (Type != TokenType.Name)
        {
            Raise(Start, "Unexpected token");
        }
        else
        {
            label = new AstLabelRef(ParseIdent());
            Semicolon();
        }

        // Verify that there is an actual destination to break or
        // continue to.

        if (label == null)
        {
            if (isBreak && !_allowBreak || !isBreak && !_allowContinue)
            {
                if (!IsTypeScript)
                    Raise(nodeStart, "Unsyntactic " + keyword);
            }
        }
        else
        {
            var i = 0;
            for (; i < _labels.Count; ++i)
            {
                var lab = _labels[(uint)i];
                if (lab.Name == label.Name)
                {
                    if (lab.IsLoop && (isBreak || lab.IsLoop))
                    {
                        break;
                    }

                    if (isBreak) break;
                }
            }

            if (i == _labels.Count && !IsTypeScript) Raise(nodeStart, "Unsyntactic " + keyword);
        }

        if (isBreak)
        {
            return new AstBreak(SourceFile, nodeStart, _lastTokEnd, label);
        }

        return new AstContinue(SourceFile, nodeStart, _lastTokEnd, label);
    }

    AstDebugger ParseDebuggerStatement(Position nodeStart)
    {
        Next();
        Semicolon();
        return new AstDebugger(SourceFile, nodeStart, _lastTokEnd);
    }

    AstDo ParseDoStatement(Position nodeStart)
    {
        Next();
        var backupAllowBreak = _allowBreak;
        var backupAllowContinue = _allowContinue;
        _allowBreak = true;
        _allowContinue = true;
        var body = (AstStatement)ParseStatement(false);
        Expect(TokenType.While);
        _allowBreak = backupAllowBreak;
        _allowContinue = backupAllowContinue;
        var test = ParseParenExpression();
        if (Options.EcmaVersion >= 6)
            Eat(TokenType.Semi);
        else
            Semicolon();

        return new AstDo(SourceFile, nodeStart, _lastTokEnd, test, body);
    }

    // Disambiguating between a `for` and a `for`/`in` or `for`/`of`
    // loop is non-trivial. Basically, we have to parse the init `var`
    // statement or expression, disallowing the `in` operator (see
    // the second parameter to `parseExpression`), and then check
    // whether the next token is `in` or `of`. When there is no init
    // part (semicolon immediately after the opening parenthesis), it
    // is a regular `for` loop.
    AstStatement ParseForStatement(Position nodeStart)
    {
        Next();
        var isAwait = EatContextual("await");
        EnterLexicalScope();
        Expect(TokenType.ParenL);
        if (Type == TokenType.Semi)
        {
            return ParseFor(nodeStart, null, isAwait);
        }

        if (IsTypeScript && TsIsForUsingDeclarationStart())
            return TsParseForUsingStatement(nodeStart, isAwait);
        if (IsTypeScript && TsIsForArrayUsingExpressionStart())
            return TsParsePreservedForArrayUsingStatement(nodeStart, isAwait);

        var isLet = IsLet();
        if (Type is TokenType.Var or TokenType.Const || isLet)
        {
            var startLoc = Start;
            var kind = isLet ? VariableKind.Let : ToVariableKind((string)GetValue());
            Next();
            var declarations = new StructRefList<AstVarDef>();
            ParseVar(ref declarations, true, kind);
            AstDefinitions init;
            if (kind == VariableKind.Let)
            {
                init = new AstLet(SourceFile, startLoc, _lastTokEnd, ref declarations);
            }
            else if (kind == VariableKind.Const)
            {
                init = new AstConst(SourceFile, startLoc, _lastTokEnd, ref declarations);
            }
            else
            {
                init = new AstVar(SourceFile, startLoc, _lastTokEnd, ref declarations);
            }

            if ((Type == TokenType.In || IsContextual("of")) &&
                init.Definitions.Count == 1 &&
                !(kind != VariableKind.Var && init.Definitions[0].Value != null))
                return ParseForIn(nodeStart, init, isAwait);
            return ParseFor(nodeStart, init, isAwait);
        }
        else
        {
            var refDestructuringErrors = new DestructuringErrors();
            var init = ParseExpression(Start, true, refDestructuringErrors);
            if (Type == TokenType.In || IsContextual("of"))
            {
                init = ToAssignable(init)!;
                CheckLVal(init, false, null);
                CheckPatternErrors(refDestructuringErrors, true);
                return ParseForIn(nodeStart, init, isAwait);
            }

            CheckExpressionErrors(refDestructuringErrors, true);
            return ParseFor(nodeStart, init, isAwait);
        }
    }

    AstLambda ParseFunctionStatement(Position nodeStart, bool isAsync)
    {
        Next();
        return ParseFunction(nodeStart, true, false, false, isAsync);
    }

    bool IsFunction()
    {
        return Type == TokenType.Function || IsAsyncFunction();
    }

    AstIf ParseIfStatement(Position nodeStart)
    {
        Next();
        var test = ParseParenExpression();
        // allow function declarations in branches, but only in non-strict mode
        var consequent = ParseStatement(!_strict && IsFunction());
        var alternate = Eat(TokenType.Else) ? ParseStatement(!_strict && IsFunction()) : null;
        return new AstIf(SourceFile, nodeStart, _lastTokEnd, test, consequent, alternate);
    }

    AstReturn ParseReturnStatement(Position nodeStart)
    {
        if (!_inFunction && !Options.AllowReturnOutsideFunction && !IsTypeScript)
            Raise(Start, "'return' outside of function");
        Next();

        // In `return` (and `break`/`continue`), the keywords with
        // optional arguments, we eagerly look for a semicolon or the
        // possibility to insert one.

        AstNode? argument = null;
        if (!Eat(TokenType.Semi) && !InsertSemicolon())
        {
            argument = ParseExpression(Start);
            Semicolon();
        }

        return new AstReturn(SourceFile, nodeStart, _lastTokEnd, argument);
    }

    AstSwitch ParseSwitchStatement(Position nodeStart)
    {
        Next();
        var discriminant = ParseParenExpression();
        var cases = new StructRefList<AstNode>();
        Expect(TokenType.BraceL);
        EnterLexicalScope();

        AstSwitchBranch? consequent = null;
        var backupAllowBreak = _allowBreak;
        for (var sawDefault = false; Type != TokenType.BraceR;)
        {
            if (Type == TokenType.Case || Type == TokenType.Default)
            {
                var isCase = Type == TokenType.Case;
                if (consequent != null)
                {
                    consequent.End = _lastTokEnd;
                }

                var startLoc = Start;
                Next();
                _allowBreak = true;
                if (isCase)
                {
                    var test = ParseExpression(Start);
                    consequent = new AstCase(SourceFile, startLoc, startLoc, test);
                }
                else
                {
                    if (sawDefault && !IsTypeScript) RaiseRecoverable(_lastTokStart, "Multiple default clauses");
                    sawDefault = true;
                    consequent = new AstDefault(SourceFile, startLoc, startLoc);
                }

                cases.Add(consequent);
                Expect(TokenType.Colon);
            }
            else
            {
                if (consequent == null)
                {
                    throw NewSyntaxError(Start, "Unexpected token");
                }

                if (IsTypeScript && TsTryParseEnumStatements(out var enumStatements, local: true))
                {
                    foreach (var enumStatement in enumStatements)
                    {
                        consequent.Body.Add(enumStatement);
                    }
                    continue;
                }
                if (IsTypeScript && TsTryParseNamespaceStatements(out var namespaceStatements, local: true))
                {
                    TsAddNamespaceStatements(ref consequent.Body, namespaceStatements);
                    continue;
                }
                if (IsTypeScript && TsIsUsingDeclarationStart())
                {
                    var usingStatement = TsParseUsingDeclarationAsPreservedStatement();
                    consequent.Body.Add(usingStatement);
                    continue;
                }
                if (IsTypeScript && Type == TokenType.Decorator)
                {
                    TsParseDecoratedClassToBody(ref consequent.Body);
                    continue;
                }

                var statement = ParseStatement(true);
                consequent.Body.Add(statement);
            }
        }

        ExitLexicalScope();
        if (consequent != null)
        {
            consequent.End = _lastTokEnd;
        }

        Next(); // Closing brace
        _allowBreak = backupAllowBreak;
        return new AstSwitch(SourceFile, nodeStart, _lastTokEnd, discriminant, ref cases);
    }

    AstThrow ParseThrowStatement(Position nodeStart)
    {
        Next();
        if (LineBreak.IsMatch(_input.Substring(_lastTokEnd.Index, Start.Index - _lastTokEnd.Index)))
        {
            if (IsTypeScript)
                return new AstThrow(SourceFile, nodeStart, _lastTokEnd, null!);
            Raise(_lastTokEnd, "Illegal newline after throw");
        }
        if (IsTypeScript && Type == TokenType.Semi)
        {
            Semicolon();
            return new AstThrow(SourceFile, nodeStart, _lastTokEnd, null!);
        }
        var argument = ParseExpression(Start);
        Semicolon();
        return new(SourceFile, nodeStart, _lastTokEnd, argument);
    }

    AstTry ParseTryStatement(Position nodeStart)
    {
        Next();
        var block = ParseBlock();
        AstCatch? handler = null;
        if (Type == TokenType.Catch)
        {
            var startLocation = Start;
            Next();
            AstNode? param = null;
            if (Eat(TokenType.ParenL))
            {
                param = ParseBindingAtom();
                TsTrySkipOptionalOrDefiniteBindingMarker();
                TsTrySkipTypeAnnotation();
                if (IsTypeScript && Eat(TokenType.Eq))
                {
                    var right = ParseMaybeAssign(Start);
                    param = new AstDefaultAssign(SourceFile, param.Start, _lastTokEnd, param, right);
                }
                EnterLexicalScope();
                CheckLVal(param, true, VariableKind.Let);
                param = ToRightDeclarationSymbolKind(param, VariableKind.Catch);
                Expect(TokenType.ParenR);
            }
            else
            {
                EnterLexicalScope();
            }

            var body = ParseBlock(false);
            ExitLexicalScope();
            handler = new(SourceFile, startLocation, _lastTokEnd, param, ref body.Body);
        }

        var startOfFinally = CurPosition();
        var finalizerBody = Eat(TokenType.Finally) ? ParseBlock() : null;
        if (handler == null && finalizerBody == null)
            Raise(nodeStart, "Missing catch or finally clause");
        var finalizer = finalizerBody != null
            ? new AstFinally(SourceFile, startOfFinally, finalizerBody.End, ref finalizerBody.Body)
            : null;
        return new(SourceFile, nodeStart, _lastTokEnd, ref block.Body, handler, finalizer);
    }

    AstDefinitions ParseVarStatement(Position nodeStart, VariableKind kind)
    {
        Next();
        var declarations = new StructRefList<AstVarDef>();
        if (!IsTypeScript || Type is not (TokenType.Eof or TokenType.Semi))
            ParseVar(ref declarations, false, kind);
        Semicolon();
        if (kind == VariableKind.Let)
        {
            var statement = new AstLet(SourceFile, nodeStart, _lastTokEnd, ref declarations);
            if (IsTypeScript)
                TsForgetErasedTypeOnlyValueNames(statement);
            return statement;
        }

        if (kind == VariableKind.Const)
        {
            var statement = new AstConst(SourceFile, nodeStart, _lastTokEnd, ref declarations);
            if (IsTypeScript)
                TsForgetErasedTypeOnlyValueNames(statement);
            return statement;
        }

        Debug.Assert(kind == VariableKind.Var);
        var varStatement = new AstVar(SourceFile, nodeStart, _lastTokEnd, ref declarations);
        if (IsTypeScript)
            TsForgetErasedTypeOnlyValueNames(varStatement);
        return varStatement;
    }

    AstWhile ParseWhileStatement(Position nodeStart)
    {
        Next();
        var test = ParseParenExpression();
        var backupAllowBreak = _allowBreak;
        var backupAllowContinue = _allowContinue;
        _allowBreak = true;
        _allowContinue = true;
        var body = ParseStatement(false) as AstStatement;
        _allowBreak = backupAllowBreak;
        _allowContinue = backupAllowContinue;
        return new AstWhile(SourceFile, nodeStart, _lastTokEnd, test, body);
    }

    AstWith ParseWithStatement(Position nodeStart)
    {
        if (_strict && !IsTypeScript) Raise(Start, "'with' in strict mode");
        Next();
        var @object = ParseParenExpression();
        var body = ParseStatement(false) as AstStatement;
        return new AstWith(SourceFile, nodeStart, _lastTokEnd, body, @object);
    }

    AstEmptyStatement ParseEmptyStatement(Position nodeStart)
    {
        Next();
        return new AstEmptyStatement(SourceFile, nodeStart, _lastTokEnd);
    }

    AstStatement TsParseDecoratedClassAsBlockStatement(Position nodeStart)
    {
        var body = new StructRefList<AstNode>();
        EnterLexicalScope();
        try
        {
            TsParseDecoratedClassToBody(ref body);
        }
        finally
        {
            ExitLexicalScope();
        }

        return new AstBlockStatement(SourceFile, nodeStart, _lastTokEnd, ref body);
    }

    void TsParseDecoratedClassToBody(ref StructRefList<AstNode> body)
    {
        _tsPendingClassDecorators = TsParseDecorators();
        if (IsContextual("abstract") && TsIsClassFollowing())
            Next();
        var oldDefaultClassName = _tsDefaultExportClassName;
        var oldDefaultClassNameUsed = _tsDefaultExportClassNameUsed;
        if (Eat(TokenType.Default))
        {
            _tsDefaultExportClassName = "default_1";
            _tsDefaultExportClassNameUsed = true;
        }
        if (Type != TokenType.Class)
            Raise(Start, "Unexpected token");
        try
        {
            var decoratedClass = ParseClass(Start, true, false);
            var decorators = _tsPendingClassDecorators;
            _tsPendingClassDecorators = null;
            TsEmitDecoratedClassToBody(ref body, decorators!, (AstDefClass)decoratedClass);
        }
        finally
        {
            _tsDefaultExportClassName = oldDefaultClassName;
            _tsDefaultExportClassNameUsed = oldDefaultClassNameUsed;
        }
    }

    AstLabeledStatement ParseLabelledStatement(Position nodeStart, string maybeName, AstSymbol expr)
    {
        foreach (var label in _labels)
        {
            if (label.Name == maybeName && !IsTypeScript)
                Raise(expr.Start, "Label '" + maybeName + "' is already declared");
        }

        var newlabel = new AstLabel(SourceFile, nodeStart, _lastTokEnd, maybeName);
        newlabel.IsLoop = TokenInformation.Types[Type].IsLoop;
        _labels.Add(newlabel);
        AstStatement body;
        var oldTsParsingLabelBody = _tsParsingLabelBody;
        _tsParsingLabelBody = true;
        try
        {
            body = ParseStatement(true);
        }
        finally
        {
            _tsParsingLabelBody = oldTsParsingLabelBody;
        }
        if (body is AstClass or AstLet or AstConst ||
            body is AstFunction functionDeclaration && (_strict || functionDeclaration.IsGenerator))
        {
            if (!IsTypeScript)
                RaiseRecoverable(body.Start, "Invalid labelled declaration");
        }
        _labels.Pop();

        return new(SourceFile, nodeStart, _lastTokEnd, body, newlabel);
    }

    AstSimpleStatement ParseExpressionStatement(Position nodeStart, AstNode expr)
    {
        if (_canBeDirective && expr is AstString directive && directive.Value == "use strict")
        {
            _strict = true;
        }

        Semicolon();
        return new(SourceFile, nodeStart, _lastTokEnd, expr);
    }

    // Parse a semicolon-enclosed block of statements, handling `"use
    // strict"` declarations when `allowStrict` is true (used for
    // function bodies).
    AstBlock ParseBlock(bool createNewLexicalScope = true)
    {
        var startLocation = Start;
        var body = new StructRefList<AstNode>();
        Expect(TokenType.BraceL);
        if (createNewLexicalScope)
        {
            EnterLexicalScope();
        }

        _tsBlockDepth++;
        var oldRuntimeEnumConstants = IsTypeScript ? TsSnapshotRuntimeEnumConstants() : null;
        try
        {
            while (!Eat(TokenType.BraceR) && Type != TokenType.Eof)
            {
                if (IsTypeScript && Type == TokenType.Decorator)
                {
                    TsParseDecoratedClassToBody(ref body);
                    continue;
                }
                if (IsTypeScript && TsTryParseEnumStatements(out var enumStatements, local: true))
                {
                    foreach (var enumStatement in enumStatements)
                    {
                        body.Add(enumStatement);
                    }
                    continue;
                }
                if (IsTypeScript && TsTryParseNamespaceStatements(out var namespaceStatements, local: true))
                {
                    TsAddNamespaceStatements(ref body, namespaceStatements);
                    continue;
                }
                if (IsTypeScript && TsIsUsingDeclarationStart())
                {
                    var usingStatements = TsParseUsingScope(topLevel: false, () => Type is TokenType.BraceR or TokenType.Eof);
                    foreach (var usingStatement in usingStatements)
                    {
                        body.Add(usingStatement);
                    }
                    continue;
                }
                var stmt = ParseStatement(true);
                if (IsTypeScript && _tsPendingClassDecorators != null && stmt is AstDefClass decoratedClass)
                {
                    var decorators = _tsPendingClassDecorators;
                    _tsPendingClassDecorators = null;
                    TsEmitDecoratedClassToBody(ref body, decorators, decoratedClass);
                    continue;
                }
                body.Add(stmt);
            }
        }
        finally
        {
            _tsBlockDepth--;
            if (IsTypeScript)
            {
                TsRestoreRuntimeEnumConstants(oldRuntimeEnumConstants);
            }
        }

        if (createNewLexicalScope)
        {
            ExitLexicalScope();
        }

        return new AstBlockStatement(SourceFile, startLocation, _lastTokEnd, ref body);
    }

    // Parse a regular `for` loop. The disambiguation code in
    // `parseStatement` will already have parsed the init statement or
    // expression.
    AstFor ParseFor(Position nodeStart, AstNode? init, bool isAwait)
    {
        if (isAwait) Raise(nodeStart, "Simple for cannot be awaited");
        Expect(TokenType.Semi);
        var test = Type == TokenType.Semi ? null : ParseExpression(Start);
        Expect(TokenType.Semi);
        var update = Type == TokenType.ParenR ? null : ParseExpression(Start);
        Expect(TokenType.ParenR);
        ExitLexicalScope();
        var backupAllowBreak = _allowBreak;
        var backupAllowContinue = _allowContinue;
        _allowBreak = true;
        _allowContinue = true;
        var body = ParseStatement(false) as AstStatement;
        _allowBreak = backupAllowBreak;
        _allowContinue = backupAllowContinue;
        return new AstFor(SourceFile, nodeStart, _lastTokEnd, body, init, test, update);
    }

    // Parse a `for`/`in` and `for`/`of` loop, which are almost
    // same from parser's perspective.
    AstForIn ParseForIn(Position nodeStart, AstNode init, bool @await)
    {
        var isIn = Type == TokenType.In;
        if (isIn && @await) Raise(nodeStart, "for in cannot be awaited");
        Next();
        var right = ParseExpression(Start);
        Expect(TokenType.ParenR);
        ExitLexicalScope();
        var backupAllowBreak = _allowBreak;
        var backupAllowContinue = _allowContinue;
        _allowBreak = true;
        _allowContinue = true;
        var body = ParseStatement(false);
        _allowBreak = backupAllowBreak;
        _allowContinue = backupAllowContinue;
        if (isIn)
        {
            return new AstForIn(SourceFile, nodeStart, _lastTokEnd, body, init, right, @await);
        }

        return new AstForOf(SourceFile, nodeStart, _lastTokEnd, body, init, right, @await);
    }

    // Parse a list of variable declarations.
    void ParseVar(ref StructRefList<AstVarDef> declarations, bool isFor, VariableKind kind)
    {
        for (;;)
        {
            var startLocation = Start;
            var id = ParseVarId(kind);
            TsTrySkipOptionalOrDefiniteBindingMarker();
            if (isFor)
                TsTrySkipForBindingTypeAnnotation();
            else
                TsTrySkipTypeAnnotation();
            AstNode? init = null;
            if (Eat(TokenType.Eq))
            {
                init = ParseMaybeAssign(Start, isFor);
            }
            else if (kind == VariableKind.Const &&
                     !IsTypeScript &&
                     !(Type == TokenType.In || Options.EcmaVersion >= 6 && IsContextual("of")))
            {
                Raise(Start, "Unexpected token");
            }
            else if (!IsTypeScript && !(id is AstSymbol) && !(isFor && (Type == TokenType.In || IsContextual("of"))))
            {
                Raise(_lastTokEnd, "Complex binding patterns require an initialization value");
            }

            var decl = new AstVarDef(SourceFile, startLocation, _lastTokEnd, id, init);
            declarations.Add(decl);
            if (!Eat(TokenType.Comma)) break;
        }
    }

    AstNode ParseVarId(VariableKind kind)
    {
        var id = ParseBindingAtom();
        CheckLVal(id, true, kind);
        id = ToRightDeclarationSymbolKind(id, kind);

        return id;
    }

    static AstNode ToRightDeclarationSymbolKind(AstNode id, VariableKind kind)
    {
        switch (id)
        {
            case AstSymbol symbol:
                return kind switch
                {
                    VariableKind.Let => new AstSymbolLet(symbol),
                    VariableKind.Const => new AstSymbolConst(symbol),
                    VariableKind.Var => new AstSymbolVar(symbol),
                    VariableKind.Catch => new AstSymbolCatch(symbol),
                    _ => throw new ArgumentOutOfRangeException(nameof(kind))
                };
            case AstDestructuring destructuring:
            {
                for (var i = 0; i < destructuring.Names.Count; i++)
                {
                    destructuring.Names.SetItem(i, ToRightDeclarationSymbolKind(destructuring.Names[i], kind));
                }

                return id;
            }
            case AstObjectProperty prop:
                prop.Value = ToRightDeclarationSymbolKind(prop.Value, kind);
                return id;
            case AstHole:
                return id;
            case AstExpansion expansion:
                expansion.Expression = ToRightDeclarationSymbolKind(expansion.Expression, kind);
                return id;
            case AstDefaultAssign defaultAssign:
                defaultAssign.Left = ToRightDeclarationSymbolKind(defaultAssign.Left, kind);
                return id;
            default:
                throw new ArgumentException("Unexpected node type " + id.GetType().Name);
        }
    }

    // Parse a function declaration or literal (depending on the
    // `isStatement` parameter).
    AstLambda ParseFunction(Position startLoc, bool isStatement, bool isNullableId,
        bool allowExpressionBody = false,
        bool isAsync = false)
    {
        var generator = Eat(TokenType.Star);

        AstSymbol? id = null;
        if (isStatement || isNullableId)
        {
            id = isNullableId && Type != TokenType.Name ? null : ParseIdent();
            if (id == null && IsTypeScript && _tsDefaultExportClassNameUsed && _tsDefaultExportClassName != null)
                id = new AstSymbolRef(SourceFile, startLoc, startLoc, _tsDefaultExportClassName);
            if (id != null)
            {
                CheckLVal(id, true, VariableKind.Var);
            }
        }

        var oldInGen = _inGenerator;
        var oldInAsync = _inAsync;
        var oldYieldPos = _yieldPos;
        var oldAwaitPos = _awaitPos;
        var oldInFunc = _inFunction;
        _inGenerator = generator;
        _inAsync = isAsync;
        _yieldPos = default;
        _awaitPos = default;
        _inFunction = true;
        EnterFunctionScope();

        try
        {
            if (isStatement == false && isNullableId == false)
                id = Type == TokenType.Name ? ParseIdent() : null;

            TsTrySkipTypeParameters();
            var parameters = new StructList<AstNode>();
            ParseFunctionParams(ref parameters);
            TsTrySkipTypeAnnotation();
            MakeSymbolFunArg(ref parameters);
            var body = new StructRefList<AstNode>();
            var useStrict = false;
            var oldAutoAccessorTempIndex = _tsAutoAccessorTempIndex;
            var oldAutoAccessorStorageTempIndex = _tsAutoAccessorStorageTempIndex;
            _tsVarScopeDepth++;
            _tsAutoAccessorTempIndex = 0;
            _tsAutoAccessorStorageTempIndex = 0;
            try
            {
                var unused = ParseFunctionBody(parameters, startLoc, id, allowExpressionBody, ref body, ref useStrict);
            }
            finally
            {
                _tsVarScopeDepth--;
                _tsAutoAccessorTempIndex = oldAutoAccessorTempIndex;
                _tsAutoAccessorStorageTempIndex = oldAutoAccessorStorageTempIndex;
            }

            if (isStatement || isNullableId)
            {
                if (id != null)
                    id = new AstSymbolDefun(id);
                var astDefun = new AstDefun(SourceFile, startLoc, _lastTokEnd, id != null ? (AstSymbolDefun)id : null,
                    ref parameters, generator,
                    isAsync, ref body);
                astDefun.SetUseStrict(useStrict);
                return astDefun;
            }

            if (id != null)
                id = new AstSymbolLambda(id);
            var astFunction = new AstFunction(SourceFile, startLoc, _lastTokEnd,
                id != null ? (AstSymbolLambda)id : null, ref parameters,
                generator, isAsync, ref body);
            astFunction.SetUseStrict(useStrict);
            return astFunction;
        }
        finally
        {
            _inGenerator = oldInGen;
            _inAsync = oldInAsync;
            _yieldPos = oldYieldPos;
            _awaitPos = oldAwaitPos;
            _inFunction = oldInFunc;
        }
    }

    void ParseFunctionParams(ref StructList<AstNode> parameters)
    {
        Expect(TokenType.ParenL);
        ParseBindingList(ref parameters, TokenType.ParenR, false, Options.EcmaVersion >= 8);
        CheckYieldAwaitInDefaultParams();
    }

    // Parse a class declaration or literal (depending on the
    // `isStatement` parameter).
    AstClass ParseClass(Position nodeStart, bool isStatement, bool isNullableId)
    {
        Next();

        var id = ParseClassId(isStatement);
        var oldAnonymousClassStaticAccessorName = _tsAnonymousClassStaticAccessorName;
        _tsAnonymousClassStaticAccessorName = null;
        TsTrySkipTypeParameters();
        var superClass = ParseClassSuper();
        if (IsTypeScript && (Type == TokenType.Comma || Type == TokenType.Extends || IsContextual("extends")))
        {
            TsSkipHeritageClause();
        }
        if (IsTypeScript && IsContextual("implements"))
        {
            TsSkipHeritageClause();
        }
        var hadConstructor = false;
        var body = new StructRefList<AstNode>();
        var memberDecoratorStatements = new List<AstStatement>();
        var instanceFieldInitializerStatements = new List<AstStatement>();
        Expect(TokenType.BraceL);
        if (IsTypeScript && id == null &&
            (_tsDefaultExportClassName == null || _tsForceAnonymousStaticAccessorNameForDefaultExportClass) &&
            TsClassBodyContainsStaticAutoAccessor())
            TsEnsureAnonymousClassStaticAccessorName(Start);
        while (!Eat(TokenType.BraceR) && Type != TokenType.Eof)
        {
            if (Eat(TokenType.Semi)) continue;
            if (IsTypeScript && IsContextual("declare") && !TsDeclareMemberStartsAccessor())
            {
                while (Type != TokenType.Semi && Type != TokenType.Eof) Next();
                Eat(TokenType.Semi);
                continue;
            }

            List<AstNode>? memberDecorators = null;
            if (IsTypeScript && Type == TokenType.Decorator)
                memberDecorators = TsParseDecorators();

            var hasTsModifiers = false;
            var isAbstractMember = false;
            var isDeclareMember = false;
            var hasStaticTsModifier = false;
            var restartClassMember = false;
            while (true)
            {
                if (TsStaticIsFollowedByClassMemberModifier())
                {
                    if (TsHasLineBreakAfterCurrentToken())
                    {
                        Next();
                        restartClassMember = true;
                        break;
                    }
                    hasTsModifiers = true;
                    hasStaticTsModifier = true;
                    Next();
                    continue;
                }

                if (!TsIsClassMemberModifier())
                    break;

                if (hasTsModifiers && TsClassModifierLooksLikeMemberName())
                    break;
                if (TsHasLineBreakAfterCurrentToken())
                {
                    Next();
                    restartClassMember = true;
                    break;
                }
                hasTsModifiers = true;
                if (IsContextual("abstract")) isAbstractMember = true;
                if (IsContextual("declare")) isDeclareMember = true;
                Next();
            }
            if (restartClassMember)
                continue;

            if (isAbstractMember)
            {
                if (memberDecorators is { Count: > 0 } &&
                    TsTryAddAbstractMemberDecorator(id, memberDecorators, memberDecoratorStatements,
                        false))
                    continue;
                TsSkipAbstractClassMemberSignature();
                continue;
            }
            if (isDeclareMember)
            {
                if (TsTryParseDeclareAccessorMember(Start, ref body))
                    continue;
                if (memberDecorators is { Count: > 0 } &&
                    TsTryAddAbstractMemberDecorator(id, memberDecorators, memberDecoratorStatements,
                        false))
                    continue;
                while (Type != TokenType.Semi && Type != TokenType.Eof) Next();
                Eat(TokenType.Semi);
                continue;
            }

            if (TsTryParseAutoAccessor(id, ref body, memberDecorators, memberDecoratorStatements,
                    instanceFieldInitializerStatements, hasStaticTsModifier))
                continue;

            var methodStart = Start;
            if (TsTrySkipClassIndexSignature())
                continue;
            var isGenerator = Eat(TokenType.Star);
            var isAsync = false;
            var isMaybeStatic = hasStaticTsModifier || Type == TokenType.Name && "static".Equals(Value);
            bool computed;
            AstNode key;
            var staticKeyAlreadyParsed = false;
            if (!hasStaticTsModifier && isMaybeStatic && TsStaticModifierIsFollowedByClassElementName())
            {
                Next();
                var staticComputed = Type == TokenType.BracketL;
                var staticKey = ParsePropertyName();
                computed = staticComputed || staticKey.computed;
                key = staticKey.key;
                staticKeyAlreadyParsed = true;
            }
            else
            {
                (computed, key) = ParsePropertyName();
            }
            TsTrySkipTypeParameters();
            // Optional method/property marker: ?
            var isOptional = IsTypeScript && Eat(TokenType.Question);
            // Static initialization block: static { ... }
            if (isMaybeStatic && Type == TokenType.BraceL)
            {
                var staticBlock = ParseBlock();
                var staticBody = new StructRefList<AstNode>();
                staticBody.TransferFrom(ref staticBlock.Body);
                body.Add(new AstStaticBlock(SourceFile, methodStart, _lastTokEnd, ref staticBody));
                continue;
            }

            var @static = hasStaticTsModifier ||
                          isMaybeStatic && (staticKeyAlreadyParsed || Type != TokenType.ParenL && Type != TokenType.Eq);
            if (@static && !staticKeyAlreadyParsed && !hasStaticTsModifier)
            {
                if (isGenerator)
                {
                    Raise(Start, "Unexpected token");
                }

                isGenerator = Eat(TokenType.Star);
                (computed, key) = ParsePropertyName();
                TsTrySkipTypeParameters();
            }

            if (!isGenerator && !computed &&
                key is AstSymbol { Name: "async" } && Type != TokenType.ParenL && Type != TokenType.Eq && !CanInsertSemicolon())
            {
                isAsync = true;
                isGenerator = Eat(TokenType.Star);
                (computed, key) = ParsePropertyName();
                TsTrySkipTypeParameters();
            }

            var kind = PropertyKind.Method;
            var isGetSet = false;
            if (!computed)
            {
                if (!isGenerator && !isAsync && key is AstSymbol identifierNode2 &&
                    Type != TokenType.ParenL && Type != TokenType.Eq && Type != TokenType.Colon &&
                    Type != TokenType.Semi &&
                    identifierNode2.Name is "get" or "set")
                {
                    isGetSet = true;
                    kind = identifierNode2.Name == "get" ? PropertyKind.Get : PropertyKind.Set;
                    (computed, key) = ParsePropertyName();
                    TsTrySkipTypeParameters();
                }

                if (!@static && !computed && key is AstSymbol { Name: "constructor" } and not AstSymbolPrivate)
                {
                    if (hadConstructor && TsTrySkipClassMethodOverloadSignature())
                        continue;
                    if (hadConstructor && !IsTypeScript) Raise(key.Start, "Duplicate constructor in the same class");
                    if (isGetSet) Raise(key.Start, "Constructor can't have get/set modifier");
                    if (isGenerator) Raise(key.Start, "Constructor can't be a generator");
                    if (isAsync) Raise(key.Start, "Constructor can't be an async method");
                    kind = PropertyKind.Constructor;
                }
                else if (@static && key is AstSymbol keyIdentifier && keyIdentifier.Name == "prototype" && !IsTypeScript)
                {
                    Raise(key.Start, "Classes may not have a static property named prototype");
                }
            }

            // Class field: key is not followed by (
            if (Type != TokenType.ParenL)
            {
                if (IsTypeScript && key is AstSymbol { Name: "declare" } && TsStaticModifierIsFollowedByGetSetAccessor())
                    continue;
                TsTrySkipOptionalOrDefiniteBindingMarker();
                TsTrySkipClassFieldTypeAnnotation();
                  AstNode? fieldValue = null;
                if (Eat(TokenType.Eq))
                {
                    fieldValue = ParseMaybeAssign(Start);
                }

                if (IsTypeScript && key is not AstSymbolPrivate)
                {
                    var initializerKey = key;
                    var decoratorKey = key;
                    if (memberDecorators is { Count: > 0 } && computed)
                    {
                        var classKey = TsPrepareDecoratedComputedClassKey(key);
                        initializerKey = classKey.DecoratorKey;
                        decoratorKey = classKey.DecoratorKey;
                        _tsPendingDecoratedComputedClassKeyAssignments ??= new List<AstNode>();
                        _tsPendingDecoratedComputedClassKeyAssignments.Add(classKey.ClassKey);
                    }

                    if (fieldValue != null)
                    {
                        if (@static)
                        {
                            var staticBody = new StructRefList<AstNode>();
                            staticBody.Add(TsBuildClassFieldInitializerStatement(
                                new AstThis(SourceFile, key.Start, key.End), initializerKey, fieldValue, computed));
                            body.Add(new AstStaticBlock(SourceFile, methodStart, _lastTokEnd, ref staticBody));
                        }
                        else
                        {
                            instanceFieldInitializerStatements.Add(TsBuildClassFieldInitializerStatement(
                                new AstThis(SourceFile, key.Start, key.End), initializerKey, fieldValue, computed));
                        }
                    }
                    else if (computed && (memberDecorators is { Count: > 0 } ||
                                           key is not (AstSymbol or AstNumber or AstString)))
                    {
                        var staticBody = new StructRefList<AstNode>();
                        staticBody.Add(new AstSimpleStatement(SourceFile, key.Start, key.End, key));
                        body.Add(new AstStaticBlock(SourceFile, methodStart, _lastTokEnd, ref staticBody));
                    }

                    if (memberDecorators is { Count: > 0 } &&
                        TsClassDecoratorTargetName(id) is { } decoratorClassName)
                    {
                        memberDecoratorStatements.Add(TsBuildMemberDecorateStatement(memberDecorators,
                            decoratorClassName, decoratorKey, @static, true, computed));
                    }

                    Eat(TokenType.Semi);
                    continue;
                }

                if (IsTypeScript && key is AstSymbolPrivate && memberDecorators is { Count: > 0 } && fieldValue != null &&
                    memberDecoratorStatements.Count > 0)
                {
                    body.Add(new AstClassField(SourceFile, methodStart, _lastTokEnd, key, null, @static, computed));
                    if (@static)
                    {
                        var staticBody = new StructRefList<AstNode>();
                        staticBody.Add(TsBuildClassFieldInitializerStatement(
                            new AstThis(SourceFile, key.Start, key.End), key, fieldValue, computed));
                        body.Add(new AstStaticBlock(SourceFile, methodStart, _lastTokEnd, ref staticBody));
                    }
                    else
                    {
                        instanceFieldInitializerStatements.Add(TsBuildClassFieldInitializerStatement(
                            new AstThis(SourceFile, key.Start, key.End), key, fieldValue, computed));
                    }
                    Eat(TokenType.Semi);
                    continue;
                }

                if (memberDecorators is { Count: > 0 })
                {
                    var classKey = key;
                    var decoratorKey = key;
                    if (computed && key is not AstSymbolPrivate)
                        (classKey, decoratorKey) = TsPrepareDecoratedComputedClassKey(key);
                    body.Add(new AstClassField(SourceFile, methodStart, _lastTokEnd, classKey, fieldValue, @static,
                        computed));
                    if (key is not AstSymbolPrivate && TsClassDecoratorTargetName(id) is { } decoratorClassName)
                        memberDecoratorStatements.Add(TsBuildMemberDecorateStatement(memberDecorators,
                            decoratorClassName, decoratorKey, @static, true, computed));
                    Eat(TokenType.Semi);
                    continue;
                }

                if (hasTsModifiers)
                {
                    body.Add(new AstClassField(SourceFile, methodStart, _lastTokEnd, key, fieldValue, @static,
                        computed));
                    Eat(TokenType.Semi);
                    continue;
                }

                body.Add(new AstClassField(SourceFile, methodStart, _lastTokEnd, key, fieldValue, @static,
                    computed));
                Eat(TokenType.Semi);
                continue;
            }

            List<AstSymbol>? tsParameterProperties = null;
            if (kind == PropertyKind.Constructor && IsTypeScript)
                tsParameterProperties = new List<AstSymbol>();
            List<(int Index, AstNode Decorator)>? tsParameterDecorators = null;
            if (IsTypeScript)
                tsParameterDecorators = new List<(int Index, AstNode Decorator)>();

            if (isGetSet && TsTryParseAccessorOverloadSignature(methodStart, key, kind, @static, ref body))
                continue;
            if (TsTrySkipClassMethodOverloadSignature())
                continue;
            if (kind == PropertyKind.Constructor)
                hadConstructor = true;

            // Optional/abstract method with no body: skip params and return type, eat ;
            if (isOptional && Type == TokenType.ParenL && !TsClassMethodSignatureIsFollowedByBody())
            {
                Expect(TokenType.ParenL);
                var pDepth = 1;
                while (Type != TokenType.Eof && pDepth > 0)
                {
                    if (Type == TokenType.ParenL) pDepth++;
                    else if (Type == TokenType.ParenR) pDepth--;
                    Next();
                }
                TsTrySkipTypeAnnotation();
                Eat(TokenType.Semi);
                continue;
            }

            var methodValue = ParseMethod(isGenerator, isAsync, tsParameterProperties, tsParameterDecorators);
            if (tsParameterProperties is { Count: > 0 })
            {
                var newBody = new StructRefList<AstNode>();
                newBody.Reserve((uint)(methodValue.Body.Count + tsParameterProperties.Count));
                var insertIndex = superClass != null ? TsConstructorSuperInsertIndex(methodValue.Body) : 0u;
                for (var i = 0u; i < insertIndex; i++)
                    newBody.Add(methodValue.Body[i]);
                foreach (var parameterProperty in tsParameterProperties)
                    newBody.Add(TsBuildParameterPropertyAssignment(parameterProperty));
                for (var i = insertIndex; i < methodValue.Body.Count; i++)
                    newBody.Add(methodValue.Body[i]);
                methodValue.Body.TransferFrom(ref newBody);
            }

            if (isGetSet)
            {
                var paramCount = kind == PropertyKind.Get ? 0 : 1;
                if (methodValue.ArgNames.Count != paramCount)
                {
                    var startLocation = methodValue.Start;
                    if (!IsTypeScript)
                    {
                        if (kind == PropertyKind.Get)
                            RaiseRecoverable(startLocation, "getter should have no params");
                        else
                            RaiseRecoverable(startLocation, "setter should have exactly one param");
                    }
                }
                else
                {
                    if (kind == PropertyKind.Set && methodValue.ArgNames[0] is AstExpansion && !IsTypeScript)
                        RaiseRecoverable(methodValue.ArgNames[0].Start, "Setter cannot use rest params");
                }
            }

            var methodClassKey = key;
            var methodDecoratorKey = key;
            if (computed && key is not AstSymbolPrivate &&
                (memberDecorators is { Count: > 0 } || tsParameterDecorators is { Count: > 0 }))
                (methodClassKey, methodDecoratorKey) = TsPrepareDecoratedComputedClassKey(key);

            if (kind == PropertyKind.Get)
            {
                body.Add(new AstObjectGetter(SourceFile, methodStart, _lastTokEnd, methodClassKey, methodValue, @static));
            }
            else if (kind == PropertyKind.Set)
            {
                body.Add(new AstObjectSetter(SourceFile, methodStart, _lastTokEnd, methodClassKey, methodValue, @static));
            }
            else if (kind is PropertyKind.Method or PropertyKind.Constructor)
            {
                body.Add(new AstConciseMethod(SourceFile, methodStart, _lastTokEnd, methodClassKey, methodValue, @static,
                    isGenerator, isAsync));
            }
            else
            {
                throw new InvalidOperationException("parseClass unknown kind " + kind);
            }

            if (memberDecorators is { Count: > 0 })
            {
                if (key is not AstSymbolPrivate && TsClassDecoratorTargetName(id) is { } decoratorClassName)
                {
                    var decorators = memberDecorators;
                    if (tsParameterDecorators is { Count: > 0 })
                    {
                        decorators = new List<AstNode>(memberDecorators);
                        decorators.AddRange(TsBuildParameterDecoratorCalls(tsParameterDecorators));
                    }
                    memberDecoratorStatements.Add(TsBuildMemberDecorateStatement(decorators, decoratorClassName,
                        methodDecoratorKey, @static, false, computed));
                }
            }
            if (tsParameterDecorators is { Count: > 0 } && memberDecorators is not { Count: > 0 })
            {
                if (kind == PropertyKind.Constructor)
                    TsAddPendingConstructorParameterDecorators(tsParameterDecorators);
                else
                {
                    if (TsClassDecoratorTargetName(id) is { } decoratorClassName)
                    {
                        if (!TsTryAppendParameterDecoratorsToExistingMemberStatement(memberDecoratorStatements,
                                tsParameterDecorators, decoratorClassName, methodDecoratorKey, @static, computed))
                            memberDecoratorStatements.Add(TsBuildParameterDecorateStatement(tsParameterDecorators,
                                decoratorClassName, methodDecoratorKey, @static, computed));
                    }
                }
            }
        }

        if (instanceFieldInitializerStatements.Count > 0)
            TsInjectInstanceFieldInitializers(ref body, superClass != null, instanceFieldInitializerStatements, nodeStart,
                _lastTokEnd);

        if (_tsPendingDecoratedComputedClassKeyAssignments is { Count: > 0 })
        {
            var staticBody = new StructRefList<AstNode>();
            foreach (var assignment in _tsPendingDecoratedComputedClassKeyAssignments)
                staticBody.Add(new AstSimpleStatement(SourceFile, assignment.Start, assignment.End, assignment));
            body.Add(new AstStaticBlock(SourceFile, nodeStart, _lastTokEnd, ref staticBody));
            _tsPendingDecoratedComputedClassKeyAssignments = null;
        }

        if (memberDecoratorStatements.Count > 0)
        {
            TsOrderMemberDecoratorStatements(memberDecoratorStatements);
            _tsPendingClassDecoratorStatements ??= new List<AstStatement>();
            _tsPendingClassDecoratorStatements.AddRange(memberDecoratorStatements);
        }

        if (id == null && IsTypeScript && _tsDefaultExportClassNameUsed && _tsDefaultExportClassName != null)
            id = new AstSymbolRef(SourceFile, nodeStart, nodeStart, _tsDefaultExportClassName);

        if ((isStatement || isNullableId) && id != null)
        {
            return new AstDefClass(SourceFile, nodeStart, _lastTokEnd, new AstSymbolDefClass(id),
                superClass, ref body);
        }

        try
        {
            return new AstClassExpression(SourceFile, nodeStart, _lastTokEnd,
                id != null ? new AstSymbolDefClass(id) : null, superClass, ref body);
        }
        finally
        {
            _tsAnonymousClassStaticAccessorName = oldAnonymousClassStaticAccessorName;
        }
    }

    AstSymbol? ParseClassId(bool isStatement)
    {
        if (!isStatement && IsTypeScript && IsContextual("implements"))
            return null;
        if (Type == TokenType.Name)
            return ParseIdent();
        if (IsTypeScript && Type == TokenType.BraceL && _tsAllowAnonymousExportClassStatement)
            return null;
        if (isStatement)
        {
            Raise(Start, "A class name is required.");
        }

        return null;
    }

    AstNode? ParseClassSuper()
    {
        if (!Eat(TokenType.Extends)) return null;
        var result = ParseExpressionSubscripts(Start);
        if (IsTypeScript && Type == TokenType.Relational && "<".Equals(Value))
            TsTrySkipTypeParameters();
        return result;
    }

    // Parses module export declaration.
    AstStatement ParseExport(Position nodeStart, IDictionary<string, bool>? exports, bool topLevel)
    {
        if (IsTypeScript)
        {
            while (Type == TokenType.Export)
                Next();
        }

        if (IsTypeScript && (IsContextual("type") || IsContextual("interface")))
            _tsErasedTypeOnlyModuleSyntaxUsed = true;

        if (IsTypeScript && Eat(TokenType.Eq))
        {
            var value = ParseMaybeAssign(Start);
            Semicolon();
            return TsBuildAssignmentStatement(nodeStart, "module.exports", value);
        }

        // export * from '...'
        if (Eat(TokenType.Star))
        {
            var star = new AstSymbolImportForeign(SourceFile, _lastTokStart, _lastTokEnd, "*");
            var specifiers = new StructList<AstNameMapping>();
            if (EatContextual("as"))
            {
                specifiers.Add(new(SourceFile, _lastTokStart, _lastTokEnd,
                    new AstSymbolExportForeign(ParseIdent(true)), star));
            }
            else
            {
                specifiers.Add(new(SourceFile, _lastTokStart, _lastTokEnd,
                    new AstSymbolExportForeign(SourceFile, _lastTokStart, _lastTokEnd, "*"), star));
            }

            ExpectContextual("from");
            if (Type != TokenType.String)
            {
                if (IsTypeScript)
                {
                    TsSkipUntilStatementEnd();
                    _tsErasedTypeOnlyModuleSyntaxUsed = true;
                    return new AstTypeScriptOnly(SourceFile, nodeStart, _lastTokEnd);
                }
                Raise(Start, "Unexpected token");
            }
            var source = ParseExpressionAtom(Start) as AstString;
            var (attributes, attributeKeyword) = ParseImportAttributes();
            Semicolon();
            return new AstExport(SourceFile, nodeStart, _lastTokEnd, source, null, ref specifiers, attributes,
                attributeKeyword);
        }

        if (Eat(TokenType.Default))
        {
            // export default ...
            if (IsTypeScript && IsContextual("interface"))
            {
                _tsErasedTypeOnlyModuleSyntaxUsed = true;
                return TsParseTypeOnlyStatement(nodeStart);
            }
            if (IsTypeScript && Type == TokenType.Function &&
                TsTryParseFunctionOverloadStatement(nodeStart, out var typeOnlyStatement))
            {
                _tsErasedTypeOnlyModuleSyntaxUsed = true;
                return typeOnlyStatement;
            }
            CheckExport(exports, "default", _lastTokStart);
            var isAsync = false;
            AstNode declaration;
            if (IsTypeScript && Type == TokenType.Decorator)
            {
                _tsPendingClassDecorators = TsParseDecorators();
                if (IsContextual("abstract") && TsIsClassFollowing())
                    Next();
                if (Type != TokenType.Class)
                    Raise(Start, "Unexpected token");
                var oldDefaultClassName = _tsDefaultExportClassName;
                var oldDefaultClassNameUsed = _tsDefaultExportClassNameUsed;
                _tsDefaultExportClassName = "default_1";
                _tsDefaultExportClassNameUsed = true;
                try
                {
                    declaration = ParseClass(Start, false, true);
                }
                finally
                {
                    _tsDefaultExportClassName = oldDefaultClassName;
                    _tsDefaultExportClassNameUsed = oldDefaultClassNameUsed;
                }
            }
            else if (Type == TokenType.Function || (isAsync = IsAsyncFunction()))
            {
                var startLoc = Start;
                Next();
                if (isAsync) Next();
                if (IsTypeScript && !topLevel)
                {
                    var oldDefaultClassName = _tsDefaultExportClassName;
                    var oldDefaultClassNameUsed = _tsDefaultExportClassNameUsed;
                    _tsDefaultExportClassName = TsNextInvalidDefaultExportName();
                    _tsDefaultExportClassNameUsed = true;
                    try
                    {
                        declaration = ParseFunction(startLoc, false, true, false, isAsync);
                    }
                    finally
                    {
                        _tsDefaultExportClassName = oldDefaultClassName;
                        _tsDefaultExportClassNameUsed = oldDefaultClassNameUsed;
                    }
                }
                else
                {
                    declaration = ParseFunction(startLoc, false, true, false, isAsync);
                }
            }
            else if (Type == TokenType.Class)
            {
                if (IsTypeScript)
                {
                    var oldDefaultClassName = _tsDefaultExportClassName;
                    var oldDefaultClassNameUsed = _tsDefaultExportClassNameUsed;
                    _tsDefaultExportClassName = "default_1";
                    _tsDefaultExportClassNameUsed = true;
                    try
                    {
                        declaration = ParseClass(Start, false, true);
                    }
                    finally
                    {
                        _tsDefaultExportClassName = oldDefaultClassName;
                        _tsDefaultExportClassNameUsed = oldDefaultClassNameUsed;
                    }
                }
                else
                {
                    declaration = ParseClass(Start, false, true);
                }
            }
            else if (IsTypeScript && IsContextual("abstract") && TsIsClassFollowing())
            {
                var startLoc = Start;
                Next();
                declaration = ParseClass(startLoc, false, true);
            }
            else
            {
                if (IsTypeScript && Type == TokenType.Name && Value is { } exportDefaultName &&
                    TsIsErasedTypeOnlyName(exportDefaultName.ToString()!))
                {
                    TsSkipUntilStatementEnd();
                    _tsErasedTypeOnlyModuleSyntaxUsed = true;
                    return new AstTypeScriptOnly(SourceFile, nodeStart, _lastTokEnd);
                }
                declaration = ParseMaybeAssign(Start);
                Semicolon();
            }

            return new AstExport(SourceFile, nodeStart, _lastTokEnd, declaration, true);
        }
        else
        {
            // export var|const|let|function|class ...
            AstNode? declaration;
            var specifiers = new StructList<AstNameMapping>();
            AstString? source = null;
            AstObject? attributes = null;
            var attributeKeyword = "with";
            if (IsTypeScript && IsContextual("as"))
            {
                var index = End.Index;
                while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
                if (TsTextStartsKeyword(index, "namespace"))
                {
                    TsSkipUntilStatementEnd();
                    return new AstTypeScriptOnly(SourceFile, nodeStart, _lastTokEnd);
                }
            }
            if (IsTypeScript && Type == TokenType.Decorator)
            {
                _tsPendingClassDecorators = TsParseDecorators();
                if (Eat(TokenType.Default))
                {
                    CheckExport(exports, "default", _lastTokStart);
                    if (IsContextual("abstract") && TsIsClassFollowing())
                        Next();
                    if (Type != TokenType.Class)
                        Raise(Start, "Unexpected token");
                    var oldDefaultClassName = _tsDefaultExportClassName;
                    var oldDefaultClassNameUsed = _tsDefaultExportClassNameUsed;
                    _tsDefaultExportClassName = "default_1";
                    _tsDefaultExportClassNameUsed = true;
                    try
                    {
                        declaration = ParseClass(Start, false, true);
                    }
                    finally
                    {
                        _tsDefaultExportClassName = oldDefaultClassName;
                        _tsDefaultExportClassNameUsed = oldDefaultClassNameUsed;
                    }
                    return new AstExport(SourceFile, nodeStart, _lastTokEnd, declaration, true);
                }
                if (IsContextual("abstract") && TsIsClassFollowing())
                    Next();
                if (Type != TokenType.Class)
                    Raise(Start, "Unexpected token");
                declaration = ParseClass(Start, true, false);
                if (declaration is AstDefClass defClass)
                    CheckExport(exports, defClass.Name!.Name, defClass.Name.Start);
            }
            else if (IsTypeScript && IsContextual("abstract") && TsIsClassFollowing())
            {
                Next();
                declaration = ParseClass(Start, true, false);
                if (declaration is AstDefClass defClass)
                    CheckExport(exports, defClass.Name!.Name, defClass.Name.Start);
            }
            else if (IsTypeScript && TsIsTypeOnlyStatementStart())
            {
                _tsErasedTypeOnlyModuleSyntaxUsed = true;
                return TsParseTypeOnlyStatement(nodeStart);
            }
            else if (IsTypeScript && TsIsDeclareStatementStart())
            {
                _tsErasedTypeOnlyModuleSyntaxUsed = true;
                return TsParseDeclareStatement(nodeStart);
            }
            else if (IsTypeScript && TsTrySkipModifierBeforeImportEquals())
            {
                declaration = TsParseImportEqualsStatement(Start);
                if (declaration is AstTypeScriptOnly)
                    return (AstStatement)declaration;
                if (declaration is AstDefinitions variableDeclaration)
                    CheckVariableExport(exports, in variableDeclaration.Definitions);
            }
            else if (IsTypeScript && Options.ParseTypeScriptNamespaceBody && TsIsUsingDeclarationStart())
            {
                declaration = TsParseNamespaceExportUsingStatement(Start);
                if (declaration is AstDefinitions variableDeclaration)
                    CheckVariableExport(exports, in variableDeclaration.Definitions);
            }
            else if (IsTypeScript && TsIsImportEqualsStatementStart())
            {
                declaration = TsParseImportEqualsStatement(Start);
                if (declaration is AstTypeScriptOnly)
                    return (AstStatement)declaration;
                if (declaration is AstDefinitions variableDeclaration)
                    CheckVariableExport(exports, in variableDeclaration.Definitions);
            }
            else if (ShouldParseExportStatement())
            {
                if (IsTypeScript && Type == TokenType.Class && TsClassKeywordIsFollowedByBody())
                {
                    var oldDefaultClassName = _tsDefaultExportClassName;
                    var oldDefaultClassNameUsed = _tsDefaultExportClassNameUsed;
                    var oldAllowAnonymousExportClassStatement = _tsAllowAnonymousExportClassStatement;
                    _tsDefaultExportClassName = "default_1";
                    _tsDefaultExportClassNameUsed = true;
                    _tsAllowAnonymousExportClassStatement = true;
                    try
                    {
                        declaration = ParseStatement(true);
                    }
                    finally
                    {
                        _tsDefaultExportClassName = oldDefaultClassName;
                        _tsDefaultExportClassNameUsed = oldDefaultClassNameUsed;
                        _tsAllowAnonymousExportClassStatement = oldAllowAnonymousExportClassStatement;
                    }
                }
                else
                {
                    declaration = ParseStatement(true);
                }
                if (declaration is AstTypeScriptOnly)
                {
                    return (AstStatement)declaration;
                }
                else if (declaration is AstDefinitions variableDeclaration)
                {
                    CheckVariableExport(exports, in variableDeclaration.Definitions);
                }
                else
                {
                    var declarationNode = declaration is AstDefun defun ? defun.Name! :
                        declaration is AstDefClass defClass ? defClass.Name! :
                        (AstSymbolDeclaration)declaration;
                    CheckExport(exports, declarationNode.Name, declarationNode.Start);
                }
            }
            else
            {
                // export { x, y as z } [from '...']
                declaration = null;
                if (IsTypeScript && TsIsNamespaceStatementStart() &&
                    TsTryParseNamespaceStatements(out var namespaceStatements, forceExport: true))
                {
                    if (namespaceStatements.Count == 0)
                        return new AstTypeScriptOnly(SourceFile, nodeStart, _lastTokEnd);
                    if (namespaceStatements.Count == 1)
                        return namespaceStatements[0];

                    var body = new StructRefList<AstNode>();
                    foreach (var statement in namespaceStatements)
                        body.Add(statement);
                    return new AstBlockStatement(SourceFile, nodeStart, _lastTokEnd, ref body);
                }
                ParseExportSpecifiers(ref specifiers, exports, out var sawExportSpecifier);
                if (specifiers.Count == 0)
                {
                    if (EatContextual("from"))
                    {
                        if (Type != TokenType.String)
                            Raise(Start, "Unexpected token");
                        source = ParseExpressionAtom(Start) as AstString;
                        (attributes, attributeKeyword) = ParseImportAttributes();
                    }

                    Semicolon();
                    if (sawExportSpecifier)
                    {
                        _tsErasedTypeOnlyModuleSyntaxUsed = true;
                        return new AstTypeScriptOnly(SourceFile, nodeStart, _lastTokEnd);
                    }
                    return new AstExport(SourceFile, nodeStart, _lastTokEnd, source, null, ref specifiers,
                        attributes, attributeKeyword);
                }
                if (EatContextual("from"))
                {
                    if (Type != TokenType.String)
                        Raise(Start, "Unexpected token");
                    source = ParseExpressionAtom(Start) as AstString;
                    (attributes, attributeKeyword) = ParseImportAttributes();
                    foreach (var spec in specifiers)
                    {
                        spec.Name = new AstSymbolImportForeign(spec.Name);
                    }
                }
                else
                {
                    // check for keywords used as local names
                    foreach (var spec in specifiers)
                    {
                        CheckUnreserved(spec.Name.Start, spec.Name.End, spec.Name.Name);
                    }
                }

                Semicolon();
            }

            return new AstExport(SourceFile, nodeStart, _lastTokEnd, source, declaration, ref specifiers, attributes,
                attributeKeyword);
        }
    }

    (AstObject? Attributes, string Keyword) ParseImportAttributes()
    {
        var keyword = "with";
        if (Eat(TokenType.With))
        {
            keyword = "with";
        }
        else if (EatContextual("assert"))
        {
            keyword = "assert";
        }
        else
        {
            return (null, keyword);
        }

        if (Type != TokenType.BraceL)
            Raise(Start, "Unexpected token");
        return ((AstObject)ParseExpressionAtom(Start), keyword);
    }

    void CheckExport(IDictionary<string, bool>? exports, string name, Position pos)
    {
        if (exports == null) return;
        if (exports.ContainsKey(name) && !IsTypeScript)
            RaiseRecoverable(pos, "Duplicate export '" + name + "'");
        exports[name] = true;
    }

    void CheckPatternExport(IDictionary<string, bool> exports, AstNode pattern)
    {
        switch (pattern)
        {
            case AstSymbol identifierNode:
                CheckExport(exports, identifierNode.Name, pattern.Start);
                break;
            case AstDestructuring patternDest:
                foreach (var prop in patternDest.Names)
                {
                    CheckPatternExport(exports, prop);
                }

                break;
            case AstObjectKeyVal keyVal:
                CheckPatternExport(exports, keyVal.Value);
                break;
            case AstDefaultAssign assignmentPattern:
                CheckPatternExport(exports, assignmentPattern.Left);
                break;
            case AstExpansion expansion:
                CheckPatternExport(exports, expansion.Expression);
                break;
            case AstHole:
                break;
            default:
                throw new InvalidOperationException("checkPattenExport unhandled " + pattern);
        }
    }

    void CheckVariableExport(IDictionary<string, bool>? exports, in StructRefList<AstVarDef> decls)
    {
        if (exports == null)
            return;
        foreach (var decl in decls)
        {
            CheckPatternExport(exports, decl.Name);
        }
    }

    bool ShouldParseExportStatement()
    {
        return TokenInformation.Types[Type].Keyword == "var" ||
               TokenInformation.Types[Type].Keyword == "const" ||
               TokenInformation.Types[Type].Keyword == "class" ||
               TokenInformation.Types[Type].Keyword == "function" ||
               IsLet() ||
               IsAsyncFunction();
    }

    // Parses a comma-separated list of module exports.
    void ParseExportSpecifiers(ref StructList<AstNameMapping> nodes, IDictionary<string, bool>? exports,
        out bool sawSpecifier)
    {
        var first = true;
        sawSpecifier = false;
        // export { x, y as z } [from '...']
        Expect(TokenType.BraceL);
        while (!Eat(TokenType.BraceR))
        {
            if (!first)
            {
                Expect(TokenType.Comma);
                if (AfterTrailingComma(TokenType.BraceR)) break;
            }
            else first = false;

            sawSpecifier = true;
            var startLoc = Start;
            if (TsTypeSpecifierIsTypeOnly())
            {
                Next();
                _ = ParseModuleSpecifierName(out _);
                if (EatContextual("as"))
                    _ = ParseModuleSpecifierName(out _);
                continue;
            }
            var local = ParseModuleSpecifierName(out var localIsStringLiteral);
            var exportedIsStringLiteral = localIsStringLiteral;
            var exported = EatContextual("as")
                ? ParseModuleSpecifierName(out exportedIsStringLiteral)
                : local;
            if (IsTypeScript && TsIsErasedTypeOnlyName(local.Name))
            {
                _tsErasedTypeOnlyModuleSyntaxUsed = true;
                continue;
            }
            CheckExport(exports, exported.Name, exported.Start);
            nodes.Add(new AstNameMapping(SourceFile, startLoc, _lastTokEnd, new AstSymbolExportForeign(exported),
                new AstSymbolExport(local))
            {
                ForeignNameIsStringLiteral = exportedIsStringLiteral,
                NameIsStringLiteral = localIsStringLiteral
            });
        }
    }

    // Parses import declaration.
    AstStatement ParseImport(Position nodeStart)
    {
        // import '...'
        var importNames = new StructList<AstNameMapping>();
        AstSymbolImport? importName = null;
        AstString? source;
        AstObject? attributes;
        var attributeKeyword = "with";
        var isDefer = IsImportDeferKeyword() && EatContextual("defer");
        if (Type == TokenType.String)
        {
            if (isDefer)
                Raise(Start, "Unexpected token");
            source = (AstString)ParseExpressionAtom(Start);
            (attributes, attributeKeyword) = ParseImportAttributes();
        }
        else
        {
            var hasTypeOnlySpecifier = ParseImportSpecifiers(ref importNames, ref importName);
            ExpectContextual("from");
            if (Type == TokenType.String)
            {
                source = (AstString)ParseExpressionAtom(Start);
                (attributes, attributeKeyword) = ParseImportAttributes();
                if (hasTypeOnlySpecifier && importName == null && importNames.Count == 0)
                {
                    Semicolon();
                    _tsErasedTypeOnlyModuleSyntaxUsed = true;
                    return new AstTypeScriptOnly(SourceFile, nodeStart, _lastTokEnd);
                }
                if (IsTypeScript && importName == null && importNames.Count == 0)
                {
                    Semicolon();
                    _tsErasedTypeOnlyModuleSyntaxUsed = true;
                    return new AstTypeScriptOnly(SourceFile, nodeStart, _lastTokEnd);
                }
            }
            else
            {
                throw NewSyntaxError(Start, "Unexpected token");
            }
        }

        Semicolon();
        return new AstImport(SourceFile, nodeStart, _lastTokEnd, source, importName, ref importNames, attributes,
            isDefer, attributeKeyword);
    }

    bool IsImportDeferKeyword()
    {
        if (!IsContextual("defer"))
            return false;
        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index]))
            index++;
        return !TsTextStartsKeyword(_input, index, "from");
    }

    // Parses a comma-separated list of module imports.
    bool ParseImportSpecifiers(ref StructList<AstNameMapping> importNames, ref AstSymbolImport? importName)
    {
        var first = true;
        var hadTypeOnlySpecifier = false;
        if (Type == TokenType.Name)
        {
            // import defaultObj, { x, y as z } from '...'
            var local = ParseIdent();
            CheckLVal(local, true, VariableKind.Let);
            importName = new(local);
            if (!Eat(TokenType.Comma))
                return false;
        }

        if (Type == TokenType.Star)
        {
            var startLoc = Start;
            var starSymbol = new AstSymbolImportForeign(SourceFile, Start, End, "*");
            Next();
            ExpectContextual("as");
            var local = ParseIdent();
            CheckLVal(local, true, VariableKind.Let);
            importNames.Add(new(SourceFile, startLoc, _lastTokEnd, starSymbol,
                new AstSymbolImport(local)));
            return false;
        }

        Expect(TokenType.BraceL);
        while (!Eat(TokenType.BraceR))
        {
            if (!first)
            {
                Expect(TokenType.Comma);
                if (AfterTrailingComma(TokenType.BraceR)) break;
            }
            else first = false;

            var startLoc = Start;
            if (TsTypeSpecifierIsTypeOnly())
            {
                hadTypeOnlySpecifier = true;
                Next();
                _ = ParseModuleSpecifierName(out _);
                if (EatContextual("as"))
                    _ = ParseIdent();
                continue;
            }
            var imported = ParseModuleSpecifierName(out var importedIsStringLiteral);
            AstSymbol local;
            if (EatContextual("as"))
            {
                local = ParseIdent();
            }
            else
            {
                CheckUnreserved(imported.Start, imported.End, imported.Name);
                local = imported;
            }

            CheckLVal(local, true, VariableKind.Let);
            importNames.Add(new AstNameMapping(SourceFile, startLoc, _lastTokEnd, new AstSymbolImportForeign(imported),
                new AstSymbolImport(local))
            {
                ForeignNameIsStringLiteral = importedIsStringLiteral
            });
        }
        return hadTypeOnlySpecifier;
    }

    AstSymbol ParseModuleSpecifierName(out bool isStringLiteral)
    {
        isStringLiteral = false;
        if (Type == TokenType.String)
        {
            var value = (AstString)ParseExpressionAtom(Start);
            isStringLiteral = true;
            return new AstSymbolRef(SourceFile, value.Start, value.End, value.Value);
        }

        return ParseIdent(true);
    }

    bool IsUseStrictDirective(AstNode statement)
    {
        var literal2 = (AstString)((AstSimpleStatement)statement).Body;
        return literal2.Value == "use strict";
    }

    bool IsDirectiveCandidate(AstNode statement)
    {
        return statement is AstSimpleStatement expressionStatementNode &&
               expressionStatementNode.Body is AstString &&
               // Reject parenthesized strings.
               (_input[statement.Start.Index] == '\"' || _input[statement.Start.Index] == '\'');
    }
}
