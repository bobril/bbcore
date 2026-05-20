using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Njsast;
using Njsast.Ast;
using Njsast.Output;
using Njsast.Runtime;

namespace Njsast.Reader;

public sealed partial class Parser
{
    bool IsTypeScript => Options.ParseTypeScript;

    AstStatement TsParseTypeOnlyStatement(Position startLocation)
    {
        TsRememberErasedTypeOnlyName();
        TsSkipTypeDeclaration();
        return new AstTypeScriptOnly(SourceFile, startLocation, _lastTokEnd);
    }

    void TsRememberErasedTypeOnlyName()
    {
        if (!IsContextual("interface") && !IsContextual("type"))
            return;

        var index = End.Index;
        index = TsSkipWhitespaceAndComments(index);
        if (index >= _input.Length || !IsIdentifierStart(_input[index], true))
            return;

        var start = index++;
        while (index < _input.Length && IsIdentifierChar(_input[index], true))
            index++;

        _tsErasedTypeOnlyNames ??= new HashSet<string>(StringComparer.Ordinal);
        _tsErasedTypeOnlyNames.Add(_input[start..index]);
    }

    bool TsIsErasedTypeOnlyName(string name)
    {
        return _tsErasedTypeOnlyNames != null && _tsErasedTypeOnlyNames.Contains(name);
    }

    void TsForgetErasedTypeOnlyValueNames(AstDefinitions definitions)
    {
        if (_tsErasedTypeOnlyNames == null)
            return;
        foreach (var definition in definitions.Definitions.AsReadOnlySpan())
            TsForgetErasedTypeOnlyValueName(definition.Name);
    }

    void TsForgetErasedTypeOnlyValueName(AstNode node)
    {
        switch (node)
        {
            case AstSymbolDeclaration symbol:
                _tsErasedTypeOnlyNames?.Remove(symbol.Name);
                break;
            case AstExpansion expansion:
                TsForgetErasedTypeOnlyValueName(expansion.Expression);
                break;
            case AstDefaultAssign defaultAssign:
                TsForgetErasedTypeOnlyValueName(defaultAssign.Left);
                break;
            case AstDestructuring destructuring:
                foreach (var name in destructuring.Names.AsReadOnlySpan())
                    TsForgetErasedTypeOnlyValueName(name);
                break;
            case AstObjectKeyVal keyValue:
                TsForgetErasedTypeOnlyValueName(keyValue.Value);
                break;
        }
    }

    void TsInsertEmptyExportModuleMarker(ref StructRefList<AstNode> body)
    {
        var specifiers = new StructList<AstNameMapping>();
        body.Add(new AstExport(SourceFile, _lastTokEnd, _lastTokEnd, null, null, ref specifiers));
        _tsRuntimeModuleSyntaxUsed = true;
    }

    bool TsIsTypeOnlyStatementStart()
    {
        if (!IsTypeScript)
            return false;
        if (IsContextual("interface"))
            return true;
        if (!IsContextual("type"))
            return false;

        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index]))
        {
            if (IsNewLine(_input[index]))
                return false;
            index++;
        }

        return index < _input.Length && IsIdentifierStart(_input[index], true);
    }

    bool TsIsDeclareStatementStart()
    {
        if (!IsTypeScript || !IsContextual("declare"))
            return false;

        var index = TsSkipWhitespaceAndComments(End.Index);
        if (index >= _input.Length)
            return false;
        return TsTextStartsKeyword(index, "abstract") ||
               TsTextStartsKeyword(index, "class") ||
               TsTextStartsKeyword(index, "const") ||
               TsTextStartsKeyword(index, "declare") ||
               TsTextStartsKeyword(index, "enum") ||
               TsTextStartsKeyword(index, "export") ||
               TsTextStartsKeyword(index, "function") ||
               TsTextStartsKeyword(index, "global") ||
               TsTextStartsKeyword(index, "import") ||
               TsTextStartsKeyword(index, "interface") ||
               TsTextStartsKeyword(index, "let") ||
               TsTextStartsKeyword(index, "module") ||
               TsTextStartsKeyword(index, "namespace") ||
               TsTextStartsKeyword(index, "type") ||
               TsTextStartsKeyword(index, "var");
    }

    bool TsIsNamespaceStatementStart()
    {
        if (!IsTypeScript || (!IsContextual("namespace") && !IsContextual("module")))
            return false;
        var index = TsSkipWhitespaceAndComments(End.Index);
        return index < _input.Length && IsIdentifierStart(_input[index], true);
    }

    bool TsIsQuotedModuleDeclarationStart()
    {
        if (!IsTypeScript || !IsContextual("module"))
            return false;
        var index = TsSkipWhitespaceAndComments(End.Index);
        return index < _input.Length && _input[index] is '"' or '\'';
    }

    bool TsIsGlobalAugmentationStatementStart()
    {
        if (!IsTypeScript || !IsContextual("global"))
            return false;
        var index = TsSkipWhitespaceAndComments(End.Index);
        return index < _input.Length && _input[index] == '{';
    }

    AstStatement TsParseGlobalAugmentationStatement(Position startLocation)
    {
        Next();
        TsSkipAmbientDeclarationBlock();
        return new AstTypeScriptOnly(SourceFile, startLocation, _lastTokEnd);
    }

    AstStatement TsParseQuotedModuleDeclaration(Position startLocation)
    {
        Next();
        if (Type == TokenType.String)
            Next();
        TsSkipAmbientDeclarationBlock();
        return new AstTypeScriptOnly(SourceFile, startLocation, _lastTokEnd);
    }

    bool TsClassKeywordIsFollowedByBody()
    {
        if (!IsTypeScript || Type != TokenType.Class)
            return false;
        var index = TsSkipWhitespaceAndComments(End.Index);
        return index < _input.Length && _input[index] == '{';
    }

    bool TsIsImportEqualsStatementStart()
    {
        if (!IsTypeScript || Type != TokenType.Import)
            return false;
        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        if (TsTextStartsKeyword(index, "type"))
        {
            index += "type".Length;
            while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        }

        if (index >= _input.Length || !IsIdentifierStart(_input[index], true))
            return false;
        index++;
        while (index < _input.Length && IsIdentifierChar(_input[index], true)) index++;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        return index < _input.Length && _input[index] == '=';
    }

    bool TsTrySkipModifierBeforeImportEquals()
    {
        if (!IsTypeScript || Type != TokenType.Name ||
            !(IsContextual("public") || IsContextual("private") || IsContextual("protected") ||
              IsContextual("static")))
            return false;
        var index = TsSkipWhitespaceAndComments(End.Index);
        if (!TsTextStartsKeyword(index, "import"))
            return false;
        Next();
        return TsIsImportEqualsStatementStart();
    }

    bool TsClassMethodSignatureIsFollowedByBody()
    {
        if (!IsTypeScript || Type != TokenType.ParenL)
            return false;
        var after = TsFindMatchingSkippingLiterals(Start.Index, '(', ')');
        if (after < 0)
            return false;
        after = TsSkipWhitespaceAndComments(after + 1);
        if (after < _input.Length && _input[after] == ':')
            after = TsSkipWhitespaceAndComments(TsSkipTypeInText(after + 1));
        return after < _input.Length && _input[after] == '{';
    }

    AstStatement TsParseImportEqualsStatement(Position startLocation)
    {
        Expect(TokenType.Import);
        if (IsContextual("type"))
        {
            TsSkipUntilStatementEnd();
            _tsErasedTypeOnlyModuleSyntaxUsed = true;
            return new AstTypeScriptOnly(SourceFile, startLocation, _lastTokEnd);
        }

        if (Type != TokenType.Name)
            Raise(Start, "Unexpected token");

        var alias = ParseIdent();
        Expect(TokenType.Eq);

        if (IsContextual("require"))
        {
            var requireStart = Start;
            Next();
            Expect(TokenType.ParenL);
            if (Type != TokenType.String)
                Raise(Start, "Unexpected token");
            var moduleName = Value?.ToString() ?? "";
            var moduleNameStart = Start;
            Next();
            Expect(TokenType.ParenR);
            Semicolon();

            var callArgs = new StructList<AstNode>();
            callArgs.Add(new AstString(SourceFile, moduleNameStart, moduleNameStart, moduleName));
            var requireCall = new AstCall(SourceFile, requireStart, _lastTokEnd,
                new AstSymbolRef(SourceFile, requireStart, requireStart, "require"), ref callArgs);
            var requireDeclarations = new StructRefList<AstVarDef>();
            var requireSymbol = new AstSymbolVar(alias);
            requireDeclarations.Add(new AstVarDef(SourceFile, alias.Start, _lastTokEnd, requireSymbol, requireCall));
            Options.ParsedTypeScriptImportEquals = true;
            return new AstTypeScriptImportEqualsConst(SourceFile, startLocation, _lastTokEnd, ref requireDeclarations);
        }

        if (Type == TokenType.Name &&
            _tsErasedTypeOnlyNamespaces != null &&
            _tsErasedTypeOnlyNamespaces.Contains(Value!.ToString()!))
        {
            TsSkipUntilStatementEnd();
            return new AstTypeScriptOnly(SourceFile, startLocation, _lastTokEnd);
        }

        var value = ParseExpression(Start);
        Semicolon();

        var declarations = new StructRefList<AstVarDef>();
        var symbol = new AstSymbolVar(alias);
        declarations.Add(new AstVarDef(SourceFile, alias.Start, _lastTokEnd, symbol, value));
        Options.ParsedTypeScriptImportEquals = true;
        return new AstTypeScriptImportEquals(SourceFile, startLocation, _lastTokEnd, ref declarations);
    }

    bool TsIsUsingDeclarationStart()
    {
        if (!IsTypeScript)
            return false;

        var index = Start.Index;
        if (IsContextual("await"))
        {
            index = End.Index;
            while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
            if (!TsTextStartsKeyword(index, "using"))
                return false;
            index += "using".Length;
        }
        else if (IsContextual("using"))
        {
            index = End.Index;
        }
        else
        {
            return false;
        }

        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        if (index >= _input.Length)
            return false;

        return IsIdentifierStart(_input[index], true) ||
               _input[index] == CharCode.LeftCurlyBracket;
    }

    bool TsIsAwaitArrayUsingExpressionStart()
    {
        if (!IsTypeScript || !IsContextual("await"))
            return false;

        var index = TsSkipWhitespaceAndComments(End.Index);
        if (!TsTextStartsKeyword(index, "using"))
            return false;
        index = TsSkipWhitespaceAndComments(index + "using".Length);
        return index < _input.Length && _input[index] == CharCode.LeftSquareBracket;
    }

    AstStatement TsParseAwaitArrayUsingExpressionStatementAsRaw(Position start)
    {
        var end = TsFindStatementEndIndex(start.Index, stopAtLineBreak: false);
        var source = _input.Substring(start.Index, end - start.Index).Trim();
        while (Type != TokenType.Eof && _lastTokEnd.Index < end)
            Next();
        Eat(TokenType.Semi);

        return new AstRawStatement(SourceFile, start, _lastTokEnd, TsRewriteAwaitArrayUsingExpression(source));
    }

    static string TsRewriteAwaitArrayUsingExpression(string source)
    {
        var equals = TsFindTopLevelChar(source, '=');
        if (equals < 0)
            return Regex.Replace(source, @"\busing\s+\[", "using[") + ";";

        var left = Regex.Replace(source[..equals].Trim(), @"\busing\s+\[", "using[");
        var right = source[(equals + 1)..].Trim();
        return left + "; " + right.TrimEnd(';') + ";";
    }

    static int TsFindTopLevelChar(string source, char target)
    {
        var brace = 0;
        var bracket = 0;
        var paren = 0;
        for (var i = 0; i < source.Length; i++)
        {
            var ch = source[i];
            if (ch is '"' or '\'' or '`')
            {
                i = TsSkipStringLike(source, i, ch) - 1;
                continue;
            }
            if (ch == '{') brace++;
            else if (ch == '}') brace--;
            else if (ch == '[') bracket++;
            else if (ch == ']') bracket--;
            else if (ch == '(') paren++;
            else if (ch == ')') paren--;
            else if (ch == target && brace == 0 && bracket == 0 && paren == 0)
                return i;
        }

        return -1;
    }

    bool TsIsForUsingDeclarationStart()
    {
        if (!IsTypeScript)
            return false;
        var index = End.Index;
        if (IsContextual("await"))
        {
            index = TsSkipWhitespaceAndComments(index);
            if (!TsTextStartsKeyword(index, "using"))
                return false;
            index += "using".Length;
        }
        else if (IsContextual("using"))
        {
            index = End.Index;
        }
        else
        {
            return false;
        }

        index = TsSkipWhitespaceAndComments(index);
        return index < _input.Length &&
               (IsIdentifierStart(_input[index], true) || _input[index] == CharCode.LeftCurlyBracket);
    }

    bool TsIsForArrayUsingExpressionStart()
    {
        if (!IsTypeScript)
            return false;

        var index = End.Index;
        if (IsContextual("await"))
        {
            index = TsSkipWhitespaceAndComments(index);
            if (!TsTextStartsKeyword(index, "using"))
                return false;
            index += "using".Length;
        }
        else if (IsContextual("using"))
        {
            index = End.Index;
        }
        else
        {
            return false;
        }

        index = TsSkipWhitespaceAndComments(index);
        return index < _input.Length && _input[index] == CharCode.LeftSquareBracket;
    }

    AstStatement TsParsePreservedForArrayUsingStatement(Position nodeStart, bool forAwait)
    {
        var end = TsFindForStatementEndIndex(nodeStart.Index);
        while (Type != TokenType.Eof && _lastTokEnd.Index < end)
            Next();
        ExitLexicalScope();

        var raw = _input.Substring(nodeStart.Index, end - nodeStart.Index).TrimEnd();
        raw = Regex.Replace(raw, @"\busing\s+\[", "using[");
        if (forAwait && TsForHeaderHasTopLevelIn(raw))
            raw = TsRemoveForAwaitFromPreservedForStatement(raw);
        return new AstRawStatement(SourceFile, nodeStart, _lastTokEnd, raw);
    }

    static bool TsForHeaderHasTopLevelIn(string raw)
    {
        var headerStart = raw.IndexOf('(');
        if (headerStart < 0)
            return false;

        var brace = 0;
        var bracket = 0;
        var paren = 0;
        for (var i = headerStart + 1; i < raw.Length; i++)
        {
            var ch = raw[i];
            if (ch is '"' or '\'' or '`')
            {
                i = TsSkipStringLike(raw, i, ch) - 1;
                continue;
            }
            if (ch == '{') brace++;
            else if (ch == '}') brace--;
            else if (ch == '[') bracket++;
            else if (ch == ']') bracket--;
            else if (ch == '(') paren++;
            else if (ch == ')')
            {
                if (brace == 0 && bracket == 0 && paren == 0)
                    return false;
                paren--;
            }
            else if (brace == 0 && bracket == 0 && paren == 0 && TsTextStartsKeyword(raw, i, "in"))
            {
                return true;
            }
        }

        return false;
    }

    bool TsExportStartsUsingDeclaration()
    {
        if (!IsTypeScript || Type != TokenType.Export)
            return false;

        var index = TsSkipWhitespaceAndComments(End.Index);
        if (TsTextStartsKeyword(index, "await"))
        {
            index += "await".Length;
            index = TsSkipWhitespaceAndComments(index);
        }

        return TsTextStartsKeyword(index, "using");
    }

    AstDefinitions TsParseNamespaceExportUsingStatement(Position startLocation)
    {
        if (IsContextual("await"))
            Next();
        ExpectContextual("using");

        var definitions = new StructRefList<AstVarDef>();
        for (;;)
        {
            var declStart = Start;
            var id = ParseBindingAtom();
            TsTrySkipOptionalOrDefiniteBindingMarker();
            TsTrySkipTypeAnnotation();
            Expect(TokenType.Eq);
            var init = ParseMaybeAssign(Start);
            CheckLVal(id, true, VariableKind.Var);
            definitions.Add(new AstVarDef(SourceFile, declStart, _lastTokEnd,
                ToRightDeclarationSymbolKind(id, VariableKind.Var), init));
            if (!Eat(TokenType.Comma))
                break;
        }
        Semicolon();
        return new AstVar(SourceFile, startLocation, _lastTokEnd, ref definitions);
    }

    AstStatement TsParseUsingDeclarationAsPreservedStatement()
    {
        var start = Start;
        var end = TsFindStatementEndIndex(start.Index);
        while (Type != TokenType.Eof && _lastTokEnd.Index < end)
            Next();
        Eat(TokenType.Semi);

        var source = _input.Substring(start.Index, end - start.Index).TrimEnd();
        if (!source.EndsWith(';'))
            source += ";";
        return new AstRawStatement(SourceFile, start, _lastTokEnd, source);
    }

    int TsFindStatementEndIndex(int start, bool stopAtLineBreak = true)
    {
        var index = start;
        var brace = 0;
        var bracket = 0;
        var paren = 0;
        while (index < _input.Length)
        {
            var ch = _input[index];
            if (ch is '"' or '\'')
            {
                index = TsSkipStringLike(index, ch);
                continue;
            }
            if (ch == '`')
            {
                index = TsSkipTemplateLiteral(index);
                continue;
            }
            if (ch == '/' && index + 1 < _input.Length)
            {
                if (_input[index + 1] == '/')
                {
                    if (stopAtLineBreak)
                        return index;
                    index += 2;
                    while (index < _input.Length && _input[index] is not '\n' and not '\r') index++;
                    continue;
                }
                if (_input[index + 1] == '*')
                {
                    index += 2;
                    while (index + 1 < _input.Length && !(_input[index] == '*' && _input[index + 1] == '/')) index++;
                    index = Math.Min(index + 2, _input.Length);
                    continue;
                }
                if (TsCanStartRegexLiteralAt(index))
                {
                    index = TsSkipRegexLiteral(index);
                    continue;
                }
            }
            if (ch == '{') brace++;
            else if (ch == '}')
            {
                if (brace == 0)
                    return index;
                brace--;
            }
            else if (ch == '[') bracket++;
            else if (ch == ']')
            {
                if (bracket > 0)
                    bracket--;
            }
            else if (ch == '(') paren++;
            else if (ch == ')')
            {
                if (paren > 0)
                    paren--;
            }
            else if (ch == ';' && brace == 0 && bracket == 0 && paren == 0)
            {
                return index;
            }
            else if (stopAtLineBreak && (ch == '\n' || ch == '\r') && brace == 0 && bracket == 0 && paren == 0)
            {
                return index;
            }

            index++;
        }
        return index;
    }

    AstStatement TsParseForUsingStatement(Position nodeStart, bool forAwait)
    {
        var usingStart = Start;
        var isAwaitUsing = IsContextual("await");
        if (isAwaitUsing)
            Next();
        ExpectContextual("using");

        var bindingStart = Start;
        var id = ParseBindingAtom();
        var bindingEnd = _lastTokEnd;
        TsTrySkipOptionalOrDefiniteBindingMarker();
        TsTrySkipForUsingTypeAnnotation();
        CheckLVal(id, true, VariableKind.Const);
        if (Type == TokenType.In)
            return TsParsePreservedForUsingInStatement(nodeStart, forAwait);
        if (Eat(TokenType.Eq))
        {
            _tsDisposeResourcesHelperUsed = true;
            return TsParseForUsingInitializerStatement(nodeStart, usingStart, isAwaitUsing, id, bindingStart,
                bindingEnd);
        }

        _tsDisposeResourcesHelperUsed = true;
        ExpectContextual("of");
        var right = ParseExpression(Start);
        Expect(TokenType.ParenR);
        ExitLexicalScope();

        var backupAllowBreak = _allowBreak;
        var backupAllowContinue = _allowContinue;
        _allowBreak = true;
        _allowContinue = true;
        var originalBody = ParseStatement(false);
        _allowBreak = backupAllowBreak;
        _allowContinue = backupAllowContinue;

        var isDestructuring = id is AstDestructuring;
        var iterName = isDestructuring ? "_a" : TsNewUsingForOfValueName(((AstSymbol)id).Name);
        var loopDefinitions = new StructRefList<AstVarDef>();
        loopDefinitions.Add(new AstVarDef(SourceFile, bindingStart, bindingStart,
            new AstSymbolConst(new AstSymbolRef(SourceFile, bindingStart, bindingStart, iterName))));
        var loopInit = new AstConst(SourceFile, bindingStart, bindingStart, ref loopDefinitions);

        var envName = TsAllocateUsingEnvName(topLevel: false);
        var errorName = TsAllocateUsingErrorName(topLevel: false);

        var tryBody = new StructRefList<AstNode>();
        if (isDestructuring)
        {
            var bindingSource = _input.Substring(bindingStart.Index, bindingEnd.Index - bindingStart.Index);
            var rawUsing = (isAwaitUsing ? "await " : "") + "using " + bindingSource + " = " + iterName + ";";
            tryBody.Add(new AstRawStatement(SourceFile, usingStart, bindingEnd, rawUsing));
        }
        else
        {
            var symbol = (AstSymbol)id;
            var scopedDefinitions = new StructRefList<AstVarDef>();
            scopedDefinitions.Add(new AstVarDef(SourceFile, symbol.Start, symbol.End,
                ToRightDeclarationSymbolKind(id, VariableKind.Const),
                TsBuildAddDisposableResourceCall(usingStart, envName,
                    new AstSymbolRef(SourceFile, bindingStart, bindingStart, iterName), isAwaitUsing)));
            tryBody.Add(new AstConst(SourceFile, usingStart, symbol.End, ref scopedDefinitions));
        }

        if (originalBody is AstBlock block)
        {
            foreach (var statement in block.Body.AsReadOnlySpan())
                tryBody.Add(statement);
        }
        else
        {
            tryBody.Add(originalBody);
        }

        var loopBodyStatements = new StructRefList<AstNode>();
        loopBodyStatements.Add(TsBuildUsingEnvDeclaration(usingStart, envName));
        loopBodyStatements.Add(TsBuildUsingTry(usingStart, envName, errorName, isAwaitUsing, ref tryBody));
        var loopBody = new AstBlockStatement(SourceFile, usingStart, _lastTokEnd, ref loopBodyStatements);
        return new AstForOf(SourceFile, nodeStart, _lastTokEnd, loopBody, loopInit, right, forAwait);
    }

    AstStatement TsParsePreservedForUsingInStatement(Position nodeStart, bool forAwait)
    {
        var end = TsFindForStatementEndIndex(nodeStart.Index);
        while (Type != TokenType.Eof && _lastTokEnd.Index < end)
            Next();
        ExitLexicalScope();
        var raw = _input.Substring(nodeStart.Index, end - nodeStart.Index).TrimEnd();
        if (forAwait)
            raw = TsRemoveForAwaitFromPreservedForStatement(raw);
        return new AstRawStatement(SourceFile, nodeStart, _lastTokEnd, raw);
    }

    static string TsRemoveForAwaitFromPreservedForStatement(string raw)
    {
        var index = "for".Length;
        while (index < raw.Length && char.IsWhiteSpace(raw[index]))
            index++;
        if (index + "await".Length > raw.Length ||
            !raw.AsSpan(index, "await".Length).SequenceEqual("await".AsSpan()) ||
            index + "await".Length < raw.Length && IsIdentifierChar(raw[index + "await".Length]))
            return raw;
        var afterAwait = index + "await".Length;
        while (afterAwait < raw.Length && char.IsWhiteSpace(raw[afterAwait]))
            afterAwait++;
        return "for " + raw[afterAwait..];
    }

    int TsFindForStatementEndIndex(int start)
    {
        var headerStart = _input.IndexOf('(', start);
        if (headerStart < 0)
            return TsFindStatementEndIndex(start, stopAtLineBreak: false);
        var headerEnd = TsFindMatchingSkippingLiterals(headerStart, '(', ')');
        if (headerEnd < 0)
            return TsFindStatementEndIndex(start, stopAtLineBreak: false);
        var bodyStart = TsSkipWhitespaceAndComments(headerEnd + 1);
        if (bodyStart < _input.Length && _input[bodyStart] == '{')
        {
            var bodyEnd = TsFindMatchingSkippingLiterals(bodyStart, '{', '}');
            if (bodyEnd >= 0)
                return bodyEnd + 1;
        }

        var statementEnd = TsFindStatementEndIndex(bodyStart, stopAtLineBreak: false);
        return statementEnd < _input.Length && _input[statementEnd] == ';'
            ? statementEnd + 1
            : statementEnd;
    }

    AstStatement TsParseForUsingInitializerStatement(Position nodeStart, Position usingStart, bool isAwaitUsing,
        AstNode firstId, Position firstDeclStart, Position firstBindingEnd)
    {
        var envName = TsAllocateUsingEnvName(topLevel: false);
        var errorName = TsAllocateUsingErrorName(topLevel: false);
        var scopedDefinitions = new StructRefList<AstVarDef>();
        var rawDeclarations = new List<string>();
        var hasDestructuring = firstId is AstDestructuring;
        ParseForUsingInitializerDefinition(usingStart, envName, isAwaitUsing, firstId, firstDeclStart,
            firstBindingEnd, ref scopedDefinitions, rawDeclarations);
        while (Eat(TokenType.Comma))
        {
            var declStart = Start;
            var id = ParseBindingAtom();
            var bindingEnd = _lastTokEnd;
            TsTrySkipOptionalOrDefiniteBindingMarker();
            TsTrySkipTypeAnnotation();
            hasDestructuring |= id is AstDestructuring;
            CheckLVal(id, true, VariableKind.Const);
            Expect(TokenType.Eq);
            ParseForUsingInitializerDefinition(usingStart, envName, isAwaitUsing, id, declStart, bindingEnd,
                ref scopedDefinitions, rawDeclarations);
        }

        var tryBody = new StructRefList<AstNode>();
        if (hasDestructuring)
        {
            var rawUsing = (isAwaitUsing ? "await " : "") + "using " + string.Join(", ", rawDeclarations) + ";";
            tryBody.Add(new AstRawStatement(SourceFile, usingStart, _lastTokEnd, rawUsing));
        }
        else
        {
            tryBody.Add(new AstConst(SourceFile, usingStart, _lastTokEnd, ref scopedDefinitions));
        }
        tryBody.Add(ParseFor(nodeStart, null, isAwait: false));

        var body = new StructRefList<AstNode>();
        body.Add(TsBuildUsingEnvDeclaration(usingStart, envName));
        body.Add(TsBuildUsingTry(usingStart, envName, errorName, isAwaitUsing, ref tryBody));
        return new AstBlockStatement(SourceFile, nodeStart, _lastTokEnd, ref body);
    }

    void ParseForUsingInitializerDefinition(Position usingStart, string envName, bool isAwaitUsing, AstNode id,
        Position declStart, Position bindingEnd, ref StructRefList<AstVarDef> scopedDefinitions,
        List<string> rawDeclarations)
    {
        var initStart = Start;
        var init = ParseMaybeAssign(Start);
        var convertedId = ToRightDeclarationSymbolKind(id, VariableKind.Const);
        var value = id is AstDestructuring
            ? init
            : TsBuildAddDisposableResourceCall(usingStart, envName, init, isAwaitUsing);
        scopedDefinitions.Add(new AstVarDef(SourceFile, declStart, _lastTokEnd, convertedId, value));
        rawDeclarations.Add(_input.Substring(declStart.Index, bindingEnd.Index - declStart.Index).Trim() +
                            " = " +
                            _input.Substring(initStart.Index, _lastTokEnd.Index - initStart.Index).Trim());
    }

    string TsNewUsingForOfValueName(string baseName)
    {
        _tsUsingForOfValueIndexes ??= new Dictionary<string, int>(StringComparer.Ordinal);
        _tsUsingForOfValueIndexes.TryGetValue(baseName, out var index);
        index++;
        _tsUsingForOfValueIndexes[baseName] = index;
        return baseName + "_" + index.ToString(CultureInfo.InvariantCulture);
    }

    void TsTrySkipForUsingTypeAnnotation()
    {
        if (!IsTypeScript || !Eat(TokenType.Colon))
            return;

        var angle = 0;
        var brace = 0;
        var paren = 0;
        var bracket = 0;
        while (Type != TokenType.Eof)
        {
            if (angle == 0 && brace == 0 && paren == 0 && bracket == 0 &&
                (IsContextual("of") || Type is TokenType.Eq or TokenType.In or TokenType.Semi))
                return;

            if (Type == TokenType.Relational && "<".Equals(Value)) angle++;
            else if (Type == TokenType.Relational && ">".Equals(Value) && angle > 0) angle--;
            else if (Type == TokenType.BackQuote)
            {
                TsMoveToIndex(TsSkipTemplateLiteral(Start.Index));
                continue;
            }
            else if (Type == TokenType.BitShift && angle > 0)
            {
                var value = Value?.ToString();
                if (value == ">>") angle = Math.Max(0, angle - 2);
                else if (value == ">>>") angle = Math.Max(0, angle - 3);
            }
            else if (Type == TokenType.BraceL) brace++;
            else if (Type == TokenType.BraceR)
            {
                if (brace == 0) return;
                brace--;
            }
            else if (Type == TokenType.ParenL) paren++;
            else if (Type == TokenType.ParenR)
            {
                if (paren == 0) return;
                paren--;
            }
            else if (Type == TokenType.BracketL) bracket++;
            else if (Type == TokenType.BracketR)
            {
                if (bracket == 0) return;
                bracket--;
            }

            Next();
        }
    }

    void TsMoveToIndex(int index)
    {
        var line = _pos.Line;
        var column = _pos.Column;
        for (var i = _pos.Index; i < index && i < _input.Length; i++)
        {
            var ch = _input[i];
            if (IsNewLine(ch))
            {
                line++;
                column = 0;
                if (ch == '\r' && i + 1 < index && _input[i + 1] == '\n')
                    i++;
            }
            else
            {
                column++;
            }
        }
        _pos = new Position(line, column, index);
        if (CurContext() == TokContext.QTmpl)
            _context.Pop();
        NextToken();
    }

    List<AstStatement> TsParseUsingScope(bool topLevel, Func<bool> isScopeEnd)
    {
        _tsDisposeResourcesHelperUsed = true;
        var startLocation = Start;
        var isAwait = IsContextual("await");
        if (isAwait)
            Next();
        ExpectContextual("using");

        var envName = TsAllocateUsingEnvName(topLevel);
        var errorName = TsAllocateUsingErrorName(topLevel);

        var declarations = new StructRefList<AstVarDef>();
        var topLevelModuleStatements = new List<AstStatement>();
        var topLevelDeclarationsAfterUsingVar = new List<AstStatement>();
        var tryBody = new StructRefList<AstNode>();
        var hasAwaitUsing = isAwait;
        TsParseUsingDeclarationIntoScope(startLocation, envName, topLevel, isAwait, ref declarations, ref tryBody);

        while (!isScopeEnd())
        {
            if (topLevel && TsTryParseTopLevelModuleStatementInUsingScope(topLevelModuleStatements,
                    topLevelDeclarationsAfterUsingVar, ref tryBody, ref declarations))
                continue;

            if (topLevel && Type == TokenType.Export && TsExportStartsUsingDeclaration())
            {
                _tsErasedTypeOnlyModuleSyntaxUsed = true;
                Next();
                var nestedAwait = IsContextual("await");
                if (nestedAwait)
                    Next();
                ExpectContextual("using");
                hasAwaitUsing |= nestedAwait;
                TsParseUsingDeclarationIntoScope(Start, envName, topLevel, nestedAwait, ref declarations,
                    ref tryBody);
                continue;
            }

            if (TsIsUsingDeclarationStart())
            {
                var nestedAwait = IsContextual("await");
                if (nestedAwait)
                    Next();
                ExpectContextual("using");
                hasAwaitUsing |= nestedAwait;
                TsParseUsingDeclarationIntoScope(Start, envName, topLevel, nestedAwait, ref declarations,
                    ref tryBody);
                continue;
            }

            tryBody.Add(ParseStatement(true, Options.ParseTypeScriptNamespaceBody));
        }

        var result = new List<AstStatement>();
        result.AddRange(topLevelModuleStatements);
        if (topLevel)
            result.Add(new AstVar(SourceFile, startLocation, _lastTokEnd, ref declarations));
        result.AddRange(topLevelDeclarationsAfterUsingVar);
        result.Add(TsBuildUsingEnvDeclaration(startLocation, envName));
        result.Add(TsBuildUsingTry(startLocation, envName, errorName, hasAwaitUsing, ref tryBody, topLevel));
        return result;
    }

    bool TsTryParseTopLevelModuleStatementInUsingScope(List<AstStatement> moduleStatements,
        List<AstStatement> declarationsAfterUsingVar, ref StructRefList<AstNode> tryBody,
        ref StructRefList<AstVarDef> declarations)
    {
        if (Type == TokenType.Import)
        {
            if (TsCurrentImportTokenStartsExpression())
                return false;

            var statement = TsIsImportEqualsStatementStart()
                ? TsParseImportEqualsStatement(Start)
                : ParseStatement(true, true);
            if (statement is AstTypeScriptOnly)
            {
                _tsErasedTypeOnlyModuleSyntaxUsed = true;
            }
            else
            {
                moduleStatements.Add(statement);
                _tsRuntimeModuleSyntaxUsed = true;
            }
            return true;
        }

        if (Type != TokenType.Export || TsExportStartsUsingDeclaration())
            return false;

        if (TsTryParseEnumStatements(out var enumStatements, preserveConstEnum: true))
        {
            TsSplitGeneratedTopLevelUsingScopeExportStatements(enumStatements, moduleStatements, ref tryBody,
                ref declarations);
            return true;
        }

        if (TsTryParseNamespaceStatements(out var namespaceStatements))
        {
            TsSplitGeneratedTopLevelUsingScopeExportStatements(namespaceStatements, moduleStatements, ref tryBody,
                ref declarations);
            return true;
        }

        var exportStatement = ParseStatement(true, true);
        _tsRuntimeModuleSyntaxUsed |= exportStatement is AstExport;

        if (exportStatement is not AstExport { ExportedDefinition: AstDefinitions definitions } export)
        {
            if (TsTrySplitTopLevelUsingScopeExport(exportStatement, moduleStatements, declarationsAfterUsingVar,
                    ref tryBody, ref declarations))
                return true;

            if (exportStatement is AstExport exportSpecifiers)
                TsHoistTopLevelUsingScopeExportedLocals(exportSpecifiers, ref tryBody, ref declarations);
            moduleStatements.Add(exportStatement);
            return true;
        }

        var exportedDeclarations = new StructRefList<AstVarDef>();
        for (var i = 0u; i < definitions.Definitions.Count; i++)
        {
            var definition = definitions.Definitions[i];
            var exportedName = TsExportedUsingScopeDeclarationName(definition.Name);
            if (exportedName == null)
            {
                moduleStatements.Add(exportStatement);
                return true;
            }

            exportedDeclarations.Add(new AstVarDef(definition.Source, definition.Start, definition.End,
                new AstSymbolLet(new AstSymbolRef(SourceFile, definition.Name.Start, definition.Name.End,
                    exportedName))));

            if (definition.Value != null)
            {
                tryBody.Add(new AstSimpleStatement(definition.Source, definition.Start, definition.End,
                    new AstAssign(definition.Source, definition.Start, definition.End,
                        new AstSymbolRef(SourceFile, definition.Name.Start, definition.Name.End, exportedName),
                        definition.Value, Operator.Assignment)));
            }
        }

        var exportedLet = new AstLet(definitions.Source, definitions.Start, definitions.End, ref exportedDeclarations);
        var specifiers = new StructList<AstNameMapping>();
        declarationsAfterUsingVar.Add(new AstExport(export.Source, export.Start, export.End, null, exportedLet,
            ref specifiers));
        return true;
    }

    void TsSplitGeneratedTopLevelUsingScopeExportStatements(List<AstStatement> generatedStatements,
        List<AstStatement> moduleStatements, ref StructRefList<AstNode> tryBody,
        ref StructRefList<AstVarDef> usingDeclarations)
    {
        if (generatedStatements.Count == 0)
            return;

        var firstRuntimeStatement = 0;
        if (generatedStatements[0] is AstTypeScriptEnum { IsExport: true } enumNode)
        {
            TsAddTopLevelUsingVarDeclaration(ref usingDeclarations, enumNode, enumNode.Name);
            moduleStatements.Add(TsBuildExportSpecifierStatement(enumNode, enumNode.Name, enumNode.Name));
            generatedStatements[0] = new AstTypeScriptEnum(enumNode.Source, enumNode.Start, enumNode.End,
                enumNode.Name, isExport: false, enumNode.IsConst, enumNode.IsLocal, enumNode.PreserveConstEnum,
                enumNode.Members, emitDeclaration: false);
        }
        else if (generatedStatements[0] is AstExport { ExportedDefinition: AstDefinitions definitions } export)
        {
            foreach (var definition in definitions.Definitions.AsReadOnlySpan())
            {
                if (definition.Name is not AstSymbol symbol)
                    continue;

                TsAddTopLevelUsingVarDeclaration(ref usingDeclarations, definition, symbol.Name);
                moduleStatements.Add(TsBuildExportSpecifierStatement(export, symbol.Name, symbol.Name));
            }

            firstRuntimeStatement = 1;
        }

        for (var i = firstRuntimeStatement; i < generatedStatements.Count; i++)
            tryBody.Add(generatedStatements[i]);
    }

    void TsHoistTopLevelUsingScopeExportedLocals(AstExport export, ref StructRefList<AstNode> tryBody,
        ref StructRefList<AstVarDef> usingDeclarations)
    {
        if (export.ModuleName != null || export.ExportedNames.Count == 0)
            return;

        var exportedLocals = new HashSet<string>(StringComparer.Ordinal);
        foreach (var specifier in export.ExportedNames.AsReadOnlySpan())
            exportedLocals.Add(specifier.Name.Name);
        if (exportedLocals.Count == 0)
            return;

        var rewrittenBody = new StructRefList<AstNode>();
        foreach (var statement in tryBody.AsReadOnlySpan())
        {
            if (statement is not AstDefinitions definitions)
            {
                rewrittenBody.Add(statement);
                continue;
            }

            var remainingDefinitions = new StructRefList<AstVarDef>();
            var assignmentStatements = new List<AstNode>();
            foreach (var definition in definitions.Definitions.AsReadOnlySpan())
            {
                if (definition.Name is not AstSymbol symbol || !exportedLocals.Contains(symbol.Name))
                {
                    remainingDefinitions.Add(definition);
                    continue;
                }

                TsAddTopLevelUsingVarDeclaration(ref usingDeclarations, definition, symbol.Name);
                if (definition.Value != null)
                {
                    assignmentStatements.Add(new AstSimpleStatement(definition.Source, definition.Start, definition.End,
                        new AstAssign(definition.Source, definition.Start, definition.End,
                            new AstSymbolRef(SourceFile, definition.Name.Start, definition.Name.End, symbol.Name),
                            definition.Value, Operator.Assignment)));
                }
            }

            if (remainingDefinitions.Count != 0)
                rewrittenBody.Add(TsCloneDefinitionsWith(definitions, ref remainingDefinitions));
            foreach (var assignment in assignmentStatements)
                rewrittenBody.Add(assignment);
        }

        tryBody.TransferFrom(ref rewrittenBody);
    }

    static AstDefinitions TsCloneDefinitionsWith(AstDefinitions source, ref StructRefList<AstVarDef> definitions)
    {
        return source switch
        {
            AstConst => new AstConst(source.Source, source.Start, source.End, ref definitions),
            AstLet => new AstLet(source.Source, source.Start, source.End, ref definitions),
            _ => new AstVar(source.Source, source.Start, source.End, ref definitions)
        };
    }

    bool TsTrySplitTopLevelUsingScopeExport(AstStatement exportStatement, List<AstStatement> moduleStatements,
        List<AstStatement> declarationsAfterUsingVar, ref StructRefList<AstNode> tryBody,
        ref StructRefList<AstVarDef> usingDeclarations)
    {
        if (exportStatement is not AstExport export)
            return false;

        if (!export.IsDefault && export.ExportedDefinition is AstDefClass namedClass)
        {
            var className = TsClassDeclarationName(namedClass);
            if (className == null)
                return false;

            TsAddTopLevelUsingVarDeclaration(ref usingDeclarations, namedClass, className);
            moduleStatements.Add(TsBuildExportSpecifierStatement(export, className, className));
            tryBody.Add(new AstSimpleStatement(namedClass.Source, namedClass.Start, namedClass.End,
                new AstAssign(namedClass.Source, namedClass.Start, namedClass.End,
                    new AstSymbolRef(SourceFile, namedClass.Name!.Start, namedClass.Name.End, className),
                    TsClassDeclarationToExpression(namedClass), Operator.Assignment)));
            return true;
        }

        if (!export.IsDefault)
            return false;

        if (export.ExportedValue != null)
        {
            const string defaultName = "_default";
            TsAddTopLevelUsingVarDeclaration(ref usingDeclarations, export.ExportedValue, defaultName);
            moduleStatements.Add(TsBuildExportSpecifierStatement(export, defaultName, "default"));
            tryBody.Add(new AstSimpleStatement(export.ExportedValue.Source, export.ExportedValue.Start,
                export.ExportedValue.End,
                new AstAssign(export.ExportedValue.Source, export.ExportedValue.Start, export.ExportedValue.End,
                    new AstSymbolRef(SourceFile, export.ExportedValue.Start, export.ExportedValue.End, defaultName),
                    export.ExportedValue, Operator.Assignment)));
            return true;
        }

        if (export.ExportedDefinition is AstDefClass defaultClass)
        {
            const string defaultName = "_default";
            var className = TsTopLevelUsingDefaultClassAssignmentName(defaultClass);
            if (className != null)
                TsAddTopLevelUsingVarDeclaration(ref usingDeclarations, defaultClass, className);
            TsAddTopLevelUsingVarDeclaration(ref usingDeclarations, defaultClass, defaultName);
            moduleStatements.Add(TsBuildExportSpecifierStatement(export, defaultName, "default"));

            AstNode classExpression = TsClassDeclarationToDefaultExportExpression(defaultClass, className == null);
            AstNode assignedValue = classExpression;
            if (className != null)
            {
                assignedValue = new AstAssign(defaultClass.Source, defaultClass.Start, defaultClass.End,
                    new AstSymbolRef(SourceFile, defaultClass.Name!.Start, defaultClass.Name.End, className),
                    classExpression, Operator.Assignment);
            }

            tryBody.Add(new AstSimpleStatement(defaultClass.Source, defaultClass.Start, defaultClass.End,
                new AstAssign(defaultClass.Source, defaultClass.Start, defaultClass.End,
                    new AstSymbolRef(SourceFile, defaultClass.Start, defaultClass.End, defaultName),
                    assignedValue, Operator.Assignment)));
            return true;
        }

        return false;
    }

    AstExport TsBuildExportSpecifierStatement(AstExport export, string localName, string exportedName)
    {
        return TsBuildExportSpecifierStatement((AstNode)export, localName, exportedName);
    }

    AstExport TsBuildExportSpecifierStatement(AstNode anchor, string localName, string exportedName)
    {
        var specifiers = new StructList<AstNameMapping>();
        specifiers.Add(new AstNameMapping(anchor.Source, anchor.Start, anchor.End,
            new AstSymbolExportForeign(anchor.Source, anchor.Start, anchor.End, exportedName),
            new AstSymbolExport(anchor.Source, anchor.Start, anchor.End, localName)));
        return new AstExport(anchor.Source, anchor.Start, anchor.End, null, null, ref specifiers);
    }

    void TsAddTopLevelUsingVarDeclaration(ref StructRefList<AstVarDef> declarations, AstNode anchor, string name)
    {
        declarations.Add(new AstVarDef(anchor.Source, anchor.Start, anchor.End,
            new AstSymbolVar(anchor.Source, anchor.Start, anchor.End, name, null)));
    }

    static string? TsClassDeclarationName(AstDefClass classDeclaration)
    {
        return classDeclaration.Name?.Name;
    }

    string? TsTopLevelUsingDefaultClassAssignmentName(AstDefClass classDeclaration)
    {
        if (!TsIsSyntheticDefaultClassName(classDeclaration))
            return TsClassDeclarationName(classDeclaration);
        return TsAnonymousDefaultClassNeedsSyntheticName(classDeclaration) ? TsClassDeclarationName(classDeclaration) : null;
    }

    static bool TsIsSyntheticDefaultClassName(AstDefClass classDeclaration)
    {
        return classDeclaration.Name is { Name: "default_1" } name &&
               name.Start.Index == classDeclaration.Start.Index &&
               name.End.Index == classDeclaration.Start.Index;
    }

    static bool TsAnonymousDefaultClassNeedsSyntheticName(AstDefClass classDeclaration)
    {
        foreach (var property in classDeclaration.Properties.AsReadOnlySpan())
        {
            if (property is AstClassField { Static: true })
                return true;
        }

        return false;
    }

    AstClassExpression TsClassDeclarationToDefaultExportExpression(AstDefClass classDeclaration, bool setDefaultName)
    {
        var classExpression = TsClassDeclarationToExpression(classDeclaration, setDefaultName ? null : classDeclaration.Name,
            useOriginalName: !setDefaultName);
        if (!setDefaultName)
            return classExpression;

        _tsSetFunctionNameHelperUsed = true;
        classExpression.Properties.Insert(0, TsBuildSetFunctionNameStaticBlock(classDeclaration.Start, "default"));
        return classExpression;
    }

    AstStaticBlock TsBuildSetFunctionNameStaticBlock(Position position, string name)
    {
        var args = new StructList<AstNode>();
        args.Add(new AstThis(SourceFile, position, position));
        args.Add(new AstString(SourceFile, position, position, name));
        var call = new AstCall(SourceFile, position, position,
            new AstSymbolRef(SourceFile, position, position, "__setFunctionName"), ref args);
        var body = new StructRefList<AstNode>();
        body.Add(new AstSimpleStatement(SourceFile, position, position, call));
        return new AstStaticBlock(SourceFile, position, position, ref body);
    }

    static AstClassExpression TsClassDeclarationToExpression(AstDefClass classDeclaration,
        AstSymbolDeclaration? name = null, bool useOriginalName = true)
    {
        var properties = new StructList<AstNode>();
        properties.AddRange(classDeclaration.Properties.AsReadOnlySpan());
        return new AstClassExpression(classDeclaration.Source, classDeclaration.Start, classDeclaration.End,
            name ?? (useOriginalName ? classDeclaration.Name : null), classDeclaration.Extends, ref properties);
    }

    bool TsCurrentImportTokenStartsExpression()
    {
        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index]))
            index++;
        if (index >= _input.Length)
            return false;
        return _input[index] is '(' or '.' or '<';
    }

    static string? TsExportedUsingScopeDeclarationName(AstNode declarationName)
    {
        return declarationName is AstSymbol symbol ? symbol.Name : null;
    }

    void TsReserveTopLevelUsingTemps()
    {
        if (TsFindTopLevelUsingScope(out var hasAwaitUsing))
        {
            _tsReserveTopLevelUsingTemp = true;
            _tsReserveTopLevelAwaitUsingResultTemp = hasAwaitUsing;
        }
    }

    string TsAllocateUsingEnvName(bool topLevel)
    {
        return "env_" + TsAllocateUsingIndex(topLevel, _tsReserveTopLevelUsingTemp,
            ref _tsTopLevelUsingEnvTempConsumed, ref _tsUsingEnvIndex).ToString(CultureInfo.InvariantCulture);
    }

    string TsAllocateUsingErrorName(bool topLevel)
    {
        return "e_" + TsAllocateUsingIndex(topLevel, _tsReserveTopLevelUsingTemp,
            ref _tsTopLevelUsingErrorTempConsumed, ref _tsUsingErrorIndex).ToString(CultureInfo.InvariantCulture);
    }

    string TsAllocateUsingResultName(bool topLevel)
    {
        return "result_" + TsAllocateUsingIndex(topLevel, _tsReserveTopLevelAwaitUsingResultTemp,
            ref _tsTopLevelAwaitUsingResultTempConsumed, ref _tsUsingResultIndex).ToString(CultureInfo.InvariantCulture);
    }

    static int TsAllocateUsingIndex(bool topLevel, bool reserved, ref bool consumed, ref int index)
    {
        if (reserved && !consumed)
        {
            if (topLevel)
            {
                consumed = true;
                index = Math.Max(index, 1);
                return 1;
            }
            index = Math.Max(index, 1);
        }

        return ++index;
    }

    bool TsFindTopLevelUsingScope(out bool hasAwaitUsing)
    {
        hasAwaitUsing = false;
        var depth = 0;
        var firstUsingFound = false;
        var atStatementStart = true;
        var afterAwait = false;

        for (var i = 0; i < _input.Length;)
        {
            var ch = _input[i];
            if (char.IsWhiteSpace(ch))
            {
                if (depth == 0 && IsNewLine(ch))
                {
                    afterAwait = false;
                    if (!atStatementStart)
                        atStatementStart = true;
                }
                i++;
                continue;
            }

            if (ch == '/' && i + 1 < _input.Length)
            {
                if (_input[i + 1] == '/')
                {
                    i += 2;
                    while (i < _input.Length && _input[i] != '\n' && _input[i] != '\r')
                        i++;
                    continue;
                }
                if (_input[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < _input.Length && !(_input[i] == '*' && _input[i + 1] == '/'))
                        i++;
                    i = Math.Min(i + 2, _input.Length);
                    continue;
                }
            }

            if (ch is '"' or '\'' or '`')
            {
                TsSkipQuotedText(ref i, ch);
                if (depth == 0)
                {
                    atStatementStart = false;
                    afterAwait = false;
                }
                continue;
            }

            if (ch is '{' or '(' or '[')
            {
                depth++;
                atStatementStart = false;
                afterAwait = false;
                i++;
                continue;
            }

            if (ch is '}' or ')' or ']')
            {
                if (depth > 0)
                    depth--;
                if (depth == 0 && ch == '}')
                    atStatementStart = true;
                afterAwait = false;
                i++;
                continue;
            }

            if (IsIdentifierStart(ch))
            {
                var wordStart = i++;
                while (i < _input.Length && IsIdentifierChar(_input[i]))
                    i++;
                var word = _input.Substring(wordStart, i - wordStart);
                if (depth == 0 && atStatementStart && word == "export")
                {
                    continue;
                }
                if (depth == 0 && atStatementStart && word == "await")
                {
                    afterAwait = true;
                    continue;
                }
                if (depth == 0 && atStatementStart && word == "using")
                {
                    var lookahead = TsSkipWhitespaceAndComments(i);
                    if (lookahead < _input.Length &&
                        (IsIdentifierStart(_input[lookahead], true) ||
                         _input[lookahead] == CharCode.LeftCurlyBracket ||
                         _input[lookahead] == CharCode.LeftSquareBracket))
                    {
                        firstUsingFound = true;
                        if (afterAwait)
                            hasAwaitUsing = true;
                    }
                }
                if (depth == 0)
                {
                    atStatementStart = false;
                    afterAwait = false;
                }
                continue;
            }

            if (depth == 0)
            {
                atStatementStart = ch == ';';
                afterAwait = false;
            }
            i++;
        }

        return firstUsingFound;
    }

    void TsSkipQuotedText(ref int index, char quote)
    {
        index++;
        while (index < _input.Length)
        {
            var ch = _input[index++];
            if (ch == '\\')
            {
                index++;
                continue;
            }
            if (ch == quote)
                return;
        }
    }

    void TsParseUsingDeclarationIntoScope(Position startLocation, string envName, bool topLevel, bool isAwait,
        ref StructRefList<AstVarDef> declarations, ref StructRefList<AstNode> tryBody)
    {
        var scopedDefinitions = new StructRefList<AstVarDef>();
        var entries = new List<(AstNode Id, Position DeclStart, Position BindingEnd, Position InitStart,
            Position InitEnd, AstNode ConvertedId, AstNode Init)>();
        var hasDestructuring = false;
        for (;;)
        {
            var declStart = Start;
            var id = ParseBindingAtom();
            var bindingEnd = _lastTokEnd;
            TsTrySkipOptionalOrDefiniteBindingMarker();
            TsTrySkipTypeAnnotation();
            Expect(TokenType.Eq);
            var initStart = Start;
            var init = ParseMaybeAssign(Start);
            var initEnd = _lastTokEnd;
            id = ToAssignable(id, true)!;
            CheckLVal(id, true, topLevel ? VariableKind.Var : VariableKind.Const);
            var convertedId = ToRightDeclarationSymbolKind(id, topLevel ? VariableKind.Var : VariableKind.Const);
            hasDestructuring |= id is AstDestructuring;
            entries.Add((id, declStart, bindingEnd, initStart, initEnd, convertedId, init));
            if (!Eat(TokenType.Comma))
                break;
        }
        Semicolon();

        if (hasDestructuring)
        {
            if (entries.Exists(entry => entry.Id is not AstDestructuring))
                _tsAddDisposableResourceHelperUsed = true;
            if (topLevel)
            {
                foreach (var entry in entries)
                {
                    if (entry.Id is AstDestructuring)
                        TsAddTopLevelUsingDestructuringDeclarations(entry.Id, ref declarations);
                    else
                        declarations.Add(new AstVarDef(SourceFile, entry.DeclStart, entry.InitEnd,
                            entry.ConvertedId, null));
                }

                tryBody.Add(new AstRawStatement(SourceFile, startLocation, _lastTokEnd,
                    TsBuildTopLevelUsingDestructuringAssignment(entries) + ";"));
            }
            else
            {
                tryBody.Add(new AstRawStatement(SourceFile, startLocation, _lastTokEnd,
                    (isAwait ? "await " : "") + "using " + TsBuildRawUsingDeclarationList(entries) + ";"));
            }
            return;
        }

        foreach (var entry in entries)
        {
            if (topLevel)
            {
                declarations.Add(new AstVarDef(SourceFile, entry.DeclStart, entry.InitEnd,
                    entry.ConvertedId, null));
                var value = TsBuildAddDisposableResourceCall(startLocation, envName, entry.Init, isAwait);
                var assignment = new AstAssign(SourceFile, entry.DeclStart, entry.InitEnd,
                    TsUsingAssignmentTarget(entry.Id), value, Operator.Assignment);
                tryBody.Add(new AstSimpleStatement(SourceFile, assignment.Start, assignment.End, assignment));
            }
            else
            {
                var value = TsBuildAddDisposableResourceCall(startLocation, envName, entry.Init, isAwait);
                scopedDefinitions.Add(new AstVarDef(SourceFile, entry.DeclStart, entry.InitEnd,
                    entry.ConvertedId, value));
            }
        }

        if (!topLevel)
            tryBody.Add(new AstConst(SourceFile, startLocation, _lastTokEnd, ref scopedDefinitions));
    }

    string TsBuildRawUsingDeclarationList(List<(AstNode Id, Position DeclStart, Position BindingEnd,
        Position InitStart, Position InitEnd, AstNode ConvertedId, AstNode Init)> entries)
    {
        var parts = new List<string>(entries.Count);
        foreach (var entry in entries)
        {
            parts.Add(_input.Substring(entry.DeclStart.Index, entry.BindingEnd.Index - entry.DeclStart.Index).Trim() +
                      " = " +
                      _input.Substring(entry.InitStart.Index, entry.InitEnd.Index - entry.InitStart.Index).Trim());
        }

        return string.Join(", ", parts);
    }

    string TsBuildTopLevelUsingDestructuringAssignment(List<(AstNode Id, Position DeclStart, Position BindingEnd,
        Position InitStart, Position InitEnd, AstNode ConvertedId, AstNode Init)> entries)
    {
        var assignment = TsBuildRawUsingDeclarationList(entries);
        if (entries.Count > 0 && entries[0].Id is AstDestructuring { IsArray: false })
            return "(" + assignment + ")";
        return assignment;
    }

    void TsAddTopLevelUsingDestructuringDeclarations(AstNode id, ref StructRefList<AstVarDef> declarations)
    {
        var names = new List<AstSymbolDeclaration>();
        TsCollectBindingSymbols(id, names);
        foreach (var name in names)
        {
            declarations.Add(new AstVarDef(SourceFile, name.Start, name.End,
                new AstSymbolVar(SourceFile, name.Start, name.End, name.Name, null)));
        }
    }

    static void TsCollectBindingSymbols(AstNode node, List<AstSymbolDeclaration> names)
    {
        switch (node)
        {
            case AstSymbolDeclaration symbol:
                names.Add(symbol);
                break;
            case AstExpansion expansion:
                TsCollectBindingSymbols(expansion.Expression, names);
                break;
            case AstDefaultAssign defaultAssign:
                TsCollectBindingSymbols(defaultAssign.Left, names);
                break;
            case AstDestructuring destructuring:
                foreach (var name in destructuring.Names.AsReadOnlySpan())
                    TsCollectBindingSymbols(name, names);
                break;
            case AstObjectKeyVal keyValue:
                TsCollectBindingSymbols(keyValue.Value, names);
                break;
        }
    }

    AstNode TsUsingAssignmentTarget(AstNode id)
    {
        return id switch
        {
            AstSymbol symbol => new AstSymbolRef(SourceFile, symbol.Start, symbol.End, symbol.Name),
            _ => id
        };
    }

    AstConst TsBuildUsingEnvDeclaration(Position position, string envName)
    {
        var definitions = new StructRefList<AstVarDef>();
        definitions.Add(new AstVarDef(SourceFile, position, position,
            new AstSymbolConst(new AstSymbolRef(SourceFile, position, position, envName)),
            TsBuildUsingEnvObject(position)));
        return new AstConst(SourceFile, position, position, ref definitions);
    }

    AstObject TsBuildUsingEnvObject(Position position)
    {
        var properties = new StructList<AstObjectItem>();
        var emptyElements = new StructList<AstNode>();
        properties.Add(new AstObjectKeyVal(SourceFile, position, position,
            new AstSymbolProperty(SourceFile, position, position, "stack"),
            new AstArray(SourceFile, position, position, ref emptyElements)));
        properties.Add(new AstObjectKeyVal(SourceFile, position, position,
            new AstSymbolProperty(SourceFile, position, position, "error"),
            new AstUnaryPrefix(SourceFile, position, position, Operator.Void,
                new AstNumber(SourceFile, position, position, 0, "0"))));
        properties.Add(new AstObjectKeyVal(SourceFile, position, position,
            new AstSymbolProperty(SourceFile, position, position, "hasError"),
            new AstFalse(SourceFile, position, position)));
        return new AstObject(SourceFile, position, position, ref properties);
    }

    AstCall TsBuildAddDisposableResourceCall(Position position, string envName, AstNode value, bool isAwait)
    {
        _tsAddDisposableResourceHelperUsed = true;
        var args = new StructList<AstNode>();
        args.Add(new AstSymbolRef(SourceFile, position, position, envName));
        args.Add(value);
        args.Add(isAwait ? new AstTrue(SourceFile, position, position) : new AstFalse(SourceFile, position, position));
        return new AstCall(SourceFile, position, position,
            new AstSymbolRef(SourceFile, position, position, "__addDisposableResource"), ref args);
    }

    AstTry TsBuildUsingTry(Position position, string envName, string errorName, bool isAwait,
        ref StructRefList<AstNode> tryBody, bool topLevel = false)
    {
        var catchBody = new StructRefList<AstNode>();
        catchBody.Add(TsBuildAssignmentStatement(position, envName + ".error", errorName));
        catchBody.Add(TsBuildAssignmentStatement(position, envName + ".hasError", new AstTrue(SourceFile, position, position)));
        var catchNode = new AstCatch(SourceFile, position, position,
            new AstSymbolCatch(new AstSymbolRef(SourceFile, position, position, errorName)), ref catchBody);

        var finallyBody = new StructRefList<AstNode>();
        if (isAwait)
        {
            var resultName = TsAllocateUsingResultName(topLevel);
            var resultDefinitions = new StructRefList<AstVarDef>();
            resultDefinitions.Add(new AstVarDef(SourceFile, position, position,
                new AstSymbolConst(new AstSymbolRef(SourceFile, position, position, resultName)),
                TsBuildDisposeResourcesCall(position, envName)));
            finallyBody.Add(new AstConst(SourceFile, position, position, ref resultDefinitions));
            finallyBody.Add(new AstIf(SourceFile, position, position,
                new AstSymbolRef(SourceFile, position, position, resultName),
                new AstSimpleStatement(SourceFile, position, position,
                    new AstAwait(SourceFile, position, position,
                        new AstSymbolRef(SourceFile, position, position, resultName))), null));
        }
        else
        {
            finallyBody.Add(new AstSimpleStatement(SourceFile, position, position,
                TsBuildDisposeResourcesCall(position, envName)));
        }

        var finallyNode = new AstFinally(SourceFile, position, position, ref finallyBody);
        return new AstTry(SourceFile, position, position, ref tryBody, catchNode, finallyNode);
    }

    AstCall TsBuildDisposeResourcesCall(Position position, string envName)
    {
        var args = new StructList<AstNode>();
        args.Add(new AstSymbolRef(SourceFile, position, position, envName));
        return new AstCall(SourceFile, position, position,
            new AstSymbolRef(SourceFile, position, position, "__disposeResources"), ref args);
    }

    AstSimpleStatement TsBuildAssignmentStatement(Position position, string dottedTarget, string sourceName)
    {
        return TsBuildAssignmentStatement(position, dottedTarget,
            new AstSymbolRef(SourceFile, position, position, sourceName));
    }

    AstSimpleStatement TsBuildAssignmentStatement(Position position, string dottedTarget, AstNode value)
    {
        var parts = dottedTarget.Split('.');
        AstNode target = new AstSymbolRef(SourceFile, position, position, parts[0]);
        for (var i = 1; i < parts.Length; i++)
            target = new AstDot(SourceFile, position, position, target, parts[i]);
        return new AstSimpleStatement(SourceFile, position, position,
            new AstAssign(SourceFile, position, position, target, value, Operator.Assignment));
    }

    void TsInsertUsingHelperStatements(ref StructRefList<AstNode> body)
    {
        if (!_tsAddDisposableResourceHelperUsed && !_tsDisposeResourcesHelperUsed && !_tsSetFunctionNameHelperUsed)
            return;

        var helpers = Parser.Parse(TsUsingHelperSource, new Options
        {
            SourceFile = SourceFile,
            SourceType = SourceType.Script,
            EcmaVersion = Options.EcmaVersion
        });
        var insertIndex = TsDirectivePrefixLength(body);
        foreach (var helperStatement in helpers.Body.AsReadOnlySpan())
        {
            if (!_tsAddDisposableResourceHelperUsed && TsDefinitionsDeclareName(helperStatement, "__addDisposableResource"))
                continue;
            if (!_tsSetFunctionNameHelperUsed && TsDefinitionsDeclareName(helperStatement, "__setFunctionName"))
                continue;
            if (!_tsDisposeResourcesHelperUsed && TsDefinitionsDeclareName(helperStatement, "__disposeResources"))
                continue;
            body.Insert(insertIndex++, helperStatement);
        }
    }

    static bool TsDefinitionsDeclareName(AstNode node, string name)
    {
        return node is AstDefinitions definitions &&
               definitions.Definitions.Count == 1 &&
               definitions.Definitions[0].Name is AstSymbol symbol &&
               symbol.Name == name;
    }

    const string TsUsingHelperSource = """
        var __addDisposableResource = (this && this.__addDisposableResource) || function (env, value, async) {
            if (value !== null && value !== void 0) {
                if (typeof value !== "object" && typeof value !== "function") throw new TypeError("Object expected.");
                var dispose, inner;
                if (async) {
                    if (!Symbol.asyncDispose) throw new TypeError("Symbol.asyncDispose is not defined.");
                    dispose = value[Symbol.asyncDispose];
                }
                if (dispose === void 0) {
                    if (!Symbol.dispose) throw new TypeError("Symbol.dispose is not defined.");
                    dispose = value[Symbol.dispose];
                    if (async) inner = dispose;
                }
                if (typeof dispose !== "function") throw new TypeError("Object not disposable.");
                if (inner) dispose = function() { try { inner.call(this); } catch (e) { return Promise.reject(e); } };
                env.stack.push({ value: value, dispose: dispose, async: async });
            }
            else if (async) {
                env.stack.push({ async: true });
            }
            return value;
        };
        var __setFunctionName = (this && this.__setFunctionName) || function (f, name, prefix) {
            if (typeof name === "symbol") name = name.description ? "[".concat(name.description, "]") : "";
            return Object.defineProperty(f, "name", { configurable: true, value: prefix ? "".concat(prefix, " ", name) : name });
        };
        var __disposeResources = (this && this.__disposeResources) || (function (SuppressedError) {
            return function (env) {
                function fail(e) {
                    env.error = env.hasError ? new SuppressedError(e, env.error, "An error was suppressed during disposal.") : e;
                    env.hasError = true;
                }
                var r, s = 0;
                function next() {
                    while (r = env.stack.pop()) {
                        try {
                            if (!r.async && s === 1) return s = 0, env.stack.push(r), Promise.resolve().then(next);
                            if (r.dispose) {
                                var result = r.dispose.call(r.value);
                                if (r.async) return s |= 2, Promise.resolve(result).then(next, function(e) { fail(e); return next(); });
                            }
                            else s |= 1;
                        }
                        catch (e) {
                            fail(e);
                        }
                    }
                    if (s === 1) return env.hasError ? Promise.reject(env.error) : Promise.resolve();
                    if (env.hasError) throw env.error;
                }
                return next();
            };
        })(typeof SuppressedError === "function" ? SuppressedError : function (error, suppressed, message) {
            var e = new Error(message);
            return e.name = "SuppressedError", e.error = error, e.suppressed = suppressed, e;
        });
        """;

    bool TsTryParseNamespaceStatements(out List<AstStatement> statements, bool local = false,
        bool forceExport = false)
    {
        statements = null!;
        var namespaceStart = Start;
        var isExport = forceExport;
        if (Type == TokenType.Export)
        {
            var index = End.Index;
            while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
            if (!TsTextStartsKeyword(index, "namespace") && !TsTextStartsKeyword(index, "module"))
                return false;
            isExport = true;
            Next();
        }

        if (!TsIsNamespaceStatementStart())
            return false;

        Next();
        var namespaceNames = new List<string>();
        var name = Type == TokenType.Name ? Value?.ToString() : null;
        if (name == null)
            Raise(Start, "Unexpected token");
        namespaceNames.Add(name!);
        Next();
        while (Eat(TokenType.Dot))
        {
            name = Type == TokenType.Name ? Value?.ToString() : null;
            if (name == null)
                Raise(Start, "Unexpected token");
            namespaceNames.Add(name!);
            Next();
        }
        Expect(TokenType.BraceL);
        var bodyStart = _lastTokEnd.Index;
        var bodyEnd = TsFindMatchingSkippingLiterals(_lastTokStart.Index, '{', '}');
        if (bodyEnd < 0)
            Raise(Start, "Unexpected token");
        TsMoveToIndexAndReadToken(bodyEnd);
        Expect(TokenType.BraceR);
        Eat(TokenType.Semi);

        var body = _input.Substring(bodyStart, bodyEnd - bodyStart);
        statements = TsLowerNamespace(namespaceNames, isExport, body, namespaceStart, _lastTokEnd, local);
        if (isExport && statements.Count == 0)
            _tsErasedTypeOnlyModuleSyntaxUsed = true;
        if (local && statements.Count != 0 && statements[0] is AstVar varStatement)
        {
            var definitions = new StructRefList<AstVarDef>();
            definitions.AddRange(varStatement.Definitions.AsReadOnlySpan());
            statements[0] = new AstLet(varStatement.Source, varStatement.Start, varStatement.End, ref definitions);
        }
        return true;
    }

    List<AstStatement> TsLowerNamespace(List<string> namespaceNames, bool isExport, string body, Position start,
        Position end, bool local)
    {
        if (namespaceNames.Count == 1)
            return TsLowerNamespace(namespaceNames[0], isExport, body, start, end);

        var iifeBody = new StructRefList<AstNode>();
        TsLowerNestedNamespace(namespaceNames, 1, body, namespaceNames[0], namespaceNames[0], start, end, local,
            ref iifeBody);
        if (iifeBody.Count == 0 && !TsNamespaceBodyNeedsRuntimeShell(body))
        {
            TsRememberErasedTypeOnlyNamespace(namespaceNames[0]);
            return new List<AstStatement>();
        }

        TsForgetErasedTypeOnlyNamespace(namespaceNames[0]);
        var result = new List<AstStatement> { TsBuildNamespaceVariable(namespaceNames[0], isExport, start, end) };
        result.Add(TsBuildNamespaceIife(namespaceNames[0], ref iifeBody, start, end));
        return result;
    }

    List<AstStatement> TsLowerNamespace(string namespaceName, bool isExport, string body, Position start, Position end)
    {
        var iifeBody = TsBuildNamespaceIifeBody(namespaceName, body, start, end);
        if (iifeBody.Count == 0 && !TsNamespaceBodyNeedsRuntimeShell(body))
        {
            TsRememberErasedTypeOnlyNamespace(namespaceName);
            return new List<AstStatement>();
        }

        TsForgetErasedTypeOnlyNamespace(namespaceName);
        var result = new List<AstStatement> { TsBuildNamespaceVariable(namespaceName, isExport, start, end) };
        result.Add(TsBuildNamespaceIife(namespaceName, ref iifeBody, start, end));
        return result;
    }

    void TsRememberErasedTypeOnlyNamespace(string namespaceName)
    {
        _tsErasedTypeOnlyNamespaces ??= new HashSet<string>(StringComparer.Ordinal);
        _tsErasedTypeOnlyNamespaces.Add(namespaceName);
    }

    void TsForgetErasedTypeOnlyNamespace(string namespaceName)
    {
        _tsErasedTypeOnlyNamespaces?.Remove(namespaceName);
    }

    static bool TsNamespaceBodyNeedsRuntimeShell(string body)
    {
        var searchable = TsEraseCommentsAndStrings(body);
        return Regex.IsMatch(searchable, @"\bexport\s+import\b") ||
               Regex.IsMatch(searchable, @"\bexport\s+(?:type\s+)?\*") ||
               Regex.IsMatch(searchable, @"\bexport\s+(?:var|let|const)\b") ||
               Regex.IsMatch(searchable, @"\bfunction\s+[A-Za-z_$][\w$]*\s*(?:<[^>{};]*>\s*)?\([^{};]*\)\s*;") ||
               TsNamespaceBodyHasValueExportSpecifier(searchable) ||
               Regex.IsMatch(searchable, @"\b(?:namespace|module)\s+[A-Za-z_$][\w$]*(?:\s*\.\s*[A-Za-z_$][\w$]*)*\s*\{[\s\S]*?\bexport\s+import\b");
    }

    static string TsEraseCommentsAndStrings(string text)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length;)
        {
            if (chars[i] == '/' && i + 1 < chars.Length)
            {
                if (chars[i + 1] == '/')
                {
                    chars[i++] = ' ';
                    chars[i++] = ' ';
                    while (i < chars.Length && chars[i] is not '\n' and not '\r')
                        chars[i++] = ' ';
                    continue;
                }
                if (chars[i + 1] == '*')
                {
                    chars[i++] = ' ';
                    chars[i++] = ' ';
                    while (i + 1 < chars.Length && !(chars[i] == '*' && chars[i + 1] == '/'))
                        chars[i++] = ' ';
                    if (i < chars.Length) chars[i++] = ' ';
                    if (i < chars.Length) chars[i++] = ' ';
                    continue;
                }
            }

            if (chars[i] is '"' or '\'' or '`')
            {
                var quote = chars[i];
                chars[i++] = ' ';
                while (i < chars.Length)
                {
                    if (chars[i] == '\\')
                    {
                        chars[i++] = ' ';
                        if (i < chars.Length) chars[i++] = ' ';
                        continue;
                    }
                    if (chars[i] == quote)
                    {
                        chars[i++] = ' ';
                        break;
                    }
                    if (chars[i] is not '\n' and not '\r')
                        chars[i] = ' ';
                    i++;
                }
                continue;
            }

            i++;
        }

        return new string(chars);
    }

    static bool TsNamespaceBodyHasValueExportSpecifier(string body)
    {
        var typeOnlyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match typeMatch in Regex.Matches(body, @"\b(?:type|interface)\s+(?<name>[A-Za-z_$][\w$]*)\b"))
            typeOnlyNames.Add(typeMatch.Groups["name"].Value);

        foreach (Match match in Regex.Matches(body, @"\bexport\s+(?<type>type\s+)?\{(?<spec>[^}]*)\}"))
        {
            if (match.Groups["type"].Success)
                continue;
            var specifiers = match.Groups["spec"].Value.Split(',');
            foreach (var specifier in specifiers)
            {
                var trimmed = specifier.TrimStart();
                if (trimmed.Length == 0)
                    continue;
                if (trimmed.StartsWith("type ", StringComparison.Ordinal) &&
                    !trimmed.StartsWith("type as ", StringComparison.Ordinal))
                    continue;
                var exportedName = TsExportSpecifierLocalName(trimmed);
                if (exportedName != null && typeOnlyNames.Contains(exportedName))
                    continue;
                return true;
            }
        }

        return false;
    }

    static string? TsExportSpecifierLocalName(string specifier)
    {
        var index = 0;
        while (index < specifier.Length && char.IsWhiteSpace(specifier[index]))
            index++;
        if (index >= specifier.Length || !IsIdentifierStart(specifier[index], true))
            return null;
        var start = index++;
        while (index < specifier.Length && IsIdentifierChar(specifier[index], true))
            index++;
        return specifier.Substring(start, index - start);
    }

    void TsAddNamespaceStatements(ref StructRefList<AstNode> targetBody, List<AstStatement> namespaceStatements)
    {
        var startIndex = 0;
        if (namespaceStatements.Count > 1 &&
            TsNamespaceVariableName(namespaceStatements[0]) is { } namespaceName &&
            TsBodyHasRuntimeDeclaration(targetBody, namespaceName))
        {
            startIndex = 1;
        }

        for (var i = startIndex; i < namespaceStatements.Count; i++)
            targetBody.Add(namespaceStatements[i]);
    }

    void TsAddEnumStatements(ref StructRefList<AstNode> targetBody, List<AstStatement> enumStatements)
    {
        var startIndex = 0;
        if (enumStatements.Count > 1 &&
            TsNamespaceVariableName(enumStatements[0]) is { } enumName &&
            TsBodyHasRuntimeDeclaration(targetBody, enumName))
        {
            startIndex = 1;
        }

        for (var i = startIndex; i < enumStatements.Count; i++)
            targetBody.Add(enumStatements[i]);
    }

    string? TsNamespaceVariableName(AstNode node)
    {
        node = TsUnwrapExportedDefinition(node);
        if (node is not AstDefinitions definitions || definitions.Definitions.Count != 1)
            return null;
        return definitions.Definitions[0].Name is AstSymbol symbol ? symbol.Name : null;
    }

    bool TsBodyHasRuntimeDeclaration(StructRefList<AstNode> body, string name)
    {
        for (var i = 0u; i < body.Count; i++)
        {
            var statement = body[i];
            var node = TsUnwrapExportedDefinition(statement);
            switch (node)
            {
                case AstDefinitions definitions:
                    if (TsDefinitionsDeclareSingleUninitializedName(definitions, name) &&
                        i + 1 < body.Count &&
                        TsLooksLikeEnumIife(body[i + 1], name))
                        return true;
                    break;
                case AstDefun { Name.Name: var functionName } when functionName == name:
                case AstDefClass { Name.Name: var className } when className == name:
                    return true;
            }
        }

        return false;
    }

    static bool TsDefinitionsDeclareSingleUninitializedName(AstDefinitions definitions, string name)
    {
        return definitions.Definitions.Count == 1 &&
               definitions.Definitions[0] is { Value: null, Name: AstSymbol symbol } &&
               symbol.Name == name;
    }

    static AstNode TsUnwrapExportedDefinition(AstNode node)
    {
        return node is AstExport { ExportedDefinition: { } definition } ? definition : node;
    }

    StructRefList<AstNode> TsBuildNamespaceIifeBody(string namespaceName, string body, Position start, Position end,
        string? fullNamespaceName = null)
    {
        fullNamespaceName ??= namespaceName;
        _tsRuntimeEnumConstants ??= new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var namespaceOptions = new Options
        {
            SourceType = SourceType.Module,
            ParseTypeScript = true,
            ParseJSX = Options.ParseJSX,
            SourceFile = SourceFile,
            ParseTypeScriptNamespaceBody = true,
            TypeScriptUsingEnvIndex = _tsUsingEnvIndex,
            TypeScriptUsingErrorIndex = _tsUsingErrorIndex,
            TypeScriptUsingResultIndex = _tsUsingResultIndex,
            ReserveTopLevelUsingTemp = _tsReserveTopLevelUsingTemp && !_tsTopLevelUsingEnvTempConsumed,
            ReserveTopLevelAwaitUsingResultTemp = _tsReserveTopLevelAwaitUsingResultTemp &&
                                                   !_tsTopLevelAwaitUsingResultTempConsumed,
            TypeScriptRuntimeEnumConstants = _tsRuntimeEnumConstants
        };
        var parsedBody = Parser.Parse(body, namespaceOptions);
        if (namespaceOptions.ParsedTypeScriptImportEquals)
            Options.ParsedTypeScriptImportEquals = true;
        TsImportNamespaceRuntimeEnumConstants(namespaceName, fullNamespaceName, parsedBody);
        TsSyncUsingTempIndexesFromNamespaceBody(parsedBody);

        var exportedNames = TsCollectNamespaceVariableExportedNames(parsedBody);
        TsCollectNamespaceAmbientExportedValueNames(body, exportedNames);
        var iifeBody = new StructRefList<AstNode>();
        if (parsedBody.HasUseStrictDirective)
            iifeBody.Add(new AstSimpleStatement(SourceFile, start, start,
                new AstString(SourceFile, start, start, "use strict")));
        var namespaceDestructuringTempIndex = 0;
        var pendingDestructuringTemps = new List<string>();
        var pendingDestructuringStatements = new StructRefList<AstNode>();
        AstNode? pendingDestructuringAnchor = null;

        void FlushPendingDestructuring()
        {
            if (pendingDestructuringStatements.Count == 0 || pendingDestructuringAnchor == null)
                return;

            if (pendingDestructuringTemps.Count != 0)
            {
                var tempDefinitions = new StructRefList<AstVarDef>();
                foreach (var temp in pendingDestructuringTemps)
                {
                    var symbol = new AstSymbolVar(SourceFile, pendingDestructuringAnchor.Start,
                        pendingDestructuringAnchor.End, temp, null);
                    tempDefinitions.Add(new AstVarDef(SourceFile, pendingDestructuringAnchor.Start,
                        pendingDestructuringAnchor.End, symbol));
                }
                iifeBody.Add(new AstVar(SourceFile, pendingDestructuringAnchor.Start, pendingDestructuringAnchor.End,
                    ref tempDefinitions));
            }

            foreach (var statement in pendingDestructuringStatements.AsReadOnlySpan())
                iifeBody.Add(statement);

            pendingDestructuringTemps.Clear();
            pendingDestructuringStatements.Clear();
            pendingDestructuringAnchor = null;
        }

        for (var i = 0u; i < parsedBody.Body.Count; i++)
        {
            var statement = parsedBody.Body[i];
            if (TsTryCollectNamespaceExportedDestructuringStatement(statement, namespaceName, exportedNames,
                    pendingDestructuringTemps, ref pendingDestructuringStatements, ref namespaceDestructuringTempIndex,
                    ref pendingDestructuringAnchor))
                continue;

            FlushPendingDestructuring();

            if (TsTryLowerNamespaceExportedClassWithDecorators(parsedBody.Body, ref i, namespaceName, ref iifeBody))
                continue;
            if (TsTryLowerNamespaceExportedEnum(parsedBody.Body, ref i, namespaceName, ref iifeBody))
                continue;
            TsLowerNamespaceStatement(statement, namespaceName, fullNamespaceName, exportedNames, ref iifeBody,
                ref namespaceDestructuringTempIndex);
        }

        FlushPendingDestructuring();
        TsHoistNamespaceUsingDestructuringTemps(ref iifeBody);

        return iifeBody;
    }

    void TsImportNamespaceRuntimeEnumConstants(string namespaceName, string fullNamespaceName, AstToplevel parsedBody)
    {
        if (_tsRuntimeEnumConstants == null)
            return;

        var localRuntimeNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in parsedBody.Body.AsReadOnlySpan())
        {
            if (TsNamespaceRuntimeEnumLocalName(node) is { } localName)
                localRuntimeNames.Add(localName);
        }

        var prefix = fullNamespaceName + ".";
        foreach (var pair in new List<KeyValuePair<string, Dictionary<string, string>>>(_tsRuntimeEnumConstants))
        {
            if (!pair.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                var dotIndex = pair.Key.IndexOf('.');
                if (dotIndex > 0 &&
                    localRuntimeNames.Contains(pair.Key[..dotIndex]) &&
                    !pair.Key.StartsWith(namespaceName + ".", StringComparison.Ordinal))
                {
                    _tsRuntimeEnumConstants[fullNamespaceName + "." + pair.Key] = pair.Value;
                }
                continue;
            }

            var localName = pair.Key[prefix.Length..];
            if (localName.Length == 0 || localName.Contains('.', StringComparison.Ordinal))
                continue;
            _tsRuntimeEnumConstants[localName] = pair.Value;
        }

        foreach (var node in parsedBody.Body.AsReadOnlySpan())
        {
            if (TsNamespaceRuntimeEnumLocalName(node) is not { } localEnumName ||
                !_tsRuntimeEnumConstants.TryGetValue(localEnumName, out var constants))
                continue;

            _tsRuntimeEnumConstants[fullNamespaceName + "." + localEnumName] = constants;
        }
    }

    static string? TsNamespaceRuntimeEnumLocalName(AstNode node)
    {
        if (node is AstExport { ExportedDefinition: AstDefinitions definitions })
            return TsRuntimeEnumVariableName(definitions);
        return TsRuntimeEnumVariableName(node);
    }

    static string? TsRuntimeEnumVariableName(AstNode node)
    {
        return node is AstVar { Definitions.Count: 1 } varStatement &&
               varStatement.Definitions[0].Name is AstSymbol symbol
            ? symbol.Name
            : null;
    }

    void TsSyncUsingTempIndexesFromNamespaceBody(AstToplevel body)
    {
        var scanner = new TypeScriptUsingTempIndexScanner();
        scanner.Walk(body);
        _tsUsingEnvIndex = Math.Max(_tsUsingEnvIndex, scanner.MaxEnvIndex);
        _tsUsingErrorIndex = Math.Max(_tsUsingErrorIndex, scanner.MaxErrorIndex);
        _tsUsingResultIndex = Math.Max(_tsUsingResultIndex, scanner.MaxResultIndex);
        _tsAddDisposableResourceHelperUsed |= scanner.UsesAddDisposableResourceHelper;
        _tsDisposeResourcesHelperUsed |= scanner.UsesDisposeResourcesHelper;
    }

    sealed class TypeScriptUsingTempIndexScanner : TreeWalker
    {
        public int MaxEnvIndex;
        public int MaxErrorIndex;
        public int MaxResultIndex;
        public bool UsesAddDisposableResourceHelper;
        public bool UsesDisposeResourcesHelper;

        protected override void Visit(AstNode node)
        {
            if (node is not AstSymbol symbol)
                return;

            if (symbol.Name == "__addDisposableResource")
                UsesAddDisposableResourceHelper = true;
            else if (symbol.Name == "__disposeResources")
                UsesDisposeResourcesHelper = true;

            if (TryReadTempIndex(symbol.Name, "env_", out var envIndex))
                MaxEnvIndex = Math.Max(MaxEnvIndex, envIndex);
            else if (TryReadTempIndex(symbol.Name, "e_", out var errorIndex))
                MaxErrorIndex = Math.Max(MaxErrorIndex, errorIndex);
            else if (TryReadTempIndex(symbol.Name, "result_", out var resultIndex))
                MaxResultIndex = Math.Max(MaxResultIndex, resultIndex);
        }

        static bool TryReadTempIndex(string name, string prefix, out int index)
        {
            index = 0;
            if (!name.StartsWith(prefix, StringComparison.Ordinal) ||
                name.Length == prefix.Length)
                return false;

            for (var i = prefix.Length; i < name.Length; i++)
            {
                var c = name[i];
                if (c < '0' || c > '9')
                    return false;
                index = index * 10 + c - '0';
            }

            return index > 0;
        }
    }

    static void TsHoistNamespaceUsingDestructuringTemps(ref StructRefList<AstNode> iifeBody)
    {
        for (var i = 1u; i < iifeBody.Count; i++)
        {
            if (iifeBody[i] is not AstTry tryNode || tryNode.Body.Count == 0 ||
                !TsIsUsingEnvDeclaration(iifeBody[i - 1]))
                continue;

            var tempDeclarations = new List<AstNode>();
            for (var j = 0u; j < tryNode.Body.Count;)
            {
                if (TsIsNamespaceDestructuringTempVar(tryNode.Body[j]))
                {
                    tempDeclarations.Add(tryNode.Body[j]);
                    tryNode.Body.RemoveAt((int)j);
                    continue;
                }

                j++;
            }

            foreach (var tempDeclaration in tempDeclarations)
            {
                iifeBody.Insert((int)i - 1, tempDeclaration);
                i++;
            }
        }
    }

    static bool TsIsUsingEnvDeclaration(AstNode node)
    {
        return node is AstConst { Definitions.Count: 1 } constNode &&
               constNode.Definitions[0].Name is AstSymbol { Name: var name } &&
               name.StartsWith("env_", StringComparison.Ordinal);
    }

    static bool TsIsNamespaceDestructuringTempVar(AstNode node)
    {
        if (node is not AstVar { Definitions.Count: > 0 } varNode)
            return false;

        foreach (var definition in varNode.Definitions.AsReadOnlySpan())
        {
            if (definition.Value != null ||
                definition.Name is not AstSymbol { Name: var name } ||
                !name.StartsWith("_", StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    void TsLowerNestedNamespace(List<string> namespaceNames, int index, string body, string parentName,
        string fullParentName, Position start, Position end, bool local, ref StructRefList<AstNode> targetBody)
    {
        var namespaceName = namespaceNames[index];
        var fullNamespaceName = fullParentName + "." + namespaceName;

        var nestedBody = new StructRefList<AstNode>();
        if (index == namespaceNames.Count - 1)
        {
            nestedBody = TsBuildNamespaceIifeBody(namespaceName, body, start, end, fullNamespaceName);
        }
        else
        {
            TsLowerNestedNamespace(namespaceNames, index + 1, body, namespaceName, fullNamespaceName, start, end,
                local, ref nestedBody);
        }

        if (nestedBody.Count == 0 && !TsNamespaceBodyNeedsRuntimeShell(body))
            return;

        targetBody.Add(TsBuildNamespaceLocalVariable(namespaceName, start, end, local));
        targetBody.Add(TsBuildNestedNamespaceIife(namespaceName, parentName, ref nestedBody, start, end));
    }

    bool TsTryLowerNamespaceExportedClassWithDecorators(StructRefList<AstNode> statements, ref uint index,
        string namespaceName, ref StructRefList<AstNode> iifeBody)
    {
        if (statements[index] is not AstExport { ExportedDefinition: AstDefClass classStatement })
            return false;

        var className = classStatement.Name?.Name;
        if (string.IsNullOrEmpty(className))
            return false;
        if (index + 1 >= statements.Count || !TsIsDecorateStatementForClass(statements[index + 1], className))
            return false;

        iifeBody.Add(classStatement);
        while (index + 1 < statements.Count && TsIsDecorateStatementForClass(statements[index + 1], className))
            iifeBody.Add(statements[++index]);
        iifeBody.Add(TsBuildNamespaceExportAssignment(namespaceName, className,
            new AstSymbolRef(classStatement, className), classStatement));
        return true;
    }

    bool TsIsDecorateStatementForClass(AstNode statement, string className)
    {
        return statement is AstSimpleStatement { Body: AstCall call } &&
               call.Expression is AstSymbolRef { Name: "__decorate" } &&
               call.Args.Count >= 2 &&
               TsDecorateTargetReferencesClass(call.Args[1], className);
    }

    bool TsDecorateTargetReferencesClass(AstNode target, string className)
    {
        return target switch
        {
            AstSymbolRef symbolRef => symbolRef.Name == className,
            AstDot { Expression: AstSymbolRef symbolRef, Property: "prototype" } => symbolRef.Name == className,
            _ => false
        };
    }

    bool TsTryLowerNamespaceExportedEnum(StructRefList<AstNode> statements, ref uint index, string namespaceName,
        ref StructRefList<AstNode> iifeBody)
    {
        if (index + 1 >= statements.Count)
            return false;
        if (statements[index] is not AstExport { ExportedDefinition: AstVar varStatement })
            return false;
        if (varStatement.Definitions.Count != 1)
            return false;
        var definition = varStatement.Definitions[0];
        if (definition.Value != null || definition.Name is not AstSymbol enumSymbol)
            return false;
        if (!TsLooksLikeEnumIife(statements[index + 1], enumSymbol.Name))
            return false;

        var localDefinitions = new StructRefList<AstVarDef>();
        localDefinitions.Add(new AstVarDef(definition,
            new AstSymbolLet(new AstSymbolRef(enumSymbol.Source, enumSymbol.Start, enumSymbol.End, enumSymbol.Name)),
            null));
        iifeBody.Add(new AstLet(varStatement.Source, varStatement.Start, varStatement.End, ref localDefinitions));
        iifeBody.Add(new TypeScriptNamespaceEnumIifeTransformer(SourceFile, namespaceName, enumSymbol.Name)
            .Transform(statements[index + 1]));
        index++;
        return true;
    }

    static bool TsLooksLikeEnumIife(AstNode statement, string enumName)
    {
        return statement is AstSimpleStatement
        {
            Body: AstCall
            {
                Args.Count: 1,
                Expression: AstFunction,
                Args: var args
            }
        } && args[0] is AstBinary
        {
            Operator: Operator.LogicalOr,
            Left: AstSymbolRef { Name: var leftName }
        } && leftName == enumName;
    }

    AstStatement TsBuildNamespaceVariable(string namespaceName, bool isExport, Position start, Position end)
    {
        var definitions = new StructRefList<AstVarDef>();
        var symbol = new AstSymbolVar(SourceFile, start, end, namespaceName, null);
        definitions.Add(new AstVarDef(SourceFile, start, end, symbol));
        var varStatement = new AstVar(SourceFile, start, end, ref definitions);
        if (!isExport)
            return varStatement;

        var specifiers = new StructList<AstNameMapping>();
        return new AstExport(SourceFile, start, end, null, varStatement, ref specifiers);
    }

    AstDefinitions TsBuildNamespaceLocalVariable(string namespaceName, Position start, Position end, bool local)
    {
        var definitions = new StructRefList<AstVarDef>();
        AstSymbolDeclaration symbol = local
            ? new AstSymbolLet(new AstSymbolRef(SourceFile, start, end, namespaceName))
            : new AstSymbolVar(SourceFile, start, end, namespaceName, null);
        definitions.Add(new AstVarDef(SourceFile, start, end, symbol));
        return local
            ? new AstLet(SourceFile, start, end, ref definitions)
            : new AstVar(SourceFile, start, end, ref definitions);
    }

    AstStatement TsBuildNamespaceIife(string namespaceName, ref StructRefList<AstNode> body, Position start, Position end)
    {
        var args = new StructList<AstNode>();
        args.Add(new AstSymbolFunarg(new AstSymbolRef(SourceFile, start, end, namespaceName), namespaceName));
        var function = new AstFunction(SourceFile, start, end, null, ref args, false, false, ref body);

        var callArgs = new StructList<AstNode>();
        var emptyObject = new AstObject(SourceFile, start, end);
        var namespaceRef = new AstSymbolRef(SourceFile, start, end, namespaceName);
        var namespaceAssign = new AstAssign(SourceFile, start, end, new AstSymbolRef(SourceFile, start, end, namespaceName),
            emptyObject, Operator.Assignment);
        callArgs.Add(new AstBinary(SourceFile, start, end, namespaceRef, namespaceAssign, Operator.LogicalOr));

        var call = new AstCall(SourceFile, start, end, function, ref callArgs);
        return new AstSimpleStatement(SourceFile, start, end, call);
    }

    AstStatement TsBuildNestedNamespaceIife(string namespaceName, string parentName, ref StructRefList<AstNode> body,
        Position start, Position end)
    {
        var args = new StructList<AstNode>();
        args.Add(new AstSymbolFunarg(new AstSymbolRef(SourceFile, start, end, namespaceName), namespaceName));
        var function = new AstFunction(SourceFile, start, end, null, ref args, false, false, ref body);

        var parentMember = new AstDot(SourceFile, start, end,
            new AstSymbolRef(SourceFile, start, end, parentName), namespaceName);
        var assignedParentMember = new AstDot(SourceFile, start, end,
            new AstSymbolRef(SourceFile, start, end, parentName), namespaceName);
        var parentAssign = new AstAssign(SourceFile, start, end, assignedParentMember,
            new AstObject(SourceFile, start, end), Operator.Assignment);
        var fallback = new AstBinary(SourceFile, start, end, parentMember, parentAssign, Operator.LogicalOr);
        var localAssign = new AstAssign(SourceFile, start, end,
            new AstSymbolRef(SourceFile, start, end, namespaceName), fallback, Operator.Assignment);

        var callArgs = new StructList<AstNode>();
        callArgs.Add(localAssign);
        var call = new AstCall(SourceFile, start, end, function, ref callArgs);
        return new AstSimpleStatement(SourceFile, start, end, call);
    }

    void TsLowerNamespaceStatement(AstNode statement, string namespaceName, string fullNamespaceName,
        HashSet<string> exportedNames, ref StructRefList<AstNode> iifeBody, ref int namespaceDestructuringTempIndex)
    {
        if (statement is AstTypeScriptOnly)
            return;

        if (statement is AstExport export)
        {
            TsLowerNamespaceExport(export, namespaceName, fullNamespaceName, exportedNames, ref iifeBody,
                ref namespaceDestructuringTempIndex);
            return;
        }

        if (statement is AstTry tryNode)
        {
            TsLowerNamespaceBlockStatements(ref tryNode.Body, namespaceName, fullNamespaceName, exportedNames,
                ref namespaceDestructuringTempIndex);
            iifeBody.Add(TsRewriteNamespaceExportReferences(tryNode, namespaceName, exportedNames));
            return;
        }

        if (statement is AstBlockStatement block)
        {
            TsLowerNamespaceBlockStatements(ref block.Body, namespaceName, fullNamespaceName, exportedNames,
                ref namespaceDestructuringTempIndex);
            iifeBody.Add(TsRewriteNamespaceExportReferences(block, namespaceName, exportedNames));
            return;
        }

        iifeBody.Add(TsRewriteNamespaceExportReferences(statement, namespaceName, exportedNames));
    }

    void TsLowerNamespaceBlockStatements(ref StructRefList<AstNode> body, string namespaceName, string fullNamespaceName,
        HashSet<string> exportedNames, ref int namespaceDestructuringTempIndex)
    {
        var loweredBody = new StructRefList<AstNode>();
        var pendingDestructuringTemps = new List<string>();
        var pendingDestructuringStatements = new StructRefList<AstNode>();
        AstNode? pendingDestructuringAnchor = null;

        void FlushPendingDestructuring()
        {
            if (pendingDestructuringStatements.Count == 0 || pendingDestructuringAnchor == null)
                return;

            if (pendingDestructuringTemps.Count != 0)
            {
                var tempDefinitions = new StructRefList<AstVarDef>();
                foreach (var temp in pendingDestructuringTemps)
                {
                    var symbol = new AstSymbolVar(SourceFile, pendingDestructuringAnchor.Start,
                        pendingDestructuringAnchor.End, temp, null);
                    tempDefinitions.Add(new AstVarDef(SourceFile, pendingDestructuringAnchor.Start,
                        pendingDestructuringAnchor.End, symbol));
                }
                loweredBody.Add(new AstVar(SourceFile, pendingDestructuringAnchor.Start, pendingDestructuringAnchor.End,
                    ref tempDefinitions));
            }

            foreach (var statement in pendingDestructuringStatements.AsReadOnlySpan())
                loweredBody.Add(statement);

            pendingDestructuringTemps.Clear();
            pendingDestructuringStatements.Clear();
            pendingDestructuringAnchor = null;
        }

        for (var i = 0u; i < body.Count; i++)
        {
            if (TsTryCollectNamespaceExportedDestructuringStatement(body[i], namespaceName, exportedNames,
                    pendingDestructuringTemps, ref pendingDestructuringStatements, ref namespaceDestructuringTempIndex,
                    ref pendingDestructuringAnchor))
                continue;

            FlushPendingDestructuring();
            TsLowerNamespaceStatement(body[i], namespaceName, fullNamespaceName, exportedNames, ref loweredBody,
                ref namespaceDestructuringTempIndex);
        }

        FlushPendingDestructuring();
        body.TransferFrom(ref loweredBody);
    }

    void TsLowerNamespaceExport(AstExport export, string namespaceName, string fullNamespaceName,
        HashSet<string> exportedNames, ref StructRefList<AstNode> iifeBody, ref int namespaceDestructuringTempIndex)
    {
        if (export.ExportedDefinition is AstDefinitions definitions)
        {
            TsLowerNamespaceExportedDefinitions(definitions, namespaceName, fullNamespaceName, exportedNames, ref iifeBody,
                ref namespaceDestructuringTempIndex);
            return;
        }

        if (export is { IsDefault: true, ExportedDefinition: AstDefun { Name: null } anonymousDefaultFunction })
        {
            var defaultName = TsNextInvalidDefaultExportName();
            anonymousDefaultFunction.Name = new AstSymbolDefun(new AstSymbolRef(SourceFile,
                anonymousDefaultFunction.Start, anonymousDefaultFunction.Start, defaultName));
        }

        if (export.ExportedDefinition is AstDefun or AstDefClass)
        {
            var declaration = TsRewriteNamespaceExportReferences(export.ExportedDefinition, namespaceName, exportedNames);
            iifeBody.Add(declaration);
            var name = TsDeclarationName(declaration);
            if (name.Length != 0)
                iifeBody.Add(TsBuildNamespaceExportAssignment(namespaceName, name, new AstSymbolRef(declaration, name), declaration));
            return;
        }

        foreach (var specifier in export.ExportedNames.AsReadOnlySpan())
        {
            if (!specifier.TypeScriptGeneratedNamespaceExport)
                continue;
            iifeBody.Add(TsBuildNamespaceExportAssignment(namespaceName, specifier.ForeignName.Name,
                new AstSymbolRef(SourceFile, specifier.Name.Start, specifier.Name.End, specifier.Name.Name),
                specifier));
        }
        // TypeScript erases user-authored export specifier lists inside namespaces.
    }

    void TsLowerNamespaceExportedDefinitions(AstDefinitions definitions, string namespaceName, string fullNamespaceName,
        HashSet<string> exportedNames, ref StructRefList<AstNode> iifeBody, ref int destructuringTempIndex)
    {
        var assignments = new StructRefList<AstNode>();
        for (var i = 0u; i < definitions.Definitions.Count; i++)
        {
            var definition = definitions.Definitions[i];
            if (definition.Name is not AstSymbol symbol)
            {
                TsFlushNamespaceExportedDefinitionAssignments(ref assignments, ref iifeBody, definitions);
                TsLowerNamespaceExportedDestructuring(definition, namespaceName, exportedNames, ref iifeBody,
                    ref destructuringTempIndex);
                continue;
            }

            if (definition.Value == null)
                continue;
            if (TsIsTypeOnlyImportEqualsValue(definition.Value, namespaceName, fullNamespaceName, exportedNames))
                continue;

            var value = TsRewriteNamespaceExportReferences(definition.Value, namespaceName, exportedNames);
            assignments.Add(TsBuildNamespaceExportAssignExpression(namespaceName, symbol.Name, value, definition));
        }
        TsFlushNamespaceExportedDefinitionAssignments(ref assignments, ref iifeBody, definitions);
    }

    void TsFlushNamespaceExportedDefinitionAssignments(ref StructRefList<AstNode> assignments,
        ref StructRefList<AstNode> iifeBody, AstNode positionHint)
    {
        if (assignments.Count == 0)
            return;
        AstNode body;
        if (assignments.Count == 1)
        {
            body = assignments[0];
        }
        else
        {
            var sequence = new StructRefList<AstNode>();
            sequence.TransferFrom(ref assignments);
            body = new AstSequence(SourceFile, positionHint.Start, positionHint.End, ref sequence);
        }
        iifeBody.Add(new AstSimpleStatement(SourceFile, positionHint.Start, positionHint.End, body));
        assignments = new StructRefList<AstNode>();
    }

    bool TsIsTypeOnlyImportEqualsValue(AstNode value, string namespaceName, string fullNamespaceName,
        HashSet<string> exportedNames)
    {
        return value switch
        {
            AstSymbolRef symbol => _tsErasedTypeOnlyNamespaces != null &&
                                   _tsErasedTypeOnlyNamespaces.Contains(symbol.Name),
            AstDot dot when TsDottedName(dot) is { } dottedName =>
                dottedName.StartsWith(fullNamespaceName + ".", StringComparison.Ordinal) &&
                TsDottedNameLeaf(dottedName) is { } leaf &&
                !exportedNames.Contains(leaf) ||
                _tsErasedTypeOnlyNamespaces != null &&
                TsDottedNameRoot(dottedName) is { } root &&
                _tsErasedTypeOnlyNamespaces.Contains(root),
            AstDot dot => TsIsTypeOnlyImportEqualsValue(dot.Expression, namespaceName, fullNamespaceName, exportedNames),
            _ => false
        };
    }

    static string? TsDottedName(AstNode node)
    {
        return node switch
        {
            AstSymbolRef symbol => symbol.Name,
            AstDot { Expression: var expression, PropertyAsString: var property } when property != null =>
                TsDottedName(expression) is { } prefix ? prefix + "." + property : null,
            _ => null
        };
    }

    static string? TsDottedNameLeaf(string dottedName)
    {
        var dot = dottedName.LastIndexOf('.');
        return dot >= 0 && dot + 1 < dottedName.Length ? dottedName[(dot + 1)..] : dottedName;
    }

    static string? TsDottedNameRoot(string dottedName)
    {
        var dot = dottedName.IndexOf('.');
        return dot > 0 ? dottedName[..dot] : dottedName;
    }

    void TsLowerNamespaceExportedDestructuring(AstVarDef definition, string namespaceName,
        HashSet<string> exportedNames, ref StructRefList<AstNode> iifeBody, ref int tempIndex)
    {
        if (definition.Value == null)
            return;

        var assignments = new StructRefList<AstNode>();
        var temps = new List<string>();
        TsCollectNamespaceDestructuringAssignments(definition.Name, definition.Value!, namespaceName, exportedNames,
            ref assignments, temps, ref tempIndex);

        if (assignments.Count == 0)
            return;

        if (temps.Count != 0)
        {
            var tempDefinitions = new StructRefList<AstVarDef>();
            foreach (var temp in temps)
            {
                var symbol = new AstSymbolVar(SourceFile, definition.Start, definition.End, temp, null);
                tempDefinitions.Add(new AstVarDef(SourceFile, definition.Start, definition.End, symbol));
            }
            iifeBody.Add(new AstVar(SourceFile, definition.Start, definition.End, ref tempDefinitions));
        }

        var body = assignments.Count == 1
            ? assignments[0]
            : new AstSequence(SourceFile, definition.Start, definition.End, ref assignments);
        iifeBody.Add(new AstSimpleStatement(SourceFile, definition.Start, definition.End, body));
    }

    bool TsTryCollectNamespaceExportedDestructuringStatement(AstNode statement, string namespaceName,
        HashSet<string> exportedNames, List<string> temps, ref StructRefList<AstNode> loweredStatements,
        ref int tempIndex, ref AstNode? anchor)
    {
        if (statement is not AstExport { ExportedDefinition: AstDefinitions definitions })
            return false;
        if (definitions.Definitions.Count == 0)
            return false;

        foreach (var definition in definitions.Definitions.AsReadOnlySpan())
        {
            if (definition.Name is AstSymbol)
                return false;
        }

        anchor ??= definitions;
        foreach (var definition in definitions.Definitions.AsReadOnlySpan())
        {
            if (definition.Value == null)
                continue;

            var assignments = new StructRefList<AstNode>();
            TsCollectNamespaceDestructuringAssignments(definition.Name, definition.Value!, namespaceName, exportedNames,
                ref assignments, temps, ref tempIndex);
            if (assignments.Count == 0)
                continue;

            var body = assignments.Count == 1
                ? assignments[0]
                : new AstSequence(SourceFile, definition.Start, definition.End, ref assignments);
            loweredStatements.Add(new AstSimpleStatement(SourceFile, definition.Start, definition.End, body));
        }

        return true;
    }

    void TsCollectNamespaceDestructuringAssignments(AstNode pattern, AstNode sourceValue, string namespaceName,
        HashSet<string> exportedNames, ref StructRefList<AstNode> assignments, List<string> temps, ref int tempIndex)
    {
        if (pattern is AstDestructuring destructuringPattern &&
            sourceValue is not AstSymbolRef &&
            TsNamespaceDestructuringImmediateSourceReadCount(destructuringPattern) > 1)
        {
            var sourceTemp = TsNewNamespaceDestructuringTemp(temps, ref tempIndex);
            assignments.Add(new AstAssign(SourceFile, pattern.Start, pattern.End,
                new AstSymbolRef(SourceFile, pattern.Start, pattern.End, sourceTemp),
                TsRewriteNamespaceExportReferences(sourceValue.DeepClone(), namespaceName, exportedNames),
                Operator.Assignment));
            sourceValue = new AstSymbolRef(SourceFile, pattern.Start, pattern.End, sourceTemp);
        }

        switch (pattern)
        {
            case AstSymbol symbol:
                assignments.Add(TsBuildNamespaceExportAssignExpression(namespaceName, symbol.Name,
                    TsRewriteNamespaceExportReferences(sourceValue.DeepClone(), namespaceName, exportedNames), symbol));
                return;
            case AstDefaultAssign defaultAssign:
            {
                var temp = TsNewNamespaceDestructuringTemp(temps, ref tempIndex);
                assignments.Add(new AstAssign(SourceFile, defaultAssign.Start, defaultAssign.End,
                    new AstSymbolRef(SourceFile, defaultAssign.Start, defaultAssign.End, temp),
                    TsRewriteNamespaceExportReferences(sourceValue.DeepClone(), namespaceName, exportedNames),
                    Operator.Assignment));
                var tempRef = new AstSymbolRef(SourceFile, defaultAssign.Start, defaultAssign.End, temp);
                var condition = new AstBinary(SourceFile, defaultAssign.Start, defaultAssign.End, tempRef,
                    new AstUnaryPrefix(SourceFile, defaultAssign.Start, defaultAssign.End, Operator.Void,
                        new AstNumber(SourceFile, defaultAssign.Start, defaultAssign.End, 0, "0")),
                    Operator.StrictEquals);
                var value = new AstConditional(SourceFile, defaultAssign.Start, defaultAssign.End, condition,
                    TsRewriteNamespaceExportReferences(defaultAssign.Right.DeepClone(), namespaceName, exportedNames),
                    new AstSymbolRef(SourceFile, defaultAssign.Start, defaultAssign.End, temp));
                TsCollectNamespaceDestructuringAssignments(defaultAssign.Left, value, namespaceName, exportedNames,
                    ref assignments, temps, ref tempIndex);
                return;
            }
            case AstDestructuring { IsArray: true } destructuring:
                for (var i = 0u; i < destructuring.Names.Count; i++)
                {
                    var item = destructuring.Names[i];
                    if (item is AstHole)
                        continue;
                    if (item is AstExpansion expansion)
                    {
                        TsCollectNamespaceDestructuringAssignments(expansion.Expression,
                            TsBuildArrayRestSlice(sourceValue, i, expansion), namespaceName, exportedNames,
                            ref assignments, temps, ref tempIndex);
                        continue;
                    }
                    var element = new AstSub(SourceFile, item.Start, item.End, sourceValue.DeepClone(),
                        new AstNumber(SourceFile, item.Start, item.End, i, i.ToString(CultureInfo.InvariantCulture)));
                    TsCollectNamespaceDestructuringAssignments(item, element, namespaceName, exportedNames,
                        ref assignments, temps, ref tempIndex);
                }
                return;
            case AstDestructuring destructuring:
                var excludedKeys = new List<AstNode>();
                foreach (var item in destructuring.Names.AsReadOnlySpan())
                {
                    if (item is AstExpansion expansion)
                    {
                        TsCollectNamespaceDestructuringAssignments(expansion.Expression,
                            TsBuildObjectRestCall(sourceValue, excludedKeys, expansion), namespaceName, exportedNames,
                            ref assignments, temps, ref tempIndex);
                        continue;
                    }
                    if (item is not AstObjectKeyVal keyValue)
                    {
                        Raise(item.Start, "Unsupported namespace exported object destructuring pattern");
                        continue;
                    }
                    var key = TsPrepareObjectDestructuringKey(keyValue.Key, ref assignments, temps, ref tempIndex,
                        namespaceName, exportedNames);
                    excludedKeys.Add(TsBuildObjectRestExcludedKey(key));
                    var property = TsBuildDestructuringPropertyAccess(sourceValue, key);
                    if (keyValue.Value is AstDestructuring nestedObjectPattern
                        && !nestedObjectPattern.IsArray
                        && TsNamespaceObjectPatternNeedsSourceTemp(nestedObjectPattern))
                    {
                        var sourceTemp = TsNewNamespaceDestructuringTemp(temps, ref tempIndex);
                        assignments.Add(new AstAssign(SourceFile, keyValue.Start, keyValue.End,
                            new AstSymbolRef(SourceFile, keyValue.Start, keyValue.End, sourceTemp),
                            TsRewriteNamespaceExportReferences(property, namespaceName, exportedNames),
                            Operator.Assignment));
                        property = new AstSymbolRef(SourceFile, keyValue.Start, keyValue.End, sourceTemp);
                    }
                    TsCollectNamespaceDestructuringAssignments(keyValue.Value, property, namespaceName, exportedNames,
                        ref assignments, temps, ref tempIndex);
                }
                return;
            default:
                Raise(pattern.Start, "Unsupported namespace exported destructuring pattern");
                return;
        }
    }

    static int TsNamespaceDestructuringImmediateSourceReadCount(AstDestructuring destructuring)
    {
        var count = 0;
        foreach (var item in destructuring.Names.AsReadOnlySpan())
        {
            if (item is AstHole)
                continue;
            count++;
        }
        return count;
    }

    static bool TsNamespaceObjectPatternNeedsSourceTemp(AstDestructuring destructuring)
    {
        foreach (var item in destructuring.Names.AsReadOnlySpan())
        {
            if (item is AstExpansion)
                return true;
        }
        return false;
    }

    AstNode TsPrepareObjectDestructuringKey(AstNode key, ref StructRefList<AstNode> assignments, List<string> temps,
        ref int tempIndex, string namespaceName, HashSet<string> exportedNames)
    {
        if (key is AstSymbolProperty or AstString or AstNumber)
            return key;

        var temp = TsNewNamespaceDestructuringTemp(temps, ref tempIndex);
        assignments.Add(new AstAssign(SourceFile, key.Start, key.End,
            new AstSymbolRef(SourceFile, key.Start, key.End, temp),
            TsRewriteNamespaceExportReferences(key.DeepClone(), namespaceName, exportedNames), Operator.Assignment));
        return new AstSymbolRef(SourceFile, key.Start, key.End, temp);
    }

    AstNode TsBuildDestructuringPropertyAccess(AstNode sourceValue, AstNode key)
    {
        return key switch
        {
            AstSymbolProperty symbol when OutputContext.IsIdentifierString(symbol.Name) =>
                new AstDot(SourceFile, key.Start, key.End, sourceValue.DeepClone(), symbol.Name),
            _ => new AstSub(SourceFile, key.Start, key.End, sourceValue.DeepClone(), key.DeepClone())
        };
    }

    AstNode TsBuildObjectRestExcludedKey(AstNode key)
    {
        return key switch
        {
            AstSymbolProperty symbol => new AstString(SourceFile, key.Start, key.End, symbol.Name),
            AstString str => str.DeepClone(),
            AstNumber number => new AstString(SourceFile, key.Start, key.End,
                number.Value.ToString(CultureInfo.InvariantCulture)),
            AstSymbolRef tempRef => TsBuildComputedRestExcludedKey(tempRef),
            _ => throw new SyntaxError("Unsupported namespace exported object rest destructuring key", key.Start)
        };
    }

    AstConditional TsBuildComputedRestExcludedKey(AstSymbolRef tempRef)
    {
        var typeOfTemp = new AstUnaryPrefix(SourceFile, tempRef.Start, tempRef.End, Operator.TypeOf,
            tempRef.DeepClone());
        var condition = new AstBinary(SourceFile, tempRef.Start, tempRef.End, typeOfTemp,
            new AstString(SourceFile, tempRef.Start, tempRef.End, "symbol"), Operator.StrictEquals);
        var stringCoerce = new AstBinary(SourceFile, tempRef.Start, tempRef.End, tempRef.DeepClone(),
            new AstString(SourceFile, tempRef.Start, tempRef.End, ""), Operator.Addition);
        return new AstConditional(SourceFile, tempRef.Start, tempRef.End, condition, tempRef.DeepClone(), stringCoerce);
    }

    AstCall TsBuildObjectRestCall(AstNode sourceValue, List<AstNode> excludedKeys, AstNode positionHint)
    {
        var elements = new StructList<AstNode>();
        foreach (var key in excludedKeys)
            elements.Add(key.DeepClone());
        var excluded = new AstArray(SourceFile, positionHint.Start, positionHint.End, ref elements);

        var args = new StructList<AstNode>();
        args.Add(sourceValue.DeepClone());
        args.Add(excluded);
        return new AstCall(SourceFile, positionHint.Start, positionHint.End,
            new AstSymbolRef(SourceFile, positionHint.Start, positionHint.End, "__rest"), ref args);
    }

    AstCall TsBuildArrayRestSlice(AstNode sourceValue, uint startIndex, AstNode positionHint)
    {
        var args = new StructList<AstNode>();
        args.Add(new AstNumber(SourceFile, positionHint.Start, positionHint.End, startIndex,
            startIndex.ToString(CultureInfo.InvariantCulture)));
        var slice = new AstDot(SourceFile, positionHint.Start, positionHint.End, sourceValue.DeepClone(), "slice");
        return new AstCall(SourceFile, positionHint.Start, positionHint.End, slice, ref args);
    }

    static string TsNewNamespaceDestructuringTemp(List<string> temps, ref int tempIndex)
    {
        while (tempIndex is 8 or 13)
            tempIndex++;
        var index = tempIndex++;
        var name = index < 26
            ? "_" + (char)('a' + index)
            : "_tmp" + index.ToString(CultureInfo.InvariantCulture);
        temps.Add(name);
        return name;
    }

    AstSimpleStatement TsBuildNamespaceExportAssignment(string namespaceName, string exportedName, AstNode value,
        AstNode positionHint)
    {
        var left = new AstDot(SourceFile, positionHint.Start, positionHint.End,
            new AstSymbolRef(SourceFile, positionHint.Start, positionHint.End, namespaceName), exportedName);
        var assignment = new AstAssign(SourceFile, positionHint.Start, positionHint.End, left, value, Operator.Assignment);
        return new AstSimpleStatement(SourceFile, positionHint.Start, positionHint.End, assignment);
    }

    AstAssign TsBuildNamespaceExportAssignExpression(string namespaceName, string exportedName, AstNode value,
        AstNode positionHint)
    {
        var left = new AstDot(SourceFile, positionHint.Start, positionHint.End,
            new AstSymbolRef(SourceFile, positionHint.Start, positionHint.End, namespaceName), exportedName);
        return new AstAssign(SourceFile, positionHint.Start, positionHint.End, left, value, Operator.Assignment);
    }

    AstNode TsRewriteNamespaceExportReferences(AstNode node, string namespaceName, HashSet<string> exportedNames)
    {
        return new TypeScriptNamespaceReferenceTransformer(SourceFile, namespaceName, exportedNames).Transform(node);
    }

    static string TsDeclarationName(AstNode declaration)
    {
        return declaration switch
        {
            AstDefun { Name: { } name } => name.Name,
            AstDefClass { Name: { } name } => name.Name,
            _ => string.Empty
        };
    }

    static HashSet<string> TsCollectNamespaceVariableExportedNames(AstToplevel body)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0u; i < body.Body.Count; i++)
        {
            var statement = body.Body[i];
            TsCollectNamespaceVariableExportedNames(statement, names,
                i + 1 < body.Body.Count ? body.Body[i + 1] : null);
        }

        return names;
    }

    static void TsCollectNamespaceAmbientExportedValueNames(string body, HashSet<string> names)
    {
        var searchable = TsEraseCommentsAndStrings(body);
        foreach (Match match in Regex.Matches(searchable,
                     @"\bexport\s+declare\s+(?:var|let|const)\s+(?<name>[A-Za-z_$][\w$]*)\b"))
        {
            names.Add(match.Groups["name"].Value);
        }
    }

    static void TsCollectNamespaceVariableExportedNames(AstNode statement, HashSet<string> names, AstNode? nextStatement)
    {
        if (statement is AstExport { ExportedDefinition: AstDefinitions definitions })
        {
            foreach (var definition in definitions.Definitions.AsReadOnlySpan())
            {
                if (definition.Name is AstSymbol symbol)
                {
                    if (definition.Value == null && nextStatement != null &&
                        TsLooksLikeEnumIife(nextStatement, symbol.Name))
                        continue;
                    names.Add(symbol.Name);
                    continue;
                }

                TsCollectBindingNames(definition.Name, names);
            }
            return;
        }

        if (statement is AstTry tryNode)
        {
            for (var i = 0u; i < tryNode.Body.Count; i++)
                TsCollectNamespaceVariableExportedNames(tryNode.Body[i], names,
                    i + 1 < tryNode.Body.Count ? tryNode.Body[i + 1] : null);
            return;
        }

        if (statement is AstBlockStatement block)
        {
            for (var i = 0u; i < block.Body.Count; i++)
                TsCollectNamespaceVariableExportedNames(block.Body[i], names,
                    i + 1 < block.Body.Count ? block.Body[i + 1] : null);
        }
    }

    static void TsCollectBindingNames(AstNode node, HashSet<string> names)
    {
        switch (node)
        {
            case AstSymbolDeclaration symbol:
                names.Add(symbol.Name);
                break;
            case AstExpansion expansion:
                TsCollectBindingNames(expansion.Expression, names);
                break;
            case AstDefaultAssign defaultAssign:
                TsCollectBindingNames(defaultAssign.Left, names);
                break;
            case AstDestructuring destructuring:
                foreach (var name in destructuring.Names.AsReadOnlySpan())
                    TsCollectBindingNames(name, names);
                break;
            case AstObjectKeyVal keyValue:
                TsCollectBindingNames(keyValue.Value, names);
                break;
        }
    }

    sealed class TypeScriptNamespaceReferenceTransformer : TreeTransformer
    {
        readonly string? _sourceFile;
        readonly string _namespaceName;
        readonly HashSet<string> _exportedNames;
        readonly Stack<HashSet<string>> _localScopes = new();

        public TypeScriptNamespaceReferenceTransformer(string? sourceFile, string namespaceName,
            HashSet<string> exportedNames)
        {
            _sourceFile = sourceFile;
            _namespaceName = namespaceName;
            _exportedNames = exportedNames;
            _localScopes.Push(new HashSet<string>(StringComparer.Ordinal));
        }

        protected override AstNode? Before(AstNode node, bool inList)
        {
            if (node is AstForIn forIn)
            {
                _localScopes.Push(new HashSet<string>(StringComparer.Ordinal));
                try
                {
                    forIn.Init = Transform(forIn.Init);
                }
                finally
                {
                    _localScopes.Pop();
                }

                forIn.Object = Transform(forIn.Object);

                var scope = new HashSet<string>(StringComparer.Ordinal);
                CollectBindingNames(forIn.Init, scope);
                _localScopes.Push(scope);
                try
                {
                    forIn.Body = (AstStatement)Transform(forIn.Body);
                }
                finally
                {
                    _localScopes.Pop();
                }

                return node;
            }

            if (node is AstFor forNode)
            {
                _localScopes.Push(new HashSet<string>(StringComparer.Ordinal));
                try
                {
                    if (forNode.Init != null)
                        forNode.Init = Transform(forNode.Init);
                    if (forNode.Condition != null)
                        forNode.Condition = Transform(forNode.Condition);
                    if (forNode.Step != null)
                        forNode.Step = Transform(forNode.Step);
                    forNode.Body = (AstStatement)Transform(forNode.Body);
                }
                finally
                {
                    _localScopes.Pop();
                }

                return node;
            }

            if (node is AstSwitch switchNode)
            {
                switchNode.Expression = Transform(switchNode.Expression);

                var scope = new HashSet<string>(StringComparer.Ordinal);
                foreach (var branchNode in switchNode.Body.AsReadOnlySpan())
                {
                    if (branchNode is not AstSwitchBranch branch)
                        continue;
                    foreach (var statement in branch.Body.AsReadOnlySpan())
                        CollectStatementBindingNames(statement, scope);
                }

                _localScopes.Push(scope);
                try
                {
                    TransformList(ref switchNode.Body);
                }
                finally
                {
                    _localScopes.Pop();
                }

                return node;
            }

            if (node is AstLambda lambda)
            {
                var scope = new HashSet<string>(StringComparer.Ordinal);
                foreach (var arg in lambda.ArgNames.AsReadOnlySpan())
                    CollectBindingNames(arg, scope);
                _localScopes.Push(scope);
                return null;
            }

            if (node is AstCatch catchNode)
            {
                var scope = new HashSet<string>(StringComparer.Ordinal);
                if (catchNode.Argname != null)
                    CollectBindingNames(catchNode.Argname, scope);
                _localScopes.Push(scope);
                return null;
            }

            if (node is AstBlockStatement)
            {
                _localScopes.Push(new HashSet<string>(StringComparer.Ordinal));
                return null;
            }

            if (node is AstDefinitions definitions)
            {
                foreach (var definition in definitions.Definitions.AsReadOnlySpan())
                    CollectBindingNames(definition.Name, _localScopes.Peek());
                return null;
            }

            if (node is AstSymbolRef symbolRef && _exportedNames.Contains(symbolRef.Name) &&
                !IsLocalName(symbolRef.Name))
                return new AstDot(_sourceFile, symbolRef.Start, symbolRef.End,
                    new AstSymbolRef(_sourceFile, symbolRef.Start, symbolRef.End, _namespaceName), symbolRef.Name);
            return null;
        }

        protected override AstNode? After(AstNode node, bool inList)
        {
            if (node is AstLambda or AstCatch or AstBlockStatement)
                _localScopes.Pop();
            return null;
        }

        bool IsLocalName(string name)
        {
            foreach (var scope in _localScopes)
            {
                if (scope.Contains(name))
                    return true;
            }
            return false;
        }

        static void CollectBindingNames(AstNode node, HashSet<string> names)
        {
            switch (node)
            {
                case AstSymbolDeclaration symbol:
                    names.Add(symbol.Name);
                    break;
                case AstExpansion expansion:
                    CollectBindingNames(expansion.Expression, names);
                    break;
                case AstDefaultAssign defaultAssign:
                    CollectBindingNames(defaultAssign.Left, names);
                    break;
                case AstDestructuring destructuring:
                    foreach (var name in destructuring.Names.AsReadOnlySpan())
                        CollectBindingNames(name, names);
                    break;
                case AstObjectKeyVal keyValue:
                    CollectBindingNames(keyValue.Value, names);
                    break;
            }
        }

        static void CollectStatementBindingNames(AstNode node, HashSet<string> names)
        {
            switch (node)
            {
                case AstDefinitions definitions:
                    foreach (var definition in definitions.Definitions.AsReadOnlySpan())
                        CollectBindingNames(definition.Name, names);
                    break;
                case AstDefun { Name: { } name }:
                    names.Add(name.Name);
                    break;
                case AstDefClass { Name: { } name }:
                    names.Add(name.Name);
                    break;
            }
        }
    }

    sealed class TypeScriptNamespaceEnumIifeTransformer : TreeTransformer
    {
        readonly string? _sourceFile;
        readonly string _namespaceName;
        readonly string _enumName;

        public TypeScriptNamespaceEnumIifeTransformer(string? sourceFile, string namespaceName, string enumName)
        {
            _sourceFile = sourceFile;
            _namespaceName = namespaceName;
            _enumName = enumName;
        }

        protected override AstNode? Before(AstNode node, bool inList)
        {
            return null;
        }

        protected override AstNode? After(AstNode node, bool inList)
        {
            if (node is AstCall { Args.Count: 1 } call &&
                call.Args[0] is AstBinary { Operator: Operator.LogicalOr } currentArg)
            {
                var emptyObject = currentArg.Right is AstAssign { Right: { } assignedValue }
                    ? assignedValue
                    : currentArg.Right;
                var nsEnum = new AstDot(_sourceFile, currentArg.Start, currentArg.End,
                    new AstSymbolRef(_sourceFile, currentArg.Start, currentArg.End, _namespaceName), _enumName);
                var nsEnumAssign = new AstAssign(_sourceFile, currentArg.Start, currentArg.End,
                    new AstDot(_sourceFile, currentArg.Start, currentArg.End,
                        new AstSymbolRef(_sourceFile, currentArg.Start, currentArg.End, _namespaceName), _enumName),
                    emptyObject, Operator.Assignment);
                var fallback = new AstBinary(_sourceFile, currentArg.Start, currentArg.End, nsEnum, nsEnumAssign,
                    Operator.LogicalOr);
                call.Args.SetItem(0, new AstAssign(_sourceFile, currentArg.Start, currentArg.End,
                    new AstSymbolRef(_sourceFile, currentArg.Start, currentArg.End, _enumName), fallback,
                    Operator.Assignment));
            }

            return null;
        }
    }

    bool TsTryParseEnumStatements(out List<AstStatement> statements, bool local = false,
        bool preserveConstEnum = false)
    {
        statements = null!;
        var isExport = false;
        var isConst = false;
        if (Type == TokenType.Export)
        {
            if (!TsExportStartsEnum(out isConst))
                return false;
            isExport = true;
            Next();
        }

        if (Type == TokenType.Const)
        {
            if (!TsTokenFollowedByEnum())
                return false;
            isConst = true;
            Next();
        }

        if (!IsContextual("enum"))
        {
            return false;
        }

        Next();
        var name = Type == TokenType.Name ? Value?.ToString() : null;
        if (name == null)
            Raise(Start, "Unexpected token");
        var enumName = name!;
        Next();
        Expect(TokenType.BraceL);
        var members = new List<AstTypeScriptEnumMember>();
        while (Type != TokenType.BraceR && Type != TokenType.Eof)
        {
            var memberName = TsParseEnumMemberName();
            string? value = null;
            AstNode? valueExpression = null;
            var forceReverseMap = false;
            if (Eat(TokenType.Eq))
            {
                var valueStart = Start.Index;
                valueExpression = ParseMaybeAssign(Start);
                value = _input.Substring(valueStart, _lastTokEnd.Index - valueStart).Trim();
                forceReverseMap = TsEnumInitializerHadErasedAssertion(valueStart, _lastTokEnd.Index);
            }

            members.Add(memberName with
            {
                Value = value,
                ValueExpression = valueExpression,
                ForceReverseMap = forceReverseMap
            });
            Eat(TokenType.Comma);
        }

        Expect(TokenType.BraceR);
        _tsHasEnumNodes = true;
        statements = new List<AstStatement>
        {
            new AstTypeScriptEnum(SourceFile, Start, _lastTokEnd, enumName, isExport, isConst, local,
                preserveConstEnum, members)
        };
        return true;
    }

    void TsMoveToIndexAndReadToken(int index, bool expressionAllowed = false)
    {
        var line = Start.Line;
        var column = Start.Column;
        for (var i = Start.Index; i < index && i < _input.Length; i++)
        {
            if (_input[i] == '\r')
            {
                if (i + 1 < index && _input[i + 1] == '\n')
                    i++;
                line++;
                column = 0;
                continue;
            }
            if (_input[i] == '\n')
            {
                line++;
                column = 0;
                continue;
            }
            column++;
        }

        _lastTokStart = Start;
        _lastTokEnd = End;
        _pos = new Position(line, column, index);
        while (_context.Count > 0 && (_context[^1] == TokContext.QTmpl || _context[^1] == TokContext.BTmpl))
            _context.RemoveAt(_context.Count - 1);
        _exprAllowed = expressionAllowed;
        NextToken();
    }

    int TsFindMatchingSkippingLiterals(int index, char open, char close)
    {
        var depth = 0;
        for (var i = index; i < _input.Length; i++)
        {
            var ch = _input[i];
            if (ch is '"' or '\'')
            {
                i = TsSkipStringLike(i, ch) - 1;
                continue;
            }
            if (ch == '`')
            {
                i = TsSkipTemplateLiteral(i) - 1;
                continue;
            }
            if (ch == '/' && i + 1 < _input.Length)
            {
                if (_input[i + 1] == '/')
                {
                    i += 2;
                    while (i < _input.Length && _input[i] is not '\r' and not '\n')
                        i++;
                    continue;
                }
                if (_input[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < _input.Length && !(_input[i] == '*' && _input[i + 1] == '/'))
                        i++;
                    i = Math.Min(i + 1, _input.Length - 1);
                    continue;
                }
                if (TsCanStartRegexLiteralAt(i))
                {
                    i = TsSkipRegexLiteral(i) - 1;
                    continue;
                }
            }
            if (ch == open)
            {
                depth++;
                continue;
            }
            if (ch == close && --depth == 0)
                return i;
        }

        return -1;
    }

    int TsSkipRegexLiteral(int index)
    {
        index++;
        var escaped = false;
        var inClass = false;
        while (index < _input.Length)
        {
            var ch = _input[index];
            if (IsNewLine(ch))
                return index;
            if (escaped)
            {
                escaped = false;
                index++;
                continue;
            }
            if (ch == '\\')
            {
                escaped = true;
                index++;
                continue;
            }
            if (ch == '[')
            {
                inClass = true;
                index++;
                continue;
            }
            if (ch == ']' && inClass)
            {
                inClass = false;
                index++;
                continue;
            }
            if (ch == '/' && !inClass)
            {
                index++;
                while (index < _input.Length && IsIdentifierChar(_input[index], true))
                    index++;
                return index;
            }
            index++;
        }

        return index;
    }

    bool TsCanStartRegexLiteralAt(int index)
    {
        var previous = index - 1;
        while (previous >= 0 && char.IsWhiteSpace(_input[previous]))
            previous--;
        if (previous < 0)
            return true;
        var ch = _input[previous];
        if ("([{=,:;!&|?+-*~^<>%".IndexOf(ch) >= 0)
            return true;
        if (!IsIdentifierChar(ch, true))
            return false;
        var end = previous + 1;
        while (previous >= 0 && IsIdentifierChar(_input[previous], true))
            previous--;
        var word = _input.Substring(previous + 1, end - previous - 1);
        return word is "return" or "throw" or "case" or "delete" or "void" or "typeof" or "instanceof" or "in" or
            "of" or "yield" or "await";
    }

    int TsSkipTemplateLiteral(int index)
    {
        index++;
        while (index < _input.Length)
        {
            var ch = _input[index];
            if (ch == '\\')
            {
                index += 2;
                continue;
            }
            if (ch == '`')
                return index + 1;
            if (ch == '$' && index + 1 < _input.Length && _input[index + 1] == '{')
            {
                index = TsSkipTemplateExpression(index + 2);
                continue;
            }
            index++;
        }

        return index;
    }

    int TsSkipTemplateExpression(int index)
    {
        var braceDepth = 0;
        var parenDepth = 0;
        var bracketDepth = 0;
        while (index < _input.Length)
        {
            var ch = _input[index];
            if (ch is '"' or '\'')
            {
                index = TsSkipStringLike(index, ch);
                continue;
            }
            if (ch == '`')
            {
                index = TsSkipTemplateLiteral(index);
                continue;
            }
            if (ch == '/' && index + 1 < _input.Length)
            {
                if (_input[index + 1] == '/')
                {
                    index += 2;
                    while (index < _input.Length && _input[index] is not '\r' and not '\n')
                        index++;
                    continue;
                }
                if (_input[index + 1] == '*')
                {
                    index += 2;
                    while (index + 1 < _input.Length && !(_input[index] == '*' && _input[index + 1] == '/'))
                        index++;
                    index = Math.Min(index + 2, _input.Length);
                    continue;
                }
            }
            switch (ch)
            {
                case '{': braceDepth++; break;
                case '}':
                    if (braceDepth == 0 && parenDepth == 0 && bracketDepth == 0)
                        return index + 1;
                    if (braceDepth > 0)
                        braceDepth--;
                    break;
                case '(': parenDepth++; break;
                case ')':
                    if (parenDepth > 0)
                        parenDepth--;
                    break;
                case '[': bracketDepth++; break;
                case ']':
                    if (bracketDepth > 0)
                        bracketDepth--;
                    break;
            }
            index++;
        }

        return index;
    }

    Dictionary<string, Dictionary<string, string>>? TsSnapshotRuntimeEnumConstants()
    {
        return _tsRuntimeEnumConstants == null
            ? null
            : new Dictionary<string, Dictionary<string, string>>(_tsRuntimeEnumConstants, StringComparer.Ordinal);
    }

    void TsRestoreRuntimeEnumConstants(Dictionary<string, Dictionary<string, string>>? snapshot)
    {
        _tsRuntimeEnumConstants = snapshot;
    }

    AstTypeScriptEnumMember TsParseEnumMemberName()
    {
        if (Type == TokenType.Name)
        {
            var name = Value!.ToString()!;
            Next();
            return new AstTypeScriptEnumMember(name, new AstString(name), new AstString(name),
                name, null, null, false);
        }

        if (Type == TokenType.String)
        {
            var name = Value!.ToString()!;
            Next();
            return new AstTypeScriptEnumMember(name, new AstString(name), new AstString(name),
                name, null, null, false);
        }

        if (Type is TokenType.Num or TokenType.BigInt)
        {
            var raw = Value!.ToString()!;
            var expression = ParseExpressionAtom(Start);
            return new AstTypeScriptEnumMember(raw, expression, expression.DeepClone(), null, null, null, false);
        }

        var keyword = TokenInformation.Types[Type].Keyword;
        if (keyword != null)
        {
            Next();
            return new AstTypeScriptEnumMember(keyword, new AstString(keyword),
                new AstString(keyword), keyword, null, null, false);
        }

        if (Type == TokenType.BracketL)
        {
            Next();
            var keyExpression = ParseExpression(Start);
            Expect(TokenType.BracketR);
            return new AstTypeScriptEnumMember(keyExpression.PrintToString(), keyExpression, keyExpression.DeepClone(),
                TsTryGetLiteralEnumMemberReferenceName(keyExpression), null, null, false);
        }

        Raise(Start, "Unexpected token");
        throw new InvalidOperationException();
    }

    static string? TsTryGetLiteralEnumMemberReferenceName(AstNode expression)
    {
        return expression switch
        {
            AstString { Value: var value } => value,
            AstTemplateString { Segments.Count: 1, Segments: var segments } =>
                ((AstTemplateSegment)segments[0]).Value,
            _ => null
        };
    }

    bool TsEnumInitializerHadErasedAssertion(int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            var ch = _input[index];
            if (ch is '"' or '\'')
            {
                index = TsSkipStringLike(index, ch) - 1;
                continue;
            }
            if (ch == '`')
            {
                index = TsSkipTemplateLiteral(index) - 1;
                continue;
            }
            if (ch == '!' && (index + 1 >= end || _input[index + 1] != '='))
                return true;
            if ((ch == 'a' && TsTextStartsKeyword(_input, index, "as")) ||
                (ch == 's' && TsTextStartsKeyword(_input, index, "satisfies")))
                return true;
        }

        return false;
    }

    static AstNode TsParseEnumInitializerExpression(string expression)
    {
        var parsed = TypeScriptParser.Parse("const __bbEnumValue = " + expression + ";", new Options
        {
            SourceType = SourceType.Module,
            EcmaVersion = 2022,
            PreserveConstEnums = true
        });
        if (parsed.Body.Count != 1 ||
            parsed.Body[0] is not AstConst { Definitions.Count: 1 } constStatement ||
            constStatement.Definitions[0].Value is not { } value)
            throw new InvalidOperationException("Could not parse enum initializer expression");
        return value;
    }

    bool TsExportStartsEnum(out bool isConst)
    {
        isConst = false;
        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        if (TsTextStartsKeyword(index, "enum")) return true;
        if (!TsTextStartsKeyword(index, "const")) return false;
        index += "const".Length;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        if (!TsTextStartsKeyword(index, "enum")) return false;
        isConst = true;
        return true;
    }

    bool TsTokenFollowedByEnum()
    {
        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        return TsTextStartsKeyword(index, "enum");
    }

    AstToplevel TsLowerTypeScriptEnumsPostParse(AstToplevel node)
    {
        if (!_tsHasEnumNodes)
            return node;

        node = (AstToplevel)new TypeScriptEnumLoweringTransformer(this).Transform(node);
        if (_tsConstEnums is { Count: > 0 })
            new TypeScriptConstEnumInlineTransformer(SourceFile, _tsConstEnums).Transform(node);
        return node;
    }

    sealed class TypeScriptEnumLoweringTransformer : TreeTransformer
    {
        readonly Parser _parser;
        readonly Stack<Dictionary<string, Dictionary<string, string>>?> _runtimeEnumScopes = new();
        readonly Stack<HashSet<string>> _declarationScopes = new();
        readonly Stack<HashSet<string>> _enumDeclarationScopes = new();
        readonly Stack<Dictionary<string, AstNode>> _constScopes = new();
        readonly HashSet<string> _currentDeclarations = new(StringComparer.Ordinal);
        readonly HashSet<string> _currentEnumDeclarations = new(StringComparer.Ordinal);
        readonly Dictionary<string, AstNode> _currentConstValues = new(StringComparer.Ordinal);

        public TypeScriptEnumLoweringTransformer(Parser parser)
        {
            _parser = parser;
        }

        protected override AstNode? Before(AstNode node, bool inList)
        {
            if (node is AstLambda or AstBlock)
            {
                _runtimeEnumScopes.Push(_parser._tsRuntimeEnumConstants == null
                    ? null
                    : new Dictionary<string, Dictionary<string, string>>(_parser._tsRuntimeEnumConstants,
                        StringComparer.Ordinal));
                _declarationScopes.Push(new HashSet<string>(_currentDeclarations, StringComparer.Ordinal));
                _enumDeclarationScopes.Push(new HashSet<string>(_currentEnumDeclarations, StringComparer.Ordinal));
                _constScopes.Push(new Dictionary<string, AstNode>(_currentConstValues, StringComparer.Ordinal));
                _currentDeclarations.Clear();
                _currentEnumDeclarations.Clear();
                _currentConstValues.Clear();
                return null;
            }

            if (node is AstDefinitions definitions)
                RememberConstDefinitions(definitions);

            if (node is AstVar { Definitions.Count: 1 } varStatement &&
                varStatement.Definitions[0] is { Value: null, Name: AstSymbol { Name: var varName } } &&
                _currentEnumDeclarations.Contains(varName))
                return inList ? Remove : new AstEmptyStatement(node.Source, node.Start, node.End);

            if (node is not AstTypeScriptEnum enumNode)
                return null;

            if (enumNode.IsConst && !enumNode.IsLocal && !enumNode.PreserveConstEnum &&
                !_parser.Options.PreserveConstEnums &&
                TsTryEvaluateConstEnumMembers(enumNode.Members, out var values))
            {
                _parser._tsConstEnums ??= new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
                _parser._tsConstEnums[enumNode.Name] = values;
                return inList ? Remove : new AstEmptyStatement(enumNode.Source, enumNode.Start, enumNode.End);
            }

            var emitDeclaration = enumNode.EmitDeclaration && _currentDeclarations.Add(enumNode.Name);
            if (emitDeclaration)
                _currentEnumDeclarations.Add(enumNode.Name);
            var statements = _parser.TsEmitEnumStatements(enumNode.Name, enumNode.IsExport, enumNode.IsLocal,
                enumNode.Members, _parser._tsRuntimeEnumConstants, emitDeclaration, _currentConstValues);
            if (TsTryCollectRuntimeEnumConstants(enumNode.Name, enumNode.Members, _parser._tsRuntimeEnumConstants,
                    _currentConstValues, out var runtimeValues))
            {
                _parser._tsRuntimeEnumConstants ??=
                    new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
                _parser._tsRuntimeEnumConstants[enumNode.Name] = runtimeValues;
            }

            if (statements.Count == 1)
                return statements[0];
            var spread = new StructRefList<AstNode>();
            foreach (var statement in statements)
                spread.Add(statement);
            if (!inList)
                return new AstBlockStatement(enumNode.Source, enumNode.Start, enumNode.End, ref spread);
            return SpreadStructList(ref spread);
        }

        protected override AstNode? After(AstNode node, bool inList)
        {
            if (node is AstLambda or AstBlock)
            {
                _parser._tsRuntimeEnumConstants = _runtimeEnumScopes.Pop();
                _currentDeclarations.Clear();
                foreach (var name in _declarationScopes.Pop())
                    _currentDeclarations.Add(name);
                _currentEnumDeclarations.Clear();
                foreach (var name in _enumDeclarationScopes.Pop())
                    _currentEnumDeclarations.Add(name);
                _currentConstValues.Clear();
                foreach (var pair in _constScopes.Pop())
                    _currentConstValues.Add(pair.Key, pair.Value);
            }

            return null;
        }

        void RememberConstDefinitions(AstDefinitions definitions)
        {
            foreach (var definition in definitions.Definitions.AsReadOnlySpan())
            {
                if (definition.Name is not AstSymbol symbol)
                    continue;
                _currentConstValues.Remove(symbol.Name);
                if (definitions is not AstConst || definition.Value == null)
                    continue;
                var value = definition.Value.ConstValue();
                if (value == null)
                    continue;
                _currentConstValues[symbol.Name] = TypeConverter.ToAst(value);
            }
        }
    }

    bool TsTextStartsKeyword(int index, string keyword)
    {
        return TsTextStartsKeyword(_input, index, keyword);
    }

    static bool TsTextStartsKeyword(string input, int index, string keyword)
    {
        return index + keyword.Length <= input.Length &&
               input.AsSpan(index, keyword.Length).SequenceEqual(keyword.AsSpan()) &&
               (index + keyword.Length == input.Length || !IsIdentifierChar(input[index + keyword.Length]));
    }

    static bool TsTryEvaluateConstEnumMembers(List<AstTypeScriptEnumMember> members,
        out Dictionary<string, string> values)
    {
        return TsTryEvaluateEnumMembers(members, knownEnumConstants: null, out values);
    }

    static bool TsTryCollectRuntimeEnumConstants(string enumName, List<AstTypeScriptEnumMember> members,
        Dictionary<string, Dictionary<string, string>>? knownEnumConstants,
        Dictionary<string, AstNode>? knownConstValues,
        out Dictionary<string, string> values)
    {
        values = new Dictionary<string, string>(StringComparer.Ordinal);
        double? next = 0;
        var futureReferenceNames = TsFutureEnumReferenceNames(members);
        foreach (var member in members)
        {
            if (member.ReferenceName != null)
                futureReferenceNames.Remove(member.ReferenceName);
            if (member.ReferenceName == null)
            {
                next = null;
                continue;
            }

            if (member.Value == null)
            {
                if (next == null)
                    continue;
                values[member.ReferenceName] = TsFormatEnumNumber(next.Value);
                next++;
                continue;
            }

            var expression = member.ValueExpression?.DeepClone();
            if (expression == null)
            {
                next = null;
                continue;
            }
            if (!member.ForceReverseMap)
            {
                expression = TsReplaceKnownEnumMemberReferences(expression, knownEnumConstants, enumName);
                expression = TsReplaceKnownConstReferences(expression, knownConstValues);
                foreach (var pair in values)
                    expression = TsReplaceCurrentEnumMemberReferences(expression, enumName: null, pair.Key,
                        TsParseEnumInitializerExpression(pair.Value),
                        knownEnumConstants);
                expression = TsReplaceForwardEnumMemberReferences(expression, enumName: null, futureReferenceNames,
                    knownEnumConstants);
            }
            if (TsTryEvaluateStringExpression(expression, out var stringValue))
            {
                values[member.ReferenceName] = TsQuoteString(stringValue);
                next = null;
                continue;
            }

            if (!member.ForceReverseMap && TsTryEvaluateNumericExpression(expression, out var numeric))
            {
                values[member.ReferenceName] = TsFormatEnumNumber(numeric);
                next = numeric + 1;
                continue;
            }

            next = null;
        }

        return values.Count != 0;
    }

    static bool TsTryEvaluateEnumMembers(List<AstTypeScriptEnumMember> members,
        Dictionary<string, Dictionary<string, string>>? knownEnumConstants,
        out Dictionary<string, string> values)
    {
        values = new Dictionary<string, string>(StringComparer.Ordinal);
        double next = 0;
        var futureReferenceNames = TsFutureEnumReferenceNames(members);
        foreach (var member in members)
        {
            if (member.ReferenceName != null)
                futureReferenceNames.Remove(member.ReferenceName);
            if (member.ReferenceName == null)
                return false;
            if (member.Value == null)
            {
                values[member.ReferenceName] = TsFormatEnumNumber(next);
                next++;
                continue;
            }

            var expression = member.ValueExpression?.DeepClone();
            if (expression == null)
                return false;
            if (!member.ForceReverseMap)
            {
                if (TsContainsForwardEnumMemberReference(expression, futureReferenceNames))
                    return false;
                expression = TsReplaceKnownEnumMemberReferences(expression, knownEnumConstants);
                foreach (var pair in values)
                    expression = TsReplaceCurrentEnumMemberReferences(expression, enumName: null, pair.Key,
                        TsParseEnumInitializerExpression(pair.Value),
                        knownEnumConstants);
                expression = TsReplaceForwardEnumMemberReferences(expression, enumName: null, futureReferenceNames,
                    knownEnumConstants);
            }
            if (TsTryEvaluateStringExpression(expression, out var stringValue))
            {
                values[member.ReferenceName] = TsQuoteString(stringValue);
                continue;
            }

            if (!TsTryEvaluateNumericExpression(expression, out var numeric))
                return false;
            values[member.ReferenceName] = TsFormatEnumNumber(numeric);
            next = numeric + 1;
        }

        return true;
    }

    static HashSet<string> TsFutureEnumReferenceNames(List<AstTypeScriptEnumMember> members)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in members)
            if (member.ReferenceName != null)
                names.Add(member.ReferenceName);
        return names;
    }

    static string TsFormatEnumNumber(double value)
    {
        return value == 0d ? "0" : value.ToString(CultureInfo.InvariantCulture);
    }

    List<AstStatement> TsEmitEnumStatements(string name, bool isExport, bool local,
        List<AstTypeScriptEnumMember> members,
        Dictionary<string, Dictionary<string, string>>? knownEnumConstants = null, bool emitDeclaration = true,
        Dictionary<string, AstNode>? knownConstValues = null)
    {
        var statements = new List<AstStatement>();
        if (emitDeclaration)
        {
            var definitions = new StructRefList<AstVarDef>();
            definitions.Add(new AstVarDef(new AstSymbolVar(name)));
            AstStatement enumDeclaration = local && !isExport
                ? new AstLet(SourceFile, new Position(), new Position(), ref definitions)
                : new AstVar(ref definitions);
            if (isExport)
                enumDeclaration = new AstExport(enumDeclaration.Source, enumDeclaration.Start, enumDeclaration.End,
                    enumDeclaration, isDefault: false);
            statements.Add(enumDeclaration);
        }

        double? nextNumeric = 0d;
        var constantValues = new Dictionary<string, AstNode>(StringComparer.Ordinal);
        var referenceNames = new HashSet<string>(StringComparer.Ordinal);
        var stringValuedReferenceNames = new HashSet<string>(StringComparer.Ordinal);
        var futureReferenceNames = TsFutureEnumReferenceNames(members);
        var body = new StructRefList<AstNode>();
        foreach (var member in members)
        {
            if (member.ReferenceName != null)
                futureReferenceNames.Remove(member.ReferenceName);
            var value = member.Value;
            if (value == null)
            {
                AstNode valueExpression = nextNumeric != null ? TsEnumNumberNode(nextNumeric.Value) : TsVoidZero();
                if (nextNumeric != null)
                    nextNumeric++;
                if (member.ReferenceName != null)
                {
                    constantValues[member.ReferenceName] = valueExpression;
                    referenceNames.Add(member.ReferenceName);
                }
                body.Add(TsEnumReverseMapStatement(name, member, valueExpression.DeepClone()));
                continue;
            }

            var expression = member.ValueExpression?.DeepClone();
            if (expression == null)
                throw new InvalidOperationException("Enum member initializer expression was not parsed");
            if (!member.ForceReverseMap)
            {
                expression = TsReplaceKnownEnumMemberReferences(expression, knownEnumConstants, name);
                expression = TsReplaceKnownConstReferences(expression, knownConstValues);
                foreach (var pair in constantValues)
                    expression = TsReplaceCurrentEnumMemberReferences(expression, name, pair.Key,
                        pair.Value.DeepClone(), knownEnumConstants);
                expression = TsReplaceForwardEnumMemberReferences(expression, name, futureReferenceNames,
                    knownEnumConstants);
            }
            if (!member.ForceReverseMap && TsTryEvaluateNumericExpression(expression, out var numeric))
            {
                var valueExpression = TsEnumNumberNode(numeric);
                nextNumeric = numeric + 1;
                if (member.ReferenceName != null)
                {
                    constantValues[member.ReferenceName] = valueExpression;
                    referenceNames.Add(member.ReferenceName);
                }
                body.Add(TsEnumReverseMapStatement(name, member, valueExpression.DeepClone()));
            }
            else if (!member.ForceReverseMap && TsTryEvaluateStringExpression(expression, out var stringValue))
            {
                nextNumeric = null;
                var valueExpression = new AstString(stringValue);
                if (member.ReferenceName != null)
                {
                    constantValues[member.ReferenceName] = valueExpression;
                    referenceNames.Add(member.ReferenceName);
                }
                body.Add(TsEnumAssignmentStatement(name, member.KeyExpression.DeepClone(), valueExpression.DeepClone()));
            }
            else if (!member.ForceReverseMap &&
                     (TsIsStringValuedEnumExpression(expression) ||
                      TsReferencesStringValuedEnumMember(expression, name, stringValuedReferenceNames)))
            {
                nextNumeric = null;
                var valueExpression = TsQualifyEnumMemberReferences(
                    member.ValueExpression!, name,
                    TsReferenceNamesIncludingCurrent(referenceNames, member.ReferenceName),
                    member.ForceReverseMap);
                if (member.ReferenceName != null)
                {
                    referenceNames.Add(member.ReferenceName);
                    stringValuedReferenceNames.Add(member.ReferenceName);
                }
                body.Add(TsEnumAssignmentStatement(name, member.KeyExpression.DeepClone(), valueExpression));
            }
            else
            {
                nextNumeric = null;
                var valueExpression = TsQualifyEnumMemberReferences(
                    member.ValueExpression!, name,
                    TsReferenceNamesIncludingCurrent(referenceNames, member.ReferenceName),
                    member.ForceReverseMap);
                if (member.ReferenceName != null)
                    referenceNames.Add(member.ReferenceName);
                body.Add(TsEnumReverseMapStatement(name, member, valueExpression));
            }
        }

        var args = new StructList<AstNode>();
        args.Add(new AstBinary(SourceFile, new Position(), new Position(), new AstSymbolRef(name),
            new AstAssign(new AstSymbolRef(name), new AstObject(), Operator.Assignment),
            Operator.LogicalOr));
        var argNames = new StructList<AstNode>();
        argNames.Add(new AstSymbolFunarg(name));
        var function = new AstFunction(SourceFile, new Position(), new Position(), null, ref argNames,
            isGenerator: false, async: false, ref body);
        statements.Add(new AstSimpleStatement(new AstCall(SourceFile, new Position(), new Position(), function,
            ref args)));
        return statements;
    }

    static AstSimpleStatement TsEnumAssignmentStatement(string enumName, AstNode keyExpression, AstNode valueExpression)
    {
        return new AstSimpleStatement(new AstAssign(new AstSub(null, new Position(), new Position(),
            new AstSymbolRef(enumName), keyExpression), valueExpression, Operator.Assignment));
    }

    static AstSimpleStatement TsEnumReverseMapStatement(string enumName, AstTypeScriptEnumMember member, AstNode valueExpression)
    {
        var innerAssignment = new AstAssign(
            new AstSub(null, new Position(), new Position(), new AstSymbolRef(enumName),
                member.KeyExpression.DeepClone()),
            valueExpression, Operator.Assignment);
        return TsEnumAssignmentStatement(enumName, innerAssignment, member.ReverseNameExpression.DeepClone());
    }

    static AstNode TsVoidZero()
    {
        return new AstUnaryPrefix(Operator.Void, new AstNumber(0));
    }

    static AstNumber TsEnumNumberNode(double value)
    {
        return new AstNumber(value == 0d ? 0d : value);
    }

    static HashSet<string> TsReferenceNamesIncludingCurrent(HashSet<string> referenceNames, string? currentName)
    {
        if (currentName == null)
            return referenceNames;
        var result = new HashSet<string>(referenceNames, StringComparer.Ordinal);
        result.Add(currentName);
        return result;
    }

    static AstNode TsQualifyEnumMemberReferences(AstNode expression, string enumName, HashSet<string> referenceNames,
        bool preserveLiteralKeywords = false)
    {
        var replacements = new Dictionary<string, AstNode>(StringComparer.Ordinal);
        foreach (var referenceName in referenceNames)
        {
            if (!TsIsIdentifierText(referenceName))
                continue;
            if (preserveLiteralKeywords && TsIsNonReferenceEnumExpressionIdentifier(referenceName))
                continue;
            replacements[referenceName] = new AstDot(new AstSymbolRef(enumName), referenceName);
        }
        return replacements.Count == 0
            ? expression.DeepClone()
            : new TsEnumReferenceTransformer(null, replacements, includeBareReferences: true,
                includePrefixedReferences: false).Transform(expression.DeepClone());
    }

    static AstNode TsReplaceKnownEnumMemberReferences(AstNode expression,
        Dictionary<string, Dictionary<string, string>>? knownEnumConstants, string? currentEnumName = null)
    {
        if (knownEnumConstants == null)
            return expression;

        foreach (var enumPair in knownEnumConstants)
        {
            var replacements = TsParseConstantMap(enumPair.Value);
            expression = new TsEnumReferenceTransformer(enumPair.Key, replacements,
                includeBareReferences: enumPair.Key == currentEnumName,
                includePrefixedReferences: enumPair.Key == currentEnumName).Transform(expression);
        }

        return expression;
    }

    static AstNode TsReplaceKnownConstReferences(AstNode expression, Dictionary<string, AstNode>? knownConstValues)
    {
        if (knownConstValues == null || knownConstValues.Count == 0)
            return expression;
        return new TsEnumReferenceTransformer(null, knownConstValues,
            includeBareReferences: true, includePrefixedReferences: false).Transform(expression);
    }

    static AstNode TsReplaceCurrentEnumMemberReferences(AstNode expression, string? enumName, string referenceName,
        AstNode replacement, Dictionary<string, Dictionary<string, string>>? knownEnumConstants)
    {
        var replacements = new Dictionary<string, AstNode>(StringComparer.Ordinal) { [referenceName] = replacement };
        if (enumName != null)
            expression = new TsEnumReferenceTransformer(enumName, replacements, includeBareReferences: false,
                includePrefixedReferences: true).Transform(expression);
        if (knownEnumConstants != null && enumName != null)
        {
            foreach (var enumPair in knownEnumConstants)
            {
                if (enumPair.Key.EndsWith("." + enumName, StringComparison.Ordinal))
                    expression = new TsEnumReferenceTransformer(enumPair.Key, replacements,
                        includeBareReferences: false, includePrefixedReferences: false).Transform(expression);
            }
        }
        return new TsEnumReferenceTransformer(null, replacements, includeBareReferences: true,
            includePrefixedReferences: false).Transform(expression);
    }

    static AstNode TsReplaceForwardEnumMemberReferences(AstNode expression, string? enumName,
        HashSet<string> futureReferenceNames, Dictionary<string, Dictionary<string, string>>? knownEnumConstants)
    {
        if (expression is AstString)
            return expression;
        var zeroReplacements = new Dictionary<string, AstNode>(StringComparer.Ordinal);
        foreach (var referenceName in futureReferenceNames)
            zeroReplacements[referenceName] = new AstNumber(0);
        if (zeroReplacements.Count == 0)
            return expression;
        if (enumName != null)
            expression = new TsEnumReferenceTransformer(enumName, zeroReplacements, includeBareReferences: false,
                includePrefixedReferences: false).Transform(expression);
        if (knownEnumConstants != null && enumName != null)
        {
            foreach (var enumPair in knownEnumConstants)
            {
                if (enumPair.Key.EndsWith("." + enumName, StringComparison.Ordinal))
                    expression = new TsEnumReferenceTransformer(enumPair.Key, zeroReplacements,
                        includeBareReferences: false, includePrefixedReferences: false).Transform(expression);
            }
        }
        return new TsEnumReferenceTransformer(null, zeroReplacements, includeBareReferences: true,
            includePrefixedReferences: false).Transform(expression);
    }

    static Dictionary<string, AstNode> TsParseConstantMap(Dictionary<string, string> values)
    {
        var result = new Dictionary<string, AstNode>(StringComparer.Ordinal);
        foreach (var pair in values)
            result[pair.Key] = TsParseEnumInitializerExpression(pair.Value);
        return result;
    }

    static bool TsTryEvaluateNumericExpression(AstNode expression, out double value)
    {
        var constValue = expression.ConstValue();
        switch (constValue)
        {
            case double number:
                value = number;
                return true;
            case int number:
                value = number;
                return true;
            case uint number:
                value = number;
                return true;
            case long number:
                value = number;
                return true;
            case ulong number:
                value = number;
                return true;
        }
        value = 0;
        return false;
    }

    static bool TsTryEvaluateStringExpression(AstNode expression, out string value)
    {
        if (expression.ConstValue() is string stringValue)
        {
            value = stringValue;
            return true;
        }
        value = "";
        return false;
    }

    static bool TsContainsForwardEnumMemberReference(AstNode expression, HashSet<string> futureReferenceNames)
    {
        return new TsSymbolReferenceFinder(futureReferenceNames).HasReference(expression);
    }

    static bool TsIsStringValuedEnumExpression(AstNode expression)
    {
        return expression switch
        {
            AstString or AstTemplateString => true,
            AstBinary { Operator: Operator.Addition } binary =>
                TsIsStringValuedEnumExpression(binary.Left) || TsIsStringValuedEnumExpression(binary.Right),
            _ => false
        };
    }

    static bool TsReferencesStringValuedEnumMember(AstNode expression, string enumName,
        HashSet<string> stringValuedReferenceNames)
    {
        return new TsEnumReferenceFinder(enumName, stringValuedReferenceNames).HasReference(expression);
    }

    sealed class TsEnumReferenceTransformer : TreeTransformer
    {
        readonly string? _enumName;
        readonly Dictionary<string, AstNode> _replacements;
        readonly bool _includeBareReferences;
        readonly bool _includePrefixedReferences;

        public TsEnumReferenceTransformer(string? enumName, Dictionary<string, AstNode> replacements,
            bool includeBareReferences, bool includePrefixedReferences)
        {
            _enumName = enumName;
            _replacements = replacements;
            _includeBareReferences = includeBareReferences;
            _includePrefixedReferences = includePrefixedReferences;
        }

        protected override AstNode? Before(AstNode node, bool inList)
        {
            if (_includeBareReferences && node is AstSymbolRef { Name: var name } &&
                _replacements.TryGetValue(name, out var replacement))
                return replacement.DeepClone();
            if (_includeBareReferences && TsKeywordConstantReferenceName(node) is { } keywordName &&
                _replacements.TryGetValue(keywordName, out replacement))
                return replacement.DeepClone();

            if (_enumName != null && TryGetEnumMemberReference(node, _enumName, _includePrefixedReferences,
                    out var referenceName) &&
                _replacements.TryGetValue(referenceName, out replacement))
                return replacement.DeepClone();

            return null;
        }

        protected override AstNode? After(AstNode node, bool inList) => null;
    }

    static string? TsKeywordConstantReferenceName(AstNode node)
    {
        return node switch
        {
            AstTrue => "true",
            AstFalse => "false",
            AstNull => "null",
            _ => null
        };
    }

    sealed class TsSymbolReferenceFinder : TreeWalker
    {
        readonly HashSet<string> _names;
        bool _found;

        public TsSymbolReferenceFinder(HashSet<string> names)
        {
            _names = names;
        }

        public bool HasReference(AstNode node)
        {
            _found = false;
            Walk(node);
            return _found;
        }

        protected override void Visit(AstNode node)
        {
            if (node is AstSymbolRef { Name: var name } && _names.Contains(name))
            {
                _found = true;
                StopDescending();
                return;
            }
            Descend();
        }
    }

    sealed class TsStringLiteralFinder : TreeWalker
    {
        bool _found;

        public bool HasStringLiteral(AstNode node)
        {
            _found = false;
            Walk(node);
            return _found;
        }

        protected override void Visit(AstNode node)
        {
            if (node is AstString or AstTemplateString { Segments.Count: 1 })
            {
                _found = true;
                StopDescending();
                return;
            }
            Descend();
        }
    }

    sealed class TsEnumReferenceFinder : TreeWalker
    {
        readonly string _enumName;
        readonly HashSet<string> _referenceNames;
        bool _found;

        public TsEnumReferenceFinder(string enumName, HashSet<string> referenceNames)
        {
            _enumName = enumName;
            _referenceNames = referenceNames;
        }

        public bool HasReference(AstNode node)
        {
            _found = false;
            Walk(node);
            return _found;
        }

        protected override void Visit(AstNode node)
        {
            if (node is AstSymbolRef { Name: var name } && _referenceNames.Contains(name) ||
                TryGetEnumMemberReference(node, _enumName, includePrefixedReference: false, out var referenceName) &&
                _referenceNames.Contains(referenceName))
            {
                _found = true;
                StopDescending();
                return;
            }
            Descend();
        }
    }

    static bool TryGetEnumMemberReference(AstNode node, string enumName, bool includePrefixedReference,
        out string referenceName)
    {
        referenceName = "";
        AstNode target;
        switch (node)
        {
            case AstDot { PropertyAsString: var property, Expression: var expression }:
                if (property == null)
                    return false;
                referenceName = property;
                target = expression;
                break;
            case AstSub { Property: AstString { Value: var property }, Expression: var expression }:
                referenceName = property;
                target = expression;
                break;
            default:
                return false;
        }

        var path = TsAstDottedName(target);
        if (path == null)
            return false;
        if (target.Start.Index > node.Start.Index)
            return false;
        if (path == enumName || path == "globalThis." + enumName)
            return true;
        return includePrefixedReference && path.EndsWith("." + enumName, StringComparison.Ordinal);
    }

    static string? TsAstDottedName(AstNode node)
    {
        return node switch
        {
            AstSymbolRef { Name: var name } => name,
            AstDot { Expression: var expression, PropertyAsString: var property } when property != null =>
                TsAstDottedName(expression) is { } prefix ? prefix + "." + property : null,
            _ => null
        };
    }

    static bool TsIsIdentifierText(string value)
    {
        if (value.Length == 0 || !IsIdentifierStart(value[0], true))
            return false;
        for (var i = 1; i < value.Length; i++)
            if (!IsIdentifierChar(value[i], true))
                return false;
        return true;
    }

    static bool TsIsNonReferenceEnumExpressionIdentifier(string name)
    {
        return name is "true" or "false" or "null";
    }

    static string TsEscapeString(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default: builder.Append(ch); break;
            }
        }

        return builder.ToString();
    }

    static bool TsIsStringLiteral(string value)
    {
        value = value.TrimStart();
        return value.StartsWith("\"", StringComparison.Ordinal) || value.StartsWith("'", StringComparison.Ordinal);
    }

    static int TsSkipStringLike(string input, int index, char quote)
    {
        index++;
        while (index < input.Length)
        {
            if (input[index] == '\\')
            {
                index += 2;
                continue;
            }
            if (input[index] == quote)
                return index + 1;
            index++;
        }
        return index;
    }

    static string TsQuoteString(string value)
    {
        return "\"" + TsEscapeString(value) + "\"";
    }

    sealed class TypeScriptConstEnumInlineTransformer : TreeTransformer
    {
        readonly string? _sourceFile;
        readonly Dictionary<string, Dictionary<string, string>> _constEnums;

        public TypeScriptConstEnumInlineTransformer(string? sourceFile,
            Dictionary<string, Dictionary<string, string>> constEnums)
        {
            _sourceFile = sourceFile;
            _constEnums = constEnums;
        }

        protected override AstNode? Before(AstNode node, bool inList)
        {
            if (node is not AstDot { Expression: AstSymbolRef enumRef, Property: string memberName } dot)
                return null;
            if (!_constEnums.TryGetValue(enumRef.Name, out var members) ||
                !members.TryGetValue(memberName, out var value))
                return null;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return new AstNumber(_sourceFile, dot.Start, dot.End, number, value);
            if (TsIsStringLiteral(value))
                return new AstString(_sourceFile, dot.Start, dot.End, value.Trim()[1..^1]);
            return null;
        }

        protected override AstNode? After(AstNode node, bool inList)
        {
            return null;
        }
    }

    bool TsIsClassFollowing()
    {
        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        return index + 5 <= _input.Length && _input.AsSpan(index, 5).SequenceEqual("class".AsSpan()) &&
               (index + 5 == _input.Length || !IsIdentifierChar(_input.Get(index + 5)));
    }

    bool TsIsClassMemberModifier()
    {
        return IsTypeScript &&
               (Type == TokenType.Const ||
                Type == TokenType.Name &&
                (IsContextual("public") || IsContextual("private") || IsContextual("protected") ||
                 IsContextual("readonly") || IsContextual("override") || IsContextual("abstract") ||
                 IsContextual("declare")));
    }

    bool TsHasLineBreakAfterCurrentToken()
    {
        if (!IsTypeScript)
            return false;
        for (var index = End.Index; index < _input.Length; index++)
        {
            var ch = _input[index];
            if (ch == '\n' || ch == '\r')
                return true;
            if (ch != ' ' && ch != '\t' && ch != '\v' && ch != '\f')
                return false;
        }
        return false;
    }

    bool TsClassModifierLooksLikeMemberName()
    {
        if (!IsTypeScript || Type != TokenType.Name && Type != TokenType.Const)
            return false;
        var index = TsSkipWhitespaceAndComments(End.Index);
        return index >= _input.Length || _input[index] is '=' or ';' or ':' or '?' or '!';
    }

    bool TsStaticIsFollowedByClassMemberModifier()
    {
        if (!IsTypeScript || !IsContextual("static"))
            return false;

        var index = TsSkipWhitespaceAndComments(End.Index);
        return TsTextStartsKeyword(index, "const") || TsTextStartsKeyword(index, "readonly") ||
               TsTextStartsKeyword(index, "override") ||
               TsTextStartsKeyword(index, "public") || TsTextStartsKeyword(index, "private") ||
               TsTextStartsKeyword(index, "protected") || TsTextStartsKeyword(index, "accessor") ||
               TsTextStartsKeyword(index, "abstract") || TsTextStartsKeyword(index, "declare");
    }

    bool TsTrySkipParameterPropertyModifiers()
    {
        if (!IsTypeScript) return false;
        var skipped = false;
        var hasParameterPropertyModifier = false;
        while (Type == TokenType.Name &&
               (IsContextual("public") || IsContextual("private") || IsContextual("protected") ||
                IsContextual("readonly") || IsContextual("override") || IsContextual("accessor") ||
                IsContextual("declare") || IsContextual("static")))
        {
            var index = TsSkipWhitespaceAndComments(End.Index);
            if (index >= _input.Length ||
                TsTextStartsKeyword(index, "as") || TsTextStartsKeyword(index, "satisfies") ||
                _input[index] is ':' or '=' or ',' or ')' ||
                !IsIdentifierStart(_input[index], true) && _input[index] != '.' && _input[index] != '[' &&
                _input[index] != '{')
                break;
            if (!IsContextual("declare") && !IsContextual("static"))
                hasParameterPropertyModifier = true;
            skipped = true;
            Next();
        }

        return skipped && hasParameterPropertyModifier;
    }

    bool TsTrySkipStaticParameterModifier()
    {
        if (!IsTypeScript || !IsContextual("static") && Type != TokenType.Export)
            return false;

        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        if (index >= _input.Length || !IsIdentifierStart(_input[index], true) && _input[index] != '.')
            return false;
        if (_input[index] == '.')
        {
            if (index + 2 >= _input.Length || _input[index + 1] != '.' || _input[index + 2] != '.')
                return false;
            Next();
            return true;
        }
        var nameEnd = index + 1;
        while (nameEnd < _input.Length && IsIdentifierChar(_input[nameEnd], true))
            nameEnd++;
        nameEnd = TsSkipWhitespaceAndComments(nameEnd);
        if (nameEnd >= _input.Length || _input[nameEnd] != ':' && _input[nameEnd] != '=' &&
            _input[nameEnd] != ',' && _input[nameEnd] != ')')
            return false;

        Next();
        return true;
    }

    bool TsTrySkipObjectPropertyModifier()
    {
        if (!IsTypeScript || Type != TokenType.Name ||
            !(IsContextual("readonly") || IsContextual("public") || IsContextual("private") ||
              IsContextual("protected") || IsContextual("accessor") || IsContextual("declare") ||
              IsContextual("override") || IsContextual("abstract")))
            return false;

        var index = TsSkipWhitespaceAndComments(End.Index);
        if (index >= _input.Length || _input[index] != '*' &&
            !IsIdentifierStart(_input[index], true) && _input[index] != '[' &&
            _input[index] != '"' && _input[index] != '\'')
            return false;

        if (_input[index] == '*')
        {
            Next();
            return true;
        }

        var followingName = "";
        if (_input[index] == '[')
        {
            var close = TsFindMatchingSkippingLiterals(index, '[', ']');
            if (close < 0)
                return false;
            index = close + 1;
        }
        else if (_input[index] is '"' or '\'')
            index = TsSkipStringLike(index, _input[index]);
        else
        {
            var nameStart = index;
            index++;
            while (index < _input.Length && IsIdentifierChar(_input[index], true))
                index++;
            followingName = _input.Substring(nameStart, index - nameStart);
        }

        index = TsSkipWhitespaceAndComments(index);
        if (followingName is "async" or "get" or "set")
        {
            Next();
            return true;
        }

        if (index >= _input.Length || _input[index] is not (':' or '(' or '<' or '*'))
            return false;

        Next();
        return true;
    }

    AstSimpleStatement TsBuildParameterPropertyAssignment(AstSymbol parameter)
    {
        var thisNode = new AstThis(SourceFile, parameter.Start, parameter.End);
        var left = new AstDot(SourceFile, parameter.Start, parameter.End, thisNode, parameter.Name);
        var right = new AstSymbolRef(SourceFile, parameter.Start, parameter.End, parameter.Name);
        var assignment = new AstAssign(SourceFile, parameter.Start, parameter.End, left, right, Operator.Assignment);
        return new AstTypeScriptParameterPropertyAssignment(SourceFile, parameter.Start, parameter.End, assignment);
    }

    AstClassField TsBuildParameterPropertyField(AstSymbol parameter)
    {
        return new AstClassField(SourceFile, parameter.Start, parameter.End,
            new AstSymbolProperty(SourceFile, parameter.Start, parameter.End, parameter.Name), null, false);
    }

    AstSimpleStatement TsBuildStaticBlockStatement(Position start, Position end, ref StructRefList<AstNode> body)
    {
        var args = new StructList<AstNode>();
        var arrow = new AstArrow(SourceFile, start, end, null, ref args, false, false, ref body);
        var callArgs = new StructList<AstNode>();
        var call = new AstCall(SourceFile, start, end, arrow, ref callArgs);
        return new AstSimpleStatement(SourceFile, start, end, call);
    }

    bool TsTrySkipAbstractMember()
    {
        if (!IsTypeScript) return false;
        var index = Start.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        if (index >= _input.Length) return false;

        var startOfLine = index;
        while (index < _input.Length)
        {
            var ch = _input[index];
            if (ch == '{') return false;
            if (ch == ';')
            {
                while (Start.Index <= index && Type != TokenType.Eof) Next();
                Eat(TokenType.Semi);
                return true;
            }
            index++;
        }
        return false;
    }

    bool TsTryParseFunctionOverloadStatement(Position startLocation, out AstStatement typeOnlyStatement)
    {
        typeOnlyStatement = null!;
        if (!IsTypeScript) return false;
        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        if (index < _input.Length && _input[index] == '*')
            index++;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        if (index >= _input.Length || !IsIdentifierStart(_input[index], true)) return false;
        while (index < _input.Length && IsIdentifierChar(_input[index], true)) index++;
        index = TsSkipWhitespaceAndComments(index);
        if (index < _input.Length && _input[index] == '<')
        {
            var typeEnd = TsFindTypeArgumentListEnd(index);
            if (typeEnd < 0) return false;
            index = TsSkipWhitespaceAndComments(typeEnd + 1);
        }

        if (index >= _input.Length || _input[index] != '(') return false;
        var close = TsFindMatchingSkippingLiterals(index, '(', ')');
        if (close < 0) return false;
        var after = TsSkipWhitespaceAndComments(close + 1);
        var endedByLineBreak = false;
        if (after < _input.Length && _input[after] == ':')
        {
            var typeStart = after + 1;
            if (TsFunctionReturnTypeHasBodyBraceOnSameLine(typeStart))
                return false;
            after = TsFindDeclareFunctionReturnTypeEnd(after + 1);
            endedByLineBreak = after < _input.Length && _input[after] is '\n' or '\r';
            after = TsSkipWhitespaceAndComments(after);
        }
        if (after >= _input.Length || _input[after] != ';' && !endedByLineBreak) return false;

        if (endedByLineBreak)
            TsMoveToIndexAndReadToken(after);
        else
            TsSkipUntilStatementEnd();
        typeOnlyStatement = new AstTypeScriptOnly(SourceFile, startLocation, _lastTokEnd);
        return true;
    }

    bool TsFunctionReturnTypeHasBodyBraceOnSameLine(int index)
    {
        var angle = 0;
        var brace = 0;
        var paren = 0;
        var bracket = 0;
        var startedType = false;
        var lastSignificant = '\0';
        var lastWord = "";
        while (index < _input.Length)
        {
            var ch = _input[index];
            if (angle == 0 && brace == 0 && paren == 0 && bracket == 0 && ch == ';')
                return false;
            if (angle == 0 && brace == 0 && paren == 0 && bracket == 0 && ch is '\n' or '\r')
            {
                var next = TsSkipWhitespaceAndComments(index + 1);
                if (next >= _input.Length || _input[next] is not ('|' or '&') && TsNextLineStartsStatement(next))
                    return false;
            }
            var allowTypeLiteral = ch == '{' && (!startedType || lastSignificant is '|' or '&' || lastWord == "is");
            if (angle == 0 && brace == 0 && paren == 0 && bracket == 0 && ch == '{' && startedType &&
                !allowTypeLiteral)
                return true;
            if (ch is '"' or '\'')
            {
                index = TsSkipStringLike(index, ch);
                startedType = true;
                lastSignificant = ch;
                lastWord = "";
                continue;
            }
            if (ch == '`')
            {
                index = TsSkipTemplateLiteral(index);
                startedType = true;
                lastSignificant = ch;
                lastWord = "";
                continue;
            }
            if (IsIdentifierStart(ch, true))
            {
                var wordStart = index++;
                while (index < _input.Length && IsIdentifierChar(_input[index], true))
                    index++;
                lastSignificant = _input[index - 1];
                lastWord = _input.Substring(wordStart, index - wordStart);
                startedType = true;
                continue;
            }
            if (!char.IsWhiteSpace(ch))
            {
                startedType = true;
                lastSignificant = ch;
                lastWord = "";
            }
            switch (ch)
            {
                case '<': angle++; break;
                case '>': if (angle > 0) angle--; break;
                case '{': brace++; break;
                case '}': if (brace > 0) brace--; break;
                case '(': paren++; break;
                case ')': if (paren > 0) paren--; break;
                case '[': bracket++; break;
                case ']': if (bracket > 0) bracket--; break;
            }
            index++;
        }
        return false;
    }

    AstStatement TsParseDeclareStatement(Position startLocation)
    {
        Next();
        if (Type == TokenType.Export)
        {
            _tsErasedTypeOnlyModuleSyntaxUsed = true;
            Next();
        }
        if (Type == TokenType.Default)
            Next();
        if (IsContextual("abstract"))
            Next();
        if (Type == TokenType.Class)
        {
            TsSkipClassLikeDeclaration();
        }
        else if (Type == TokenType.Function)
        {
            TsSkipDeclareFunctionStatement();
        }
        else if (Type == TokenType.Const && TsConstIsFollowedByEnum())
        {
            TsSkipAmbientDeclarationBlock();
        }
        else if (Type is TokenType.Var or TokenType.Const || IsLet())
        {
            TsSkipDeclareVariableStatement();
        }
        else if (IsContextual("global") || IsContextual("namespace") || IsContextual("module") ||
                 IsContextual("enum") || IsContextual("interface"))
        {
            TsSkipAmbientDeclarationBlock();
        }
        else
        {
            TsSkipDeclareStatementFallback();
        }

        return new AstTypeScriptOnly(SourceFile, startLocation, _lastTokEnd);
    }

    void TsSkipDeclareFunctionStatement()
    {
        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        if (index < _input.Length && _input[index] == '*')
            index++;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        if (index >= _input.Length || !IsIdentifierStart(_input[index], true))
        {
            TsSkipDeclareStatementFallback();
            return;
        }

        index++;
        while (index < _input.Length && IsIdentifierChar(_input[index], true)) index++;
        index = TsSkipWhitespaceAndComments(index);
        if (index < _input.Length && _input[index] == '<')
        {
            var typeEnd = TsFindTypeArgumentListEnd(index);
            if (typeEnd < 0)
            {
                TsSkipDeclareStatementFallback();
                return;
            }
            index = TsSkipWhitespaceAndComments(typeEnd + 1);
        }

        if (index >= _input.Length || _input[index] != '(')
        {
            TsSkipDeclareStatementFallback();
            return;
        }

        var close = TsFindMatchingSkippingLiterals(index, '(', ')');
        if (close < 0)
        {
            TsSkipDeclareStatementFallback();
            return;
        }

        index = TsSkipWhitespaceAndComments(close + 1);
        if (index < _input.Length && _input[index] == ':')
            index = TsFindDeclareFunctionReturnTypeEnd(index + 1);
        index = TsSkipWhitespaceAndComments(index);
        TsMoveToIndexAndReadToken(index);
        Eat(TokenType.Semi);
    }

    int TsFindDeclareFunctionReturnTypeEnd(int index)
    {
        var angle = 0;
        var brace = 0;
        var bracket = 0;
        var paren = 0;
        while (index < _input.Length)
        {
            var ch = _input[index];
            if (ch is '"' or '\'')
            {
                index = TsSkipStringLike(index, ch);
                continue;
            }
            if (ch == '`')
            {
                index = TsSkipTemplateLiteral(index);
                continue;
            }
            if (ch == '/' && index + 1 < _input.Length)
            {
                if (_input[index + 1] == '/')
                {
                    while (index < _input.Length && _input[index] is not '\n' and not '\r') index++;
                    continue;
                }
                if (_input[index + 1] == '*')
                {
                    index += 2;
                    while (index + 1 < _input.Length && !(_input[index] == '*' && _input[index + 1] == '/')) index++;
                    index = Math.Min(index + 2, _input.Length);
                    continue;
                }
            }

            if (ch == '<') angle++;
            else if (ch == '>' && angle > 0) angle--;
            else if (ch == '{') brace++;
            else if (ch == '}' && brace > 0) brace--;
            else if (ch == '[') bracket++;
            else if (ch == ']' && bracket > 0) bracket--;
            else if (ch == '(') paren++;
            else if (ch == ')' && paren > 0) paren--;
            else if (angle == 0 && brace == 0 && bracket == 0 && paren == 0)
            {
                if (ch == ';')
                    return index;
                if (ch is '\n' or '\r' && TsNextLineStartsStatement(index + 1))
                    return index;
            }

            index++;
        }
        return index;
    }

    bool TsNextLineStartsStatement(int index)
    {
        while (index < _input.Length && _input[index] is ' ' or '\t' or '\v' or '\f' or '\n' or '\r')
            index++;
        return TsTextStartsKeyword(index, "function") ||
               TsTextStartsKeyword(index, "declare") ||
               TsTextStartsKeyword(index, "class") ||
               TsTextStartsKeyword(index, "interface") ||
               TsTextStartsKeyword(index, "type") ||
               TsTextStartsKeyword(index, "namespace") ||
               TsTextStartsKeyword(index, "module") ||
               TsTextStartsKeyword(index, "export") ||
               TsTextStartsKeyword(index, "import") ||
               TsTextStartsKeyword(index, "const") ||
               TsTextStartsKeyword(index, "let") ||
               TsTextStartsKeyword(index, "var") ||
               TsTextStartsKeyword(index, "async") ||
               TsIdentifierIsFollowedByCall(index);
    }

    bool TsIdentifierIsFollowedByCall(int index)
    {
        if (index >= _input.Length || !IsIdentifierStart(_input[index], true))
            return false;
        index++;
        while (index < _input.Length && IsIdentifierChar(_input[index], true))
            index++;
        index = TsSkipWhitespaceAndComments(index);
        return index < _input.Length && _input[index] == '(';
    }

    void TsSkipDeclareStatementFallback()
    {
        var end = TsFindStatementEndIndex(Start.Index, stopAtLineBreak: false);
        TsMoveToIndexAndReadToken(end);
        Eat(TokenType.Semi);
    }

    bool TsConstIsFollowedByEnum()
    {
        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        return TsTextStartsKeyword(index, "enum");
    }

    void TsSkipDeclareVariableStatement()
    {
        var end = TsFindDeclareVariableStatementEnd(Start.Index);
        TsMoveToIndexAndReadToken(end);
        Eat(TokenType.Semi);
    }

    int TsFindDeclareVariableStatementEnd(int index)
    {
        var brace = 0;
        var bracket = 0;
        var paren = 0;
        while (index < _input.Length)
        {
            var ch = _input[index];
            if (ch is '"' or '\'')
            {
                index = TsSkipStringLike(index, ch);
                continue;
            }
            if (ch == '`')
            {
                index = TsSkipTemplateLiteral(index);
                continue;
            }
            if (ch == '/' && index + 1 < _input.Length)
            {
                if (_input[index + 1] == '/')
                {
                    while (index < _input.Length && _input[index] is not '\n' and not '\r') index++;
                    continue;
                }
                if (_input[index + 1] == '*')
                {
                    index += 2;
                    while (index + 1 < _input.Length && !(_input[index] == '*' && _input[index + 1] == '/')) index++;
                    index = Math.Min(index + 2, _input.Length);
                    continue;
                }
            }

            if (ch == '{') brace++;
            else if (ch == '}' && brace > 0) brace--;
            else if (ch == '[') bracket++;
            else if (ch == ']' && bracket > 0) bracket--;
            else if (ch == '(') paren++;
            else if (ch == ')' && paren > 0) paren--;
            else if (brace == 0 && bracket == 0 && paren == 0)
            {
                if (ch == ';')
                    return index;
                if (ch is '\n' or '\r' && !TsDeclareVariableLineContinues(index))
                    return index;
            }

            index++;
        }
        return index;
    }

    bool TsDeclareVariableLineContinues(int newlineIndex)
    {
        var index = newlineIndex - 1;
        while (index >= 0 && _input[index] is ' ' or '\t' or '\v' or '\f' or '\n' or '\r')
            index--;
        if (index < 0)
            return false;
        var ch = _input[index];
        if (ch is ':' or '<' or '|' or '&' or ',' or '?' or '=')
            return true;
        if (ch == '>' && index > 0 && _input[index - 1] == '=')
            return true;
        index = newlineIndex + 1;
        while (index < _input.Length && _input[index] is ' ' or '\t' or '\v' or '\f' or '\n' or '\r')
            index++;
        if (index < _input.Length && _input[index] is '|' or '&' or '>' or '?' or ':' or ',' or ')')
            return true;
        return false;
    }

    void TsSkipAmbientDeclarationBlock()
    {
        while (Type != TokenType.BraceL && Type != TokenType.Semi && Type != TokenType.Eof) Next();
        if (Type == TokenType.BraceL)
        {
            var close = TsFindMatchingSkippingLiterals(Start.Index, '{', '}');
            if (close < 0) Raise(Start, "Unexpected token");
            TsMoveToIndexAndReadToken(close);
            Expect(TokenType.BraceR);
        }
        Eat(TokenType.Semi);
    }

    bool TsTrySkipFunctionOverloadSignature(Position startLocation)
    {
        if (!IsTypeScript) return false;
        var index = Start.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        if (index >= _input.Length || _input[index] != '(' && _input[index] != '<') return false;
        var paren = _input.IndexOf('(', index);
        if (paren < 0) return false;
        var close = TsFindMatchingSkippingLiterals(paren, '(', ')');
        if (close < 0) return false;
        var after = close + 1;
        while (after < _input.Length && char.IsWhiteSpace(_input[after])) after++;
        if (after < _input.Length && _input[after] == ':')
        {
            after++;
            var typeEnd = TsSkipTypeInText(after);
            after = typeEnd;
            while (after < _input.Length && char.IsWhiteSpace(_input[after])) after++;
        }

        if (after >= _input.Length || _input[after] != ';') return false;
        while (Start.Index <= after && Type != TokenType.Eof) Next();
        Eat(TokenType.Semi);
        return true;
    }

    void TsSkipAbstractClassMemberSignature()
    {
        var index = Start.Index;
        while (index < _input.Length && _input[index] != '(' && _input[index] != ':' && _input[index] != ';' &&
               _input[index] != '\n' && _input[index] != '\r')
            index++;
        if (index < _input.Length && _input[index] == '(')
        {
            var close = TsFindMatchingSkippingLiterals(index, '(', ')');
            if (close >= 0)
                index = close + 1;
        }
        index = TsSkipWhitespaceAndComments(index);
        if (index < _input.Length && _input[index] == ':')
            index = TsSkipTypeInText(index + 1);
        index = TsSkipWhitespaceAndComments(index);
        while (index < _input.Length && _input[index] != ';' && _input[index] != '\n' && _input[index] != '\r' &&
               _input[index] != '{')
            index++;
        if (index < _input.Length && _input[index] == ';')
            index++;
        TsMoveToIndexAndReadToken(index);
    }

    bool TsTrySkipClassMethodOverloadSignature()
    {
        if (!IsTypeScript || Type != TokenType.ParenL) return false;
        var paren = Start.Index;
        var close = TsFindMatchingSkippingLiterals(paren, '(', ')');
        if (close < 0) return false;
        var after = close + 1;
        while (after < _input.Length && char.IsWhiteSpace(_input[after])) after++;
        if (after < _input.Length && _input[after] == ':')
        {
            after = TsSkipTypeInText(after + 1);
            if (after < 0) return false;
            while (after < _input.Length && char.IsWhiteSpace(_input[after])) after++;
        }
        if (after >= _input.Length || _input[after] != ';') return false;
        while (Type != TokenType.Eof && Start.Index <= after) Next();
        Eat(TokenType.Semi);
        return true;
    }

    bool TsTrySkipObjectMethodSignature()
    {
        if (!IsTypeScript)
            return false;
        var index = Start.Index;
        if (Type == TokenType.Name || TokenInformation.Types[Type].Keyword != null)
            index = End.Index;
        else if (Type is TokenType.Num or TokenType.String)
            index = End.Index;
        else if (Type == TokenType.BracketL)
        {
            var closeBracket = TsFindMatchingSkippingLiterals(Start.Index, '[', ']');
            if (closeBracket < 0)
                return false;
            index = closeBracket + 1;
        }
        else
            return false;

        index = TsSkipWhitespaceAndComments(index);
        if (index >= _input.Length || _input[index] != '(')
            return false;
        var close = TsFindMatchingSkippingLiterals(index, '(', ')');
        if (close < 0)
            return false;
        var after = TsSkipWhitespaceAndComments(close + 1);
        if (after < _input.Length && _input[after] == ':')
            after = TsSkipWhitespaceAndComments(TsSkipTypeInText(after + 1));
        if (after >= _input.Length || _input[after] != ';')
            return false;
        TsMoveToIndexAndReadToken(after + 1);
        return true;
    }

    bool TsTrySkipClassIndexSignature()
    {
        if (!IsTypeScript)
            return false;

        var bracket = Start.Index;
        if (Type != TokenType.BracketL)
        {
            if (!IsContextual("static"))
                return false;
            bracket = TsSkipWhitespaceAndComments(End.Index);
            if (bracket >= _input.Length || _input[bracket] != '[')
                return false;
        }

        var close = TsFindMatchingSkippingLiterals(bracket, '[', ']');
        if (close < 0)
            return false;

        var colonInBrackets = false;
        for (var index = bracket + 1; index < close; index++)
        {
            if (_input[index] == ':')
            {
                colonInBrackets = true;
                break;
            }
        }
        var restIndexSignature = false;
        if (!colonInBrackets)
        {
            var content = TsSkipWhitespaceAndComments(bracket + 1);
            restIndexSignature = content + 2 < close && _input[content] == '.' && _input[content + 1] == '.' &&
                                 _input[content + 2] == '.';
            if (!restIndexSignature)
                return false;
        }

        var after = TsSkipWhitespaceAndComments(close + 1);
        if (after < _input.Length && _input[after] == ';' && colonInBrackets)
        {
            TsMoveToIndexAndReadToken(after + 1);
            return true;
        }
        if (after < _input.Length && _input[after] == '}' && colonInBrackets)
        {
            TsMoveToIndexAndReadToken(after);
            return true;
        }
        if (after >= _input.Length || _input[after] != ':')
            return false;

        after = TsSkipWhitespaceAndComments(TsSkipTypeInText(after + 1));
        if (after < _input.Length && _input[after] == '}')
        {
            TsMoveToIndexAndReadToken(after);
            return true;
        }
        if (after < _input.Length && _input[after] == ';')
            after++;

        TsMoveToIndexAndReadToken(after);
        return true;
    }

    bool TsTryParseAccessorOverloadSignature(Position startLocation, AstNode key, PropertyKind kind, bool isStatic,
        ref StructRefList<AstNode> classBody)
    {
        if (!IsTypeScript || Type != TokenType.ParenL)
            return false;
        var paren = Start.Index;
        var close = TsFindMatchingSkippingLiterals(paren, '(', ')');
        if (close < 0)
            return false;
        var after = close + 1;
        while (after < _input.Length && char.IsWhiteSpace(_input[after])) after++;
        if (after < _input.Length && _input[after] == ':')
        {
            after = TsSkipTypeInText(after + 1);
            if (after < 0)
                return false;
            while (after < _input.Length && char.IsWhiteSpace(_input[after])) after++;
        }
        if (after >= _input.Length || _input[after] != ';')
            return false;

        Expect(TokenType.ParenL);
        var parameters = new StructList<AstNode>();
        ParseBindingList(ref parameters, TokenType.ParenR, false, Options.EcmaVersion >= 8);
        TsTrySkipTypeAnnotation();
        MakeSymbolFunArg(ref parameters);
        Expect(TokenType.Semi);

        var emptyBody = new StructRefList<AstNode>();
        var method = new AstFunction(SourceFile, startLocation, _lastTokEnd, null, ref parameters, false, false,
            ref emptyBody);
        if (kind == PropertyKind.Get)
            classBody.Add(new AstObjectGetter(SourceFile, startLocation, _lastTokEnd, key, method, isStatic));
        else
            classBody.Add(new AstObjectSetter(SourceFile, startLocation, _lastTokEnd, key, method, isStatic));
        return true;
    }

    bool TsTryParseDeclareAccessorMember(Position methodStart, ref StructRefList<AstNode> classBody)
    {
        if (!IsTypeScript)
            return false;
        var isStatic = false;
        if (Type == TokenType.Name && "static".Equals(Value) && TsStaticModifierIsFollowedByClassElementName())
        {
            isStatic = true;
            Next();
        }

        if (!IsContextual("get") && !IsContextual("set"))
            return false;
        var kind = IsContextual("get") ? PropertyKind.Get : PropertyKind.Set;
        Next();
        var property = ParsePropertyName();
        if (kind == PropertyKind.Get && Type == TokenType.Colon)
        {
            TsTrySkipTypeAnnotation();
            Expect(TokenType.Semi);
            var parameters = new StructList<AstNode>();
            var emptyBody = new StructRefList<AstNode>();
            var method = new AstFunction(SourceFile, methodStart, _lastTokEnd, null, ref parameters, false, false,
                ref emptyBody);
            classBody.Add(new AstObjectGetter(SourceFile, methodStart, _lastTokEnd, property.key, method, isStatic));
            return true;
        }
        if (Type != TokenType.ParenL)
            return false;
        return TsTryParseAccessorOverloadSignature(methodStart, property.key, kind, isStatic, ref classBody);
    }

    bool TsDeclareMemberStartsAccessor()
    {
        if (!IsTypeScript || !IsContextual("declare"))
            return false;
        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        if (TsTextStartsKeyword(index, "static"))
        {
            index += "static".Length;
            while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        }
        if (!TsTextStartsKeyword(index, "get") && !TsTextStartsKeyword(index, "set"))
            return false;
        index += 3;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        if (index >= _input.Length || !IsIdentifierStart(_input[index], true) && _input[index] != '"' &&
            _input[index] != '\'' && _input[index] != '[' && _input[index] != '#' && !char.IsDigit(_input[index]))
            return false;
        while (index < _input.Length && _input[index] != '(' && _input[index] != ':' && _input[index] != ';' &&
               _input[index] != '\n' && _input[index] != '\r')
            index++;
        return index < _input.Length && (_input[index] == '(' || _input[index] == ':');
    }

    void TsTrySkipTypeParameters(bool expressionAllowedAfter = false)
    {
        if (!TsStartsTypeArgumentListToken()) return;
        var end = TsFindTypeArgumentListEnd(Start.Index);
        if (end >= 0)
            TsMoveToIndexAndReadToken(end + 1, expressionAllowedAfter);
        else
            TsSkipBalancedTokenType(TokenType.Relational, "<", ">");
    }

    bool TsStartsTypeArgumentListToken()
    {
        return IsTypeScript &&
               (Type == TokenType.Relational && "<".Equals(Value) ||
                Type == TokenType.BitShift && Value is string bitShift && bitShift.StartsWith("<"));
    }

    void TsTrySkipTypeAnnotation()
    {
        if (!IsTypeScript || !Eat(TokenType.Colon)) return;
        var typeStart = Start;
        TsSkipType();
        _tsCanInsertSemicolonAfterSkippedType = Type != TokenType.Eq &&
                                               Type != TokenType.Comma &&
                                               Type != TokenType.Semi &&
                                               LineBreak.IsMatch(_input.Substring(typeStart.Index,
                                                   Math.Max(0, Start.Index - typeStart.Index)));
    }

    void TsTrySkipClassFieldTypeAnnotation()
    {
        if (!IsTypeScript || !Eat(TokenType.Colon)) return;
        TsSkipType(stopAtClassFieldBoundary: true);
    }

    bool TsTypeAnnotationIsFollowedByArrow()
    {
        if (!IsTypeScript || Type != TokenType.Colon)
            return false;
        var afterType = TsSkipWhitespaceAndComments(TsSkipTypeInText(End.Index));
        return afterType + 1 < _input.Length && _input[afterType] == '=' && _input[afterType + 1] == '>';
    }

    void TsTrySkipArrowReturnTypeAnnotation()
    {
        if (!IsTypeScript || !Eat(TokenType.Colon))
            return;
        TsSkipType(stopAtExpressionBodyArrow: true);
    }

    bool TsTypeAnnotationStartsWithConditionalAlternate()
    {
        if (!IsTypeScript || Type != TokenType.Colon)
            return false;
        var index = TsSkipWhitespaceAndComments(End.Index);
        if (index >= _input.Length)
            return false;
        return _input[index] is '(' or '<' or '"' or '\'' or '`' or '[' or '{' ||
               char.IsDigit(_input[index]);
    }

    void TsTrySkipForBindingTypeAnnotation()
    {
        if (!IsTypeScript || !Eat(TokenType.Colon)) return;
        TsSkipType(stopAtForInOf: true);
    }

    void TsSkipHeritageClause()
    {
        Next();
        while (Type != TokenType.BraceL && Type != TokenType.Eof)
        {
            TsSkipType();
            if (Type == TokenType.Comma)
            {
                Next();
                continue;
            }

            if (Type != TokenType.BraceL)
                Next();
        }
    }


    bool TsTypeSpecifierIsTypeOnly()
    {
        if (!IsTypeScript || !IsContextual("type"))
            return false;

        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        if (TsTextStartsKeyword(index, "as"))
        {
            index += "as".Length;
            while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
            return index >= _input.Length || _input[index] == ',' || _input[index] == '}' ||
                   TsTextStartsKeyword(index, "as");
        }

        return !TsTextStartsKeyword(index, "as") && index < _input.Length && _input[index] != ',' &&
               _input[index] != '}';
    }

    void TsTrySkipOptionalOrDefiniteBindingMarker()
    {
        if (!IsTypeScript) return;
        if ((Type == TokenType.Question || Type == TokenType.Prefix && "!".Equals(Value)) &&
            TsCanSkipOptionalOrDefiniteMarker())
            Next();
    }

    bool TsCanSkipOptionalOrDefiniteMarker()
    {
        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        if (index >= _input.Length) return true;
        return _input[index] is ':' or '=' or ',' or ')' or ';';
    }

    bool TsShouldProbeSingleParameterArrow()
    {
        if (!IsTypeScript) return true;

        var index = Start.Index;
        if (Type == TokenType.Question || Type == TokenType.Prefix && "!".Equals(Value))
        {
            index = End.Index;
            while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        }

        if (index >= _input.Length) return false;
        if (_input[index] == ':')
        {
            index = TsSkipTypeInText(index + 1);
            while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        }

        return index + 1 < _input.Length && _input[index] == '=' && _input[index + 1] == '>';
    }

    bool TsCanParseAsyncGenericArrow()
    {
        if (Type != TokenType.Relational || !"<".Equals(Value))
            return false;

        var typeEnd = TsFindTypeArgumentListEnd(Start.Index);
        if (typeEnd < 0)
            return false;
        var parenStart = TsSkipWhitespaceAndComments(typeEnd + 1);
        if (parenStart >= _input.Length || _input[parenStart] != '(')
            return false;
        var parenEnd = TsFindMatchingSkippingLiterals(parenStart, '(', ')');
        if (parenEnd < 0)
            return false;
        var after = TsSkipWhitespaceAndComments(parenEnd + 1);
        if (after < _input.Length && _input[after] == ':')
            after = TsSkipWhitespaceAndComments(TsSkipTypeInText(after + 1));
        return after + 1 < _input.Length && _input[after] == '=' && _input[after + 1] == '>';
    }

    bool TsCanParseGenericArrow()
    {
        if (Type != TokenType.Relational || !"<".Equals(Value))
            return false;

        var typeEnd = TsFindTypeArgumentListEnd(Start.Index);
        if (typeEnd < 0)
            return false;
        var parenStart = TsSkipWhitespaceAndComments(typeEnd + 1);
        if (parenStart >= _input.Length || _input[parenStart] != '(')
            return false;
        var parenEnd = TsFindMatchingSkippingLiterals(parenStart, '(', ')');
        if (parenEnd < 0)
            return false;
        var after = TsSkipWhitespaceAndComments(parenEnd + 1);
        if (after < _input.Length && _input[after] == ':')
            after = TsSkipWhitespaceAndComments(TsSkipTypeInText(after + 1));
        return after + 1 < _input.Length && _input[after] == '=' && _input[after + 1] == '>';
    }

    int TsFindTypeArgumentListEnd(int index)
    {
        if (index >= _input.Length || _input[index] != '<')
            return -1;

        var angle = 0;
        var brace = 0;
        var paren = 0;
        var bracket = 0;
        for (var i = index; i < _input.Length; i++)
        {
            var ch = _input[i];
            if (ch is '"' or '\'' or '`')
            {
                i = TsSkipStringLike(i, ch) - 1;
                continue;
            }

            switch (ch)
            {
                case '<':
                    angle++;
                    break;
                case '>':
                    if (i > 0 && _input[i - 1] == '=')
                        break;
                    angle--;
                    if (brace == 0 && paren == 0 && bracket == 0 && angle == 0)
                        return i;
                    break;
                case '{':
                    brace++;
                    break;
                case '}':
                    if (brace > 0)
                        brace--;
                    else if (angle == 1)
                        return -1;
                    break;
                case '(':
                    paren++;
                    break;
                case ')':
                    if (paren > 0)
                        paren--;
                    else if (angle == 1)
                        return -1;
                    break;
                case '[':
                    bracket++;
                    break;
                case ']':
                    if (bracket > 0)
                        bracket--;
                    else if (angle == 1)
                        return -1;
                    break;
                case ';':
                    if (angle > 0 && brace == 0 && paren == 0 && bracket == 0)
                        return -1;
                    break;
            }
        }

        return -1;
    }

    bool TsCanFollowTypeArgumentsInExpression(int nextIndex)
    {
        if (nextIndex >= _input.Length || _input[nextIndex] is '\n' or '\r')
            return true;
        var ch = _input[nextIndex];
        if (ch is '(' or '`')
            return true;
        if (ch is '<' or '>' or '+' or '-')
            return false;
        return TsTokenAtCanFollowTypeArgumentsInExpression(nextIndex);
    }

    bool TsTokenAtCanFollowTypeArgumentsInExpression(int index)
    {
        var tokenStart = Start;
        var tokenEnd = End;
        var tokenType = Type;
        var tokenValue = Value;
        var lastTokStart = _lastTokStart;
        var lastTokEnd = _lastTokEnd;
        var pos = _pos;
        var containsEsc = _containsEsc;
        var exprAllowed = _exprAllowed;
        var inTemplateElement = _inTemplateElement;
        var context = _context.ToArray();
        try
        {
            _pos = MoveToIndex(index);
            NextToken();
            if (Type is TokenType.Relational && Value is "<" or ">" ||
                Type is TokenType.PlusMin && Value is "+" or "-")
                return false;
            return CanInsertSemicolon() || TsIsExpressionOperatorAfterType() ||
                   IsContextual("as") || IsContextual("satisfies") ||
                   Type is TokenType.Instanceof or TokenType.In ||
                   !TokenInformation.Types[Type].StartsExpression;
        }
        finally
        {
            Start = tokenStart;
            End = tokenEnd;
            Type = tokenType;
            Value = tokenValue;
            _lastTokStart = lastTokStart;
            _lastTokEnd = lastTokEnd;
            _pos = pos;
            _containsEsc = containsEsc;
            _exprAllowed = exprAllowed;
            _inTemplateElement = inTemplateElement;
            _context.Clear();
            _context.AddRange(context);
        }
    }

    bool TsStaticModifierIsFollowedByClassElementName()
    {
        if (!IsTypeScript) return false;
        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        if (index >= _input.Length) return false;
        return _input[index] == '[' || IsIdentifierStart(_input[index], true) || _input[index] == '"' ||
               _input[index] == '\'' || char.IsDigit(_input[index]);
    }

    bool TsStaticModifierIsFollowedByAccessor()
    {
        if (!IsTypeScript || Type != TokenType.Name || !"static".Equals(Value))
            return false;
        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        return TsTextStartsKeyword(index, "accessor");
    }

    bool TsClassModifierSequenceStartsAutoAccessor()
    {
        if (!IsTypeScript || Type != TokenType.Name)
            return false;

        var index = Start.Index;
        var sawModifier = false;
        while (index < _input.Length)
        {
            index = TsSkipWhitespaceAndComments(index);
            if (TsTextStartsKeyword(index, "accessor"))
                return TsAccessorKeywordIsFollowedByClassElementName(index + "accessor".Length);

            var length = TsModifierKeywordLength(index);
            if (length == 0)
                return false;

            sawModifier = true;
            index += length;
        }

        return sawModifier;
    }

    int TsModifierKeywordLength(int index)
    {
        foreach (var keyword in new[] { "static", "public", "private", "protected", "readonly", "override" })
        {
            if (TsTextStartsKeyword(index, keyword))
                return keyword.Length;
        }

        return 0;
    }

    bool TsAccessorKeywordIsFollowedByClassElementName(int index)
    {
        index = TsSkipWhitespaceAndComments(index);
        if (index >= _input.Length) return false;
        return _input[index] == '[' || _input[index] == '#' || IsIdentifierStart(_input[index], true) ||
               _input[index] == '"' || _input[index] == '\'' || char.IsDigit(_input[index]);
    }

    bool TsTryParseAutoAccessor(AstSymbol? className, ref StructRefList<AstNode> classBody,
        List<AstNode>? decorators, List<AstStatement> memberDecoratorStatements,
        List<AstStatement> instanceFieldInitializerStatements, bool hasStaticTsModifier = false)
    {
        if (!IsTypeScript)
            return false;

        var startLocation = Start;
        var isStatic = hasStaticTsModifier;
        if (IsContextual("accessor"))
        {
            if (!TsAccessorKeywordIsFollowedByClassElementName(End.Index))
                return false;
        }
        else if (!TsClassModifierSequenceStartsAutoAccessor())
            return false;

        while (!IsContextual("accessor"))
        {
            if (IsContextual("static"))
                isStatic = true;
            Next();
        }

        Next();

        var anonymousStaticClassName = className == null &&
                                       (_tsDefaultExportClassName == null ||
                                        _tsForceAnonymousStaticAccessorNameForDefaultExportClass)
            ? _tsAnonymousClassStaticAccessorName
            : null;
        if (isStatic && className == null &&
            (_tsDefaultExportClassName == null || _tsForceAnonymousStaticAccessorNameForDefaultExportClass))
            anonymousStaticClassName = TsEnsureAnonymousClassStaticAccessorName(startLocation);

        var property = ParsePropertyName();
        var key = property.key;
        var getterKey = key;
        var setterKey = key;
        var storageKey = key;
        var decoratorKey = key;
        if (property.computed)
        {
            if (key is AstString or AstNumber or AstTemplateString { Segments.Count: 1 })
            {
                getterKey = new AstComputedPropertyKey(SourceFile, key.Start, key.End, key);
                setterKey = getterKey;
            }
            else
            {
                var temp = TsNewAutoAccessorTemp();
                TsAddAutoAccessorTempDeclaration(startLocation, temp);
                getterKey = new AstAssign(SourceFile, key.Start, key.End,
                    new AstSymbolRef(SourceFile, key.Start, key.End, temp), key, Operator.Assignment);
                setterKey = new AstSymbolRef(SourceFile, key.Start, key.End, temp);
                decoratorKey = setterKey;
            }
            storageKey = key;
        }
        TsTrySkipOptionalOrDefiniteBindingMarker();
        TsTrySkipTypeAnnotation();

        AstNode? fieldValue = null;
        if (Eat(TokenType.Eq))
            fieldValue = ParseMaybeAssign(Start);
        Eat(TokenType.Semi);

        var storageName = TsAutoAccessorStorageName(storageKey);
        var privateStorageKey = new AstSymbolPrivate(SourceFile, key.Start, key.End, storageName);
        var lowerDecoratedInstanceInitializer = !isStatic && decorators is { Count: > 0 } &&
                                                decoratorKey is not AstSymbolPrivate && fieldValue != null;
        classBody.Add(new AstClassField(SourceFile, startLocation, _lastTokEnd, privateStorageKey,
            lowerDecoratedInstanceInitializer ? null : fieldValue, isStatic));
        if (lowerDecoratedInstanceInitializer)
            instanceFieldInitializerStatements.Add(TsBuildClassFieldInitializerStatement(
                new AstThis(SourceFile, key.Start, key.End), privateStorageKey, fieldValue!, false));
        classBody.Add(TsBuildAutoAccessorGetter(startLocation, getterKey, storageName, isStatic, className));
        classBody.Add(TsBuildAutoAccessorSetter(startLocation, setterKey, storageName, isStatic, className));
        if (decorators is { Count: > 0 } && decoratorKey is not AstSymbolPrivate)
        {
            var decoratorClassName = className?.Name ?? _tsDefaultExportClassName;
            if (decoratorClassName != null)
            {
                if (className == null)
                    _tsDefaultExportClassNameUsed = true;
                memberDecoratorStatements.Add(TsBuildMemberDecorateStatement(decorators, decoratorClassName,
                    decoratorKey, isStatic, false, property.computed));
            }
        }
        return true;
    }

    void TsInsertPendingClassComputedKeyStatements(ref StructRefList<AstNode> body)
    {
        if (_tsPendingClassComputedKeyStatements == null)
            return;
        var insertIndex = TsDirectivePrefixLength(body);
        for (var i = 0; i < _tsPendingClassComputedKeyStatements.Count; i++)
        {
            var pendingStatement = _tsPendingClassComputedKeyStatements[i];
            if (pendingStatement.TargetBlockDepth != _tsBlockDepth ||
                pendingStatement.VarScopeDepth != _tsVarScopeDepth)
                continue;
            body.Insert(insertIndex++, pendingStatement.Statement);
            _tsPendingClassComputedKeyStatements.RemoveAt(i);
            i--;
        }
        if (_tsPendingClassComputedKeyStatements.Count == 0)
            _tsPendingClassComputedKeyStatements = null;
    }

    static int TsDirectivePrefixLength(StructRefList<AstNode> body)
    {
        var index = 0;
        while (index < body.Count && body[(uint)index] is AstSimpleStatement
               {
                   Body: AstString
               })
        {
            index++;
        }
        return index;
    }

    string TsNewAutoAccessorTemp()
    {
        return TsAutoAccessorTempName(_tsAutoAccessorTempIndex++);
    }

    static string TsAutoAccessorTempName(int index)
    {
        return index < 26
            ? "_" + (char)('a' + index)
            : "_tmp" + index.ToString(CultureInfo.InvariantCulture);
    }

    void TsAddAutoAccessorTempDeclaration(Position position, string temp)
    {
        const int targetBlockDepth = 0;
        var targetVarScopeDepth = _tsVarScopeDepth;
        if (_tsPendingClassComputedKeyStatements != null)
        {
            foreach (var pending in _tsPendingClassComputedKeyStatements)
            {
                if (pending.TargetBlockDepth == targetBlockDepth &&
                    pending.VarScopeDepth == targetVarScopeDepth &&
                    pending.Statement is AstVar pendingVar)
                {
                    pendingVar.Definitions.Add(new AstVarDef(SourceFile, position, position,
                        new AstSymbolVar(SourceFile, position, position, temp, null)));
                    return;
                }
            }
        }

        var definitions = new StructRefList<AstVarDef>();
        definitions.Add(new AstVarDef(SourceFile, position, position,
            new AstSymbolVar(SourceFile, position, position, temp, null)));
        var statement = new AstVar(SourceFile, position, position, ref definitions);
        _tsPendingClassComputedKeyStatements ??=
            new List<(AstStatement Statement, int TargetBlockDepth, int VarScopeDepth)>();
        _tsPendingClassComputedKeyStatements.Add((statement, targetBlockDepth, targetVarScopeDepth));
    }

    (AstNode ClassKey, AstNode DecoratorKey) TsPrepareDecoratedComputedClassKey(AstNode key)
    {
        var temp = TsNewAutoAccessorTemp();
        TsAddAutoAccessorTempDeclaration(key.Start, temp);
        var tempRef = new AstSymbolRef(SourceFile, key.Start, key.End, temp);
        AstNode classKey = new AstAssign(SourceFile, key.Start, key.End, tempRef, key, Operator.Assignment);
        if (_tsPendingDecoratedComputedClassKeyAssignments is { Count: > 0 })
        {
            var expressions = new StructList<AstNode>();
            foreach (var assignment in _tsPendingDecoratedComputedClassKeyAssignments)
                expressions.Add(assignment);
            expressions.Add(classKey);
            classKey = new AstSequence(SourceFile, key.Start, key.End, ref expressions);
            _tsPendingDecoratedComputedClassKeyAssignments = null;
        }
        var decoratorKey = new AstSymbolRef(SourceFile, key.Start, key.End, temp);
        return (classKey, decoratorKey);
    }

    string TsEnsureAnonymousClassStaticAccessorName(Position position)
    {
        if (_tsAnonymousClassStaticAccessorName != null)
            return _tsAnonymousClassStaticAccessorName;

        var temp = TsNewAutoAccessorTemp();
        TsAddAutoAccessorTempDeclaration(position, temp);
        _tsAnonymousClassStaticAccessorName = temp;
        return temp;
    }

    bool TsClassBodyContainsStaticAutoAccessor()
    {
        var index = Start.Index;
        var braceDepth = 0;
        while (index < _input.Length)
        {
            var ch = _input[index];
            if (char.IsWhiteSpace(ch))
            {
                index++;
                continue;
            }

            if (ch == '/' && index + 1 < _input.Length)
            {
                if (_input[index + 1] == '/')
                {
                    index += 2;
                    while (index < _input.Length && _input[index] is not '\n' and not '\r') index++;
                    continue;
                }
                if (_input[index + 1] == '*')
                {
                    index += 2;
                    while (index + 1 < _input.Length && !(_input[index] == '*' && _input[index + 1] == '/')) index++;
                    index = Math.Min(index + 2, _input.Length);
                    continue;
                }
            }

            if (ch is '"' or '\'' or '`')
            {
                index = TsSkipStringLike(index, ch);
                continue;
            }

            if (ch == '{')
            {
                braceDepth++;
                index++;
                continue;
            }

            if (ch == '}')
            {
                if (braceDepth == 0)
                    return false;
                braceDepth--;
                index++;
                continue;
            }

            if (braceDepth == 0 && TsTextStartsKeyword(index, "static"))
            {
                var next = TsSkipWhitespaceAndComments(index + "static".Length);
                if (TsTextStartsKeyword(next, "accessor"))
                    return true;
            }

            index++;
        }

        return false;
    }

    bool TsNextDecoratedDefaultExportClassIsAnonymous()
    {
        if (Type != TokenType.Export)
            return false;
        var index = TsSkipWhitespaceAndComments(End.Index);
        if (!TsTextStartsKeyword(index, "default"))
            return false;
        index = TsSkipWhitespaceAndComments(index + "default".Length);
        if (!TsTextStartsKeyword(index, "class"))
            return false;
        index = TsSkipWhitespaceAndComments(index + "class".Length);
        if (index >= _input.Length)
            return false;
        return _input[index] == '{' || TsTextStartsKeyword(index, "extends");
    }

    int TsSkipStringLike(int index, char quote)
    {
        index++;
        while (index < _input.Length)
        {
            if (_input[index] == '\\')
            {
                index += 2;
                continue;
            }
            if (_input[index] == quote)
                return index + 1;
            index++;
        }
        return index;
    }

    int TsSkipWhitespaceAndComments(int index)
    {
        while (index < _input.Length)
        {
            if (char.IsWhiteSpace(_input[index]))
            {
                index++;
                continue;
            }
            if (_input[index] == '/' && index + 1 < _input.Length)
            {
                if (_input[index + 1] == '/')
                {
                    index += 2;
                    while (index < _input.Length && _input[index] is not '\n' and not '\r') index++;
                    continue;
                }
                if (_input[index + 1] == '*')
                {
                    index += 2;
                    while (index + 1 < _input.Length && !(_input[index] == '*' && _input[index + 1] == '/')) index++;
                    index = Math.Min(index + 2, _input.Length);
                    continue;
                }
            }
            break;
        }
        return index;
    }

    string TsAutoAccessorStorageName(AstNode key)
    {
        return key switch
        {
            AstSymbolRef => TsAutoAccessorTempName(_tsAutoAccessorStorageTempIndex++) + "_accessor_storage",
            AstSymbol symbol => symbol.Name + "_accessor_storage",
            AstString or AstNumber => TsAutoAccessorTempName(_tsAutoAccessorStorageTempIndex++) + "_accessor_storage",
            _ => TsAutoAccessorTempName(_tsAutoAccessorStorageTempIndex++) + "_accessor_storage"
        };
    }

    AstObjectGetter TsBuildAutoAccessorGetter(Position startLocation, AstNode key, string storageName, bool isStatic,
        AstSymbol? className)
    {
        var args = new StructList<AstNode>();
        var body = new StructRefList<AstNode>();
        body.Add(new AstReturn(SourceFile, startLocation, _lastTokEnd,
            TsBuildAutoAccessorStorageRef(startLocation, storageName, isStatic, className)));
        var method = new AstAccessor(SourceFile, startLocation, _lastTokEnd, ref args, false, false, ref body);
        return new AstObjectGetter(SourceFile, startLocation, _lastTokEnd, key, method, isStatic);
    }

    AstObjectSetter TsBuildAutoAccessorSetter(Position startLocation, AstNode key, string storageName, bool isStatic,
        AstSymbol? className)
    {
        var args = new StructList<AstNode>();
        args.Add(new AstSymbolFunarg(new AstSymbolRef(SourceFile, startLocation, _lastTokEnd, "value")));
        var body = new StructRefList<AstNode>();
        var assign = new AstAssign(SourceFile, startLocation, _lastTokEnd,
            TsBuildAutoAccessorStorageRef(startLocation, storageName, isStatic, className),
            new AstSymbolRef(SourceFile, startLocation, _lastTokEnd, "value"), Operator.Assignment);
        body.Add(new AstSimpleStatement(SourceFile, startLocation, _lastTokEnd, assign));
        var method = new AstAccessor(SourceFile, startLocation, _lastTokEnd, ref args, false, false, ref body);
        return new AstObjectSetter(SourceFile, startLocation, _lastTokEnd, key, method, isStatic);
    }

    AstDot TsBuildAutoAccessorStorageRef(Position startLocation, string storageName, bool isStatic,
        AstSymbol? className)
    {
        AstNode target;
        if (isStatic)
        {
            if (className == null)
            {
                var defaultClassName = _tsDefaultExportClassName;
                if (defaultClassName != null && !_tsForceAnonymousStaticAccessorNameForDefaultExportClass)
                {
                    _tsDefaultExportClassNameUsed = true;
                    target = new AstSymbolRef(SourceFile, startLocation, _lastTokEnd, defaultClassName);
                    return new AstDot(SourceFile, startLocation, _lastTokEnd, target, "#" + storageName);
                }

                var anonymousClassName = TsEnsureAnonymousClassStaticAccessorName(startLocation);
                target = new AstSymbolRef(SourceFile, startLocation, _lastTokEnd, anonymousClassName);
                return new AstDot(SourceFile, startLocation, _lastTokEnd, target, "#" + storageName);
            }
            target = new AstSymbolRef(SourceFile, startLocation, _lastTokEnd, className!.Name);
        }
        else
        {
            target = new AstThis(SourceFile, startLocation, _lastTokEnd);
        }

        return new AstDot(SourceFile, startLocation, _lastTokEnd, target, "#" + storageName);
    }

    bool TsStaticModifierIsFollowedByGetSetAccessor()
    {
        if (!IsTypeScript || Type != TokenType.Name || !"static".Equals(Value))
            return false;
        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        return TsTextStartsKeyword(index, "get") || TsTextStartsKeyword(index, "set");
    }

    bool TsDefaultIsFollowedByClass()
    {
        if (!IsTypeScript || Type != TokenType.Default && !IsContextual("default"))
            return false;
        var index = TsSkipWhitespaceAndComments(End.Index);
        return TsTextStartsKeyword(index, "class");
    }

    bool TsDefaultIsFollowedByFunction()
    {
        if (!IsTypeScript || Type != TokenType.Default && !IsContextual("default"))
            return false;
        var index = TsSkipWhitespaceAndComments(End.Index);
        if (TsTextStartsKeyword(index, "function"))
            return true;
        if (!TsTextStartsKeyword(index, "async"))
            return false;
        index = TsSkipWhitespaceAndComments(index + "async".Length);
        return TsTextStartsKeyword(index, "function");
    }

    string TsNextInvalidDefaultExportName()
    {
        _tsInvalidDefaultExportNameIndex++;
        return "default_" + _tsInvalidDefaultExportNameIndex.ToString(CultureInfo.InvariantCulture);
    }

    void TsSkipTypeDeclaration()
    {
        if (IsContextual("interface"))
        {
            Next();
            var angle = 0; var paren = 0; var bracket = 0;
            while (Type != TokenType.Eof)
            {
                if (Type == TokenType.BraceL && angle == 0 && paren == 0 && bracket == 0) break;
                if (Type == TokenType.Relational && "<".Equals(Value)) angle++;
                else if (Type == TokenType.Relational && ">".Equals(Value) && angle > 0) angle--;
                else if (Type == TokenType.BitShift && angle > 0)
                {
                    var value = Value?.ToString();
                    if (value == ">>") angle = Math.Max(0, angle - 2);
                    else if (value == ">>>") angle = Math.Max(0, angle - 3);
                }
                else if (Type == TokenType.ParenL) paren++;
                else if (Type == TokenType.ParenR && paren > 0) paren--;
                else if (Type == TokenType.BracketL) bracket++;
                else if (Type == TokenType.BracketR && bracket > 0) bracket--;
                Next();
            }
            if (Type == TokenType.BraceL)
            {
                var close = TsFindMatchingSkippingLiterals(Start.Index, '{', '}');
                if (close < 0) Raise(Start, "Unexpected token");
                TsMoveToIndexAndReadToken(close);
                Expect(TokenType.BraceR);
            }
            Eat(TokenType.Semi);
            return;
        }
        if (IsContextual("type"))
        {
            var typeAliasEnd = TsFindTypeAliasStatementEndIndex(Start.Index);
            if (typeAliasEnd >= 0)
            {
                TsMoveToIndexAndReadToken(typeAliasEnd);
                Eat(TokenType.Semi);
                return;
            }
        }
        var end = TsFindStatementEndIndex(Start.Index, stopAtLineBreak: false);
        while (Type != TokenType.Eof && _lastTokEnd.Index < end)
            Next();
        Eat(TokenType.Semi);
    }

    int TsFindTypeAliasStatementEndIndex(int start)
    {
        var index = start;
        var angle = 0;
        var brace = 0;
        var bracket = 0;
        var paren = 0;
        var foundEquals = false;
        while (index < _input.Length)
        {
            var ch = _input[index];
            if (ch is '"' or '\'')
            {
                index = TsSkipStringLike(index, ch);
                continue;
            }
            if (ch == '`')
            {
                index = TsSkipTemplateLiteral(index);
                continue;
            }
            if (ch == '/' && index + 1 < _input.Length)
            {
                if (_input[index + 1] == '/')
                {
                    index += 2;
                    while (index < _input.Length && _input[index] is not '\n' and not '\r') index++;
                    continue;
                }
                if (_input[index + 1] == '*')
                {
                    index += 2;
                    while (index + 1 < _input.Length && !(_input[index] == '*' && _input[index + 1] == '/')) index++;
                    index = Math.Min(index + 2, _input.Length);
                    continue;
                }
                if (TsCanStartRegexLiteralAt(index))
                {
                    index = TsSkipRegexLiteral(index);
                    continue;
                }
            }

            if (ch == '<') angle++;
            else if (ch == '>' && angle > 0) angle--;
            else if (ch == '{') brace++;
            else if (ch == '}' && brace > 0) brace--;
            else if (ch == '[') bracket++;
            else if (ch == ']' && bracket > 0) bracket--;
            else if (ch == '(') paren++;
            else if (ch == ')' && paren > 0) paren--;
            else if (ch == '=' && angle == 0 && brace == 0 && bracket == 0 && paren == 0)
                foundEquals = true;
            else if (ch == ';' && angle == 0 && brace == 0 && bracket == 0 && paren == 0)
                return index;
            else if (foundEquals && (ch == '\n' || ch == '\r') &&
                     angle == 0 && brace == 0 && bracket == 0 && paren == 0)
            {
                var after = TsSkipWhitespaceAndComments(index);
                if (TsTextStartsStatementAfterTypeAlias(after))
                    return index;
            }
            index++;
        }
        return foundEquals ? index : -1;
    }

    bool TsTextStartsStatementAfterTypeAlias(int index)
    {
        return TsTextStartsKeyword(index, "abstract") ||
               TsTextStartsKeyword(index, "class") ||
               TsTextStartsKeyword(index, "const") ||
               TsTextStartsKeyword(index, "declare") ||
               TsTextStartsKeyword(index, "enum") ||
               TsTextStartsKeyword(index, "export") ||
               TsTextStartsKeyword(index, "function") ||
               TsTextStartsKeyword(index, "import") ||
               TsTextStartsKeyword(index, "interface") ||
               TsTextStartsKeyword(index, "let") ||
               TsTextStartsKeyword(index, "module") ||
               TsTextStartsKeyword(index, "namespace") ||
               TsTextStartsKeyword(index, "type") ||
               TsTextStartsKeyword(index, "var");
    }

    void TsSkipClassLikeDeclaration()
    {
        var angle = 0;
        var paren = 0;
        var bracket = 0;
        while (Type != TokenType.Eof)
        {
            if (Type == TokenType.Semi && angle == 0 && paren == 0 && bracket == 0)
                break;
            if (Type == TokenType.BraceL && angle == 0 && paren == 0 && bracket == 0)
                break;
            if (Type == TokenType.Relational && "<".Equals(Value)) angle++;
            else if (Type == TokenType.Relational && ">".Equals(Value) && angle > 0) angle--;
            else if (Type == TokenType.BitShift && angle > 0)
            {
                var value = Value?.ToString();
                if (value == ">>") angle = Math.Max(0, angle - 2);
                else if (value == ">>>") angle = Math.Max(0, angle - 3);
            }
            else if (Type == TokenType.ParenL) paren++;
            else if (Type == TokenType.ParenR && paren > 0) paren--;
            else if (Type == TokenType.BracketL) bracket++;
            else if (Type == TokenType.BracketR && bracket > 0) bracket--;
            Next();
        }
        if (Type == TokenType.BraceL) TsSkipBalancedTokenType(TokenType.BraceL, "{", "}");
        Eat(TokenType.Semi);
    }

    void TsSkipUntilStatementEnd()
    {
        var brace = 0;
        var paren = 0;
        var bracket = 0;
        while (Type != TokenType.Eof)
        {
            if (Type == TokenType.Semi && brace == 0 && paren == 0 && bracket == 0)
            {
                Next();
                return;
            }

            if (Type == TokenType.BraceL) brace++;
            else if (Type == TokenType.BraceR)
            {
                if (brace == 0) return;
                brace--;
            }
            else if (Type == TokenType.ParenL) paren++;
            else if (Type == TokenType.ParenR)
            {
                if (paren == 0) return;
                paren--;
            }
            else if (Type == TokenType.BracketL) bracket++;
            else if (Type == TokenType.BracketR)
            {
                if (bracket == 0) return;
                bracket--;
            }

            Next();
        }
    }

    void TsSkipType(bool stopAtExpressionOperators = false, bool stopAtForInOf = false,
        bool stopAtClassFieldBoundary = false, bool stopAtExpressionBodyArrow = false)
    {
        var angle = 0;
        var brace = 0;
        var paren = 0;
        var bracket = 0;
        var startedType = false;
        var lastWasTypePredicateIs = false;
        var lastWasPipeOrAmp = false;
        var justClosedParen = false;
        var justClosedBracket = false;
        var justHadArrowAfterParen = false;
        var sawTopLevelExtends = false;
        var lastWasExtends = false;
        var lastWasConditionalQuestion = false;
        var lastWasConditionalColon = false;
        var conditionalTypeDepth = 0;
        var justClosedTopLevelTypeLiteral = false;
        while (Type != TokenType.Eof)
        {
            if (justClosedTopLevelTypeLiteral && angle == 0 && brace == 0 && paren == 0 && bracket == 0 &&
                Start.Line > _lastTokEnd.Line && TsIsTypeBoundaryAfterTypeLiteral())
                return;
            justClosedTopLevelTypeLiteral = false;
            if (startedType && angle == 0 && brace == 0 && paren == 0 && bracket == 0 &&
                Start.Line > _lastTokEnd.Line && TsIsTypeBoundaryAfterTypeLiteral())
                return;
            if (stopAtExpressionOperators && Type == TokenType.Semi && angle > 0)
                return;
            if (stopAtClassFieldBoundary && startedType && angle == 0 && brace == 0 && paren == 0 && bracket == 0 &&
                Start.Line > _lastTokEnd.Line && (TsIsClassMemberModifier() || TsLooksLikeClassMemberStart()))
                return;
            var allowTypeLiteral = Type == TokenType.BraceL && (!startedType || lastWasTypePredicateIs ||
                lastWasPipeOrAmp || justHadArrowAfterParen || lastWasExtends || lastWasConditionalQuestion ||
                lastWasConditionalColon);
            if (startedType && Type == TokenType.BraceL && !allowTypeLiteral && justClosedBracket &&
                angle > 0 && brace == 0 && paren == 0 && bracket == 0)
                return;
            if (angle == 0 && brace == 0 && paren == 0 && bracket == 0 &&
                (Type == TokenType.Comma || Type == TokenType.ParenR ||
                 (Type == TokenType.BraceL && startedType && !allowTypeLiteral) ||
                 Type == TokenType.BraceR || Type == TokenType.Eq || Type == TokenType.Semi ||
                 (stopAtExpressionOperators && startedType && Type == TokenType.Colon &&
                  conditionalTypeDepth == 0) ||
                 (Type == TokenType.Question && !sawTopLevelExtends &&
                  (stopAtExpressionOperators && TsQuestionLooksLikeConditionalExpression() || !IsTypeScript)) ||
                 (stopAtExpressionOperators && startedType && TsIsExpressionOperatorAfterType()) ||
                 (stopAtExpressionOperators && startedType && TsIsRelationalExpressionOperatorAfterType()) ||
                 (stopAtForInOf && startedType && (Type == TokenType.In || IsContextual("of"))) ||
                 (Type == TokenType.Arrow &&
                  (!justClosedParen || stopAtExpressionBodyArrow && TsArrowLooksLikeExpressionBody())) ||
                 (startedType && (IsContextual("as") || IsContextual("satisfies")))))
                return;

            var isArrow = Type == TokenType.Arrow;
            if (isArrow && justClosedParen)
                justHadArrowAfterParen = true;
            else
                justHadArrowAfterParen = false;
            justClosedParen = false;
            justClosedBracket = false;
            startedType = true;
            lastWasTypePredicateIs = IsContextual("is");
            lastWasPipeOrAmp = Type is TokenType.BitwiseOr or TokenType.BitwiseAnd;
            lastWasExtends = Type == TokenType.Extends || IsContextual("extends");
            lastWasConditionalQuestion = false;
            lastWasConditionalColon = false;
            if (angle == 0 && brace == 0 && paren == 0 && bracket == 0 &&
                (Type == TokenType.Extends || IsContextual("extends")))
                sawTopLevelExtends = true;
            else if (angle == 0 && brace == 0 && paren == 0 && bracket == 0 && Type == TokenType.Question &&
                     sawTopLevelExtends)
            {
                conditionalTypeDepth++;
                sawTopLevelExtends = false;
                lastWasConditionalQuestion = true;
            }
            else if (angle == 0 && brace == 0 && paren == 0 && bracket == 0 && Type == TokenType.Colon &&
                     conditionalTypeDepth > 0)
            {
                conditionalTypeDepth--;
                lastWasConditionalColon = true;
            }
            if (Type == TokenType.Relational && "<".Equals(Value)) angle++;
            else if (Type == TokenType.Relational && ">".Equals(Value) && angle > 0) angle--;
            else if (Type == TokenType.BackQuote)
            {
                TsMoveToIndex(TsSkipTemplateLiteral(Start.Index));
                startedType = true;
                continue;
            }
            else if (Type == TokenType.BitShift && angle > 0)
            {
                var value = Value?.ToString();
                if (value == ">>") angle = Math.Max(0, angle - 2);
                else if (value == ">>>") angle = Math.Max(0, angle - 3);
            }
            else if (Type == TokenType.BraceL) brace++;
            else if (Type == TokenType.BraceR)
            {
                if (brace == 0) return;
                brace--;
                if (brace == 0 && angle == 0 && paren == 0 && bracket == 0)
                    justClosedTopLevelTypeLiteral = true;
            }
            else if (Type == TokenType.ParenL) paren++;
            else if (Type == TokenType.ParenR)
            {
                if (paren == 0) return;
                paren--;
                if (paren == 0) justClosedParen = true;
            }
            else if (Type == TokenType.BracketL) bracket++;
            else if (Type == TokenType.BracketR)
            {
                if (bracket == 0) return;
                bracket--;
                if (bracket == 0) justClosedBracket = true;
            }

            Next();
        }
    }

    void TsSkipErasedAssertionType()
    {
        if (Type == TokenType.Const)
        {
            Next();
            return;
        }

        TsSkipType(stopAtExpressionOperators: true);
    }

    bool TsArrowLooksLikeExpressionBody()
    {
        if (Type != TokenType.Arrow)
            return false;
        var index = TsSkipWhitespaceAndComments(End.Index);
        if (index >= _input.Length)
            return false;
        var ch = _input[index];
        if (ch is '"' or '\'' or '`' or '(' or '[' or '{' || char.IsDigit(ch))
            return true;
        return TsTextStartsKeyword(index, "async") ||
               TsTextStartsKeyword(index, "class") ||
               TsTextStartsKeyword(index, "function") ||
               TsTextStartsKeyword(index, "new") ||
               TsTextStartsKeyword(index, "null") ||
               TsTextStartsKeyword(index, "this") ||
               TsTextStartsKeyword(index, "true") ||
               TsTextStartsKeyword(index, "false") ||
               TsTextStartsKeyword(index, "undefined");
    }

    bool TsIsTypeBoundaryAfterTypeLiteral()
    {
        return Type is TokenType.Var or TokenType.Const or TokenType.Function or TokenType.Class or TokenType.Export
                   or TokenType.Import or TokenType.If or TokenType.For or TokenType.While or TokenType.Switch
                   or TokenType.Return or TokenType.Throw or TokenType.Try or TokenType.Do or TokenType.Eof ||
               IsContextual("interface") || IsContextual("type") || IsContextual("namespace") ||
               IsContextual("module") || IsContextual("declare") || IsContextual("enum") ||
               IsContextual("abstract") || IsContextual("let");
    }

    bool TsIsExpressionOperatorAfterType()
    {
        return Type is TokenType.PlusMin or TokenType.Star or TokenType.Slash or TokenType.Modulo or TokenType.Starstar
            or TokenType.BitShift or TokenType.Equality or TokenType.LogicalOr or TokenType.LogicalAnd
            or TokenType.NullishCoalescing or TokenType.Instanceof or TokenType.In;
    }

    bool TsLooksLikeClassMemberStart()
    {
        if (!IsTypeScript)
            return false;
        if (Type is TokenType.String or TokenType.Num or TokenType.BracketL or TokenType.PrivateName)
            return true;
        if (Type != TokenType.Name && TokenInformation.Types[Type].Keyword == null)
            return false;
        var index = TsSkipWhitespaceAndComments(End.Index);
        return index < _input.Length && _input[index] is '(' or ':' or '?' or '!' or '=' or ';' or '<';
    }

    bool TsIsRelationalExpressionOperatorAfterType()
    {
        if (Type != TokenType.Relational)
            return false;
        var value = Value?.ToString();
        if (value is ">" or ">=" or "<=")
            return true;
        if (value != "<")
            return false;
        if (Start.Index > _lastTokEnd.Index)
            return true;
        return TsFindTypeArgumentListEnd(Start.Index) < 0;
    }

    bool TsQuestionLooksLikeConditionalExpression()
    {
        if (!IsTypeScript || Type != TokenType.Question)
            return false;
        var index = TsSkipWhitespaceAndComments(End.Index);
        var paren = 0;
        var bracket = 0;
        var brace = 0;
        while (index < _input.Length)
        {
            var ch = _input[index];
            if (ch is ';' or '\r' or '\n' && paren == 0 && bracket == 0 && brace == 0)
                return false;
            if (paren == 0 && bracket == 0 && brace == 0 && ch == ':')
                return true;
            if (ch is '"' or '\'')
            {
                index = TsSkipStringLike(index, ch);
                continue;
            }
            if (ch == '`')
            {
                index = TsSkipTemplateLiteral(index);
                continue;
            }
            switch (ch)
            {
                case '(':
                    paren++;
                    break;
                case ')':
                    if (paren == 0) return false;
                    paren--;
                    break;
                case '[':
                    bracket++;
                    break;
                case ']':
                    if (bracket == 0) return false;
                    bracket--;
                    break;
                case '{':
                    brace++;
                    break;
                case '}':
                    if (brace == 0) return false;
                    brace--;
                    break;
            }
            index++;
        }
        return false;
    }

    bool TsRelationalTokenLooksLikeSkippedTypeClose()
    {
        if (!IsTypeScript || Type != TokenType.Relational || !">".Equals(Value))
            return false;
        var index = TsSkipWhitespaceAndComments(End.Index);
        return index < _input.Length && _input[index] is ')' or ']' or ',' or ';' or '{';
    }

    void TsSkipBalancedTokenType(TokenType openType, string openValue, string closeValue)
    {
        var depth = 0;
        var angle = 0;
        var paren = 0;
        var bracket = 0;
        var firstLine = Start.Line + 1;
        while (Type != TokenType.Eof)
        {
            if ((Type == openType && openValue.Equals(Value)) ||
                (Type == TokenType.BraceL && openType == TokenType.BraceL) ||
                (Type == TokenType.ParenL && openType == TokenType.ParenL) ||
                (Type == TokenType.BracketL && openType == TokenType.BracketL))
            {
                depth++;
            }
            else if ((Type == TokenType.Relational && closeValue.Equals(Value)) ||
                     (Type == TokenType.BraceR && closeValue == "}") ||
                     (Type == TokenType.ParenR && closeValue == ")") ||
                     (Type == TokenType.BracketR && closeValue == "]"))
            {
                depth--;
                Next();
                if (depth <= 0 && angle == 0 && paren == 0 && bracket == 0) return;
                continue;
            }
            else if (closeValue == ">" && (Type == TokenType.Relational || Type == TokenType.BitShift))
            {
                var val = (string)Value!;
                if (val == ">>")
                {
                    depth -= 2;
                    Next();
                    if (depth <= 0 && angle == 0 && paren == 0 && bracket == 0) return;
                    continue;
                }
                if (val == ">>>")
                {
                    depth -= 3;
                    Next();
                    if (depth <= 0 && angle == 0 && paren == 0 && bracket == 0) return;
                    continue;
                }
                if (val == "<<" && (openType == TokenType.Relational && openValue == "<"))
                {
                    depth += 2;
                    Next();
                    continue;
                }
                if (val == ">=" || val == ">" || val == "<")
                {
                    if (val != "<") depth--;
                    else if (openType == TokenType.Relational && openValue == "<") depth++;
                    Next();
                    if (depth <= 0 && angle == 0 && paren == 0 && bracket == 0) return;
                    continue;
                }
            }
            else if (Type == TokenType.Relational && "<".Equals(Value))
            {
                angle++;
            }
            else if (Type == TokenType.Relational && ">".Equals(Value) && angle > 0)
            {
                angle--;
            }
            else if (Type == TokenType.ParenL)
            {
                paren++;
            }
            else if (Type == TokenType.ParenR && paren > 0)
            {
                paren--;
            }
            else if (Type == TokenType.BracketL)
            {
                bracket++;
            }
            else if (Type == TokenType.BracketR && bracket > 0)
            {
                bracket--;
            }

            Next();
        }
    }

    int TsSkipTypeInText(int index)
    {
        var angle = 0;
        var brace = 0;
        var paren = 0;
        var bracket = 0;
        var startedType = false;
        var lastSignificant = '\0';
        var lastWord = "";
        while (index < _input.Length)
        {
            var ch = _input[index];
            var allowTypeLiteral = ch == '{' && (!startedType || lastSignificant is '|' or '&' || lastWord == "is");
            if (angle == 0 && brace == 0 && paren == 0 && bracket == 0 &&
                (ch == ';' || ch == '=' || ch == '}' || (ch == '{' && startedType && !allowTypeLiteral)))
                return index;
            if (IsIdentifierStart(ch, true))
            {
                var wordStart = index;
                index++;
                while (index < _input.Length && IsIdentifierChar(_input[index], true))
                    index++;
                lastSignificant = _input[index - 1];
                lastWord = _input.Substring(wordStart, index - wordStart);
                startedType = true;
                continue;
            }
            if (!char.IsWhiteSpace(ch))
            {
                startedType = true;
                lastSignificant = ch;
                lastWord = "";
            }
            if (ch is '"' or '\'')
            {
                index = TsSkipStringLike(index, ch);
                continue;
            }
            if (ch == '`')
            {
                index = TsSkipTemplateLiteral(index);
                continue;
            }
            if (ch == '/' && index + 1 < _input.Length)
            {
                if (_input[index + 1] == '/')
                {
                    index += 2;
                    while (index < _input.Length && _input[index] is not '\r' and not '\n')
                        index++;
                    continue;
                }
                if (_input[index + 1] == '*')
                {
                    index += 2;
                    while (index + 1 < _input.Length && !(_input[index] == '*' && _input[index + 1] == '/'))
                        index++;
                    index = Math.Min(index + 2, _input.Length);
                    continue;
                }
                if (TsCanStartRegexLiteralAt(index))
                {
                    index = TsSkipRegexLiteral(index);
                    continue;
                }
            }
            switch (ch)
            {
                case '<': angle++; break;
                case '>': if (angle > 0) angle--; break;
                case '{': brace++; break;
                case '}': if (brace > 0) brace--; break;
                case '(': paren++; break;
                case ')': if (paren > 0) paren--; break;
                case '[': bracket++; break;
                case ']': if (bracket > 0) bracket--; break;
            }
            index++;
        }

        return index;
    }

    int TsFindMatching(int index, char open, char close)
    {
        var depth = 0;
        var isAngleBracket = close == '>';
        for (var i = index; i < _input.Length; i++)
        {
            if (_input[i] == open) depth++;
            else if (_input[i] == close && (i == 0 || _input[i - 1] != '=') && --depth == 0) return i;
            else if (isAngleBracket && depth == 1 && (_input[i] == ';' || _input[i] == '{'))
                return -1;
        }

        return -1;
    }
}
