using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Njsast;
using Njsast.Ast;
using Njsast.Output;

namespace Njsast.Reader;

public sealed partial class Parser
{
    bool IsTypeScript => Options.ParseTypeScript;

    internal readonly record struct TsEnumMember(
        string Name,
        string KeyExpression,
        string ReverseNameExpression,
        string? ReferenceName,
        string? Value,
        bool ForceReverseMap);

    AstStatement TsParseTypeOnlyStatement(Position startLocation)
    {
        TsSkipTypeDeclaration();
        return new AstTypeScriptOnly(SourceFile, startLocation, _lastTokEnd);
    }

    void TsInsertEmptyExportModuleMarker(ref StructList<AstNode> body)
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
        return IsTypeScript && IsContextual("declare");
    }

    bool TsIsNamespaceStatementStart()
    {
        return IsTypeScript && (IsContextual("namespace") || IsContextual("module"));
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
            TsSkipUntilStatementEnd();
            _tsErasedTypeOnlyModuleSyntaxUsed = true;
            return new AstTypeScriptOnly(SourceFile, startLocation, _lastTokEnd);
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

        var declarations = new StructList<AstVarDef>();
        var symbol = new AstSymbolVar(alias);
        declarations.Add(new AstVarDef(SourceFile, alias.Start, _lastTokEnd, symbol, value));
        return new AstVar(SourceFile, startLocation, _lastTokEnd, ref declarations);
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

        var definitions = new StructList<AstVarDef>();
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
        var loopDefinitions = new StructList<AstVarDef>();
        loopDefinitions.Add(new AstVarDef(SourceFile, bindingStart, bindingStart,
            new AstSymbolConst(new AstSymbolRef(SourceFile, bindingStart, bindingStart, iterName))));
        var loopInit = new AstConst(SourceFile, bindingStart, bindingStart, ref loopDefinitions);

        var envName = TsAllocateUsingEnvName(topLevel: false);
        var errorName = TsAllocateUsingErrorName(topLevel: false);

        var tryBody = new StructList<AstNode>();
        if (isDestructuring)
        {
            var bindingSource = _input.Substring(bindingStart.Index, bindingEnd.Index - bindingStart.Index);
            var rawUsing = (isAwaitUsing ? "await " : "") + "using " + bindingSource + " = " + iterName + ";";
            tryBody.Add(new AstRawStatement(SourceFile, usingStart, bindingEnd, rawUsing));
        }
        else
        {
            var symbol = (AstSymbol)id;
            var scopedDefinitions = new StructList<AstVarDef>();
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

        var loopBodyStatements = new StructList<AstNode>();
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
        var scopedDefinitions = new StructList<AstVarDef>();
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

        var tryBody = new StructList<AstNode>();
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

        var body = new StructList<AstNode>();
        body.Add(TsBuildUsingEnvDeclaration(usingStart, envName));
        body.Add(TsBuildUsingTry(usingStart, envName, errorName, isAwaitUsing, ref tryBody));
        return new AstBlockStatement(SourceFile, nodeStart, _lastTokEnd, ref body);
    }

    void ParseForUsingInitializerDefinition(Position usingStart, string envName, bool isAwaitUsing, AstNode id,
        Position declStart, Position bindingEnd, ref StructList<AstVarDef> scopedDefinitions,
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

        var declarations = new StructList<AstVarDef>();
        var topLevelModuleStatements = new List<AstStatement>();
        var topLevelDeclarationsAfterUsingVar = new List<AstStatement>();
        var tryBody = new StructList<AstNode>();
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
        List<AstStatement> declarationsAfterUsingVar, ref StructList<AstNode> tryBody,
        ref StructList<AstVarDef> declarations)
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

        var exportedDeclarations = new StructList<AstVarDef>();
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
        List<AstStatement> moduleStatements, ref StructList<AstNode> tryBody,
        ref StructList<AstVarDef> usingDeclarations)
    {
        if (generatedStatements.Count == 0)
            return;

        var firstRuntimeStatement = 0;
        if (generatedStatements[0] is AstExport { ExportedDefinition: AstDefinitions definitions } export)
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

    void TsHoistTopLevelUsingScopeExportedLocals(AstExport export, ref StructList<AstNode> tryBody,
        ref StructList<AstVarDef> usingDeclarations)
    {
        if (export.ModuleName != null || export.ExportedNames.Count == 0)
            return;

        var exportedLocals = new HashSet<string>(StringComparer.Ordinal);
        foreach (var specifier in export.ExportedNames.AsReadOnlySpan())
            exportedLocals.Add(specifier.Name.Name);
        if (exportedLocals.Count == 0)
            return;

        var rewrittenBody = new StructList<AstNode>();
        foreach (var statement in tryBody.AsReadOnlySpan())
        {
            if (statement is not AstDefinitions definitions)
            {
                rewrittenBody.Add(statement);
                continue;
            }

            var remainingDefinitions = new StructList<AstVarDef>();
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

    static AstDefinitions TsCloneDefinitionsWith(AstDefinitions source, ref StructList<AstVarDef> definitions)
    {
        return source switch
        {
            AstConst => new AstConst(source.Source, source.Start, source.End, ref definitions),
            AstLet => new AstLet(source.Source, source.Start, source.End, ref definitions),
            _ => new AstVar(source.Source, source.Start, source.End, ref definitions)
        };
    }

    bool TsTrySplitTopLevelUsingScopeExport(AstStatement exportStatement, List<AstStatement> moduleStatements,
        List<AstStatement> declarationsAfterUsingVar, ref StructList<AstNode> tryBody,
        ref StructList<AstVarDef> usingDeclarations)
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
        var specifiers = new StructList<AstNameMapping>();
        specifiers.Add(new AstNameMapping(export.Source, export.Start, export.End,
            new AstSymbolExportForeign(export.Source, export.Start, export.End, exportedName),
            new AstSymbolExport(export.Source, export.Start, export.End, localName)));
        return new AstExport(export.Source, export.Start, export.End, null, null, ref specifiers);
    }

    void TsAddTopLevelUsingVarDeclaration(ref StructList<AstVarDef> declarations, AstNode anchor, string name)
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
        classExpression.Properties.Insert(0) = TsBuildSetFunctionNameStaticBlock(classDeclaration.Start, "default");
        return classExpression;
    }

    AstStaticBlock TsBuildSetFunctionNameStaticBlock(Position position, string name)
    {
        var args = new StructList<AstNode>();
        args.Add(new AstThis(SourceFile, position, position));
        args.Add(new AstString(SourceFile, position, position, name));
        var call = new AstCall(SourceFile, position, position,
            new AstSymbolRef(SourceFile, position, position, "__setFunctionName"), ref args);
        var body = new StructList<AstNode>();
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
        ref StructList<AstVarDef> declarations, ref StructList<AstNode> tryBody)
    {
        var scopedDefinitions = new StructList<AstVarDef>();
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

    void TsAddTopLevelUsingDestructuringDeclarations(AstNode id, ref StructList<AstVarDef> declarations)
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
        var definitions = new StructList<AstVarDef>();
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
        ref StructList<AstNode> tryBody, bool topLevel = false)
    {
        var catchBody = new StructList<AstNode>();
        catchBody.Add(TsBuildAssignmentStatement(position, envName + ".error", errorName));
        catchBody.Add(TsBuildAssignmentStatement(position, envName + ".hasError", new AstTrue(SourceFile, position, position)));
        var catchNode = new AstCatch(SourceFile, position, position,
            new AstSymbolCatch(new AstSymbolRef(SourceFile, position, position, errorName)), ref catchBody);

        var finallyBody = new StructList<AstNode>();
        if (isAwait)
        {
            var resultName = TsAllocateUsingResultName(topLevel);
            var resultDefinitions = new StructList<AstVarDef>();
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

    void TsInsertUsingHelperStatements(ref StructList<AstNode> body)
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
            body.Insert(insertIndex++) = helperStatement;
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

    bool TsTryParseNamespaceStatements(out List<AstStatement> statements, bool local = false)
    {
        statements = null!;
        var namespaceStart = Start;
        var isExport = false;
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
            var definitions = new StructList<AstVarDef>();
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

        var iifeBody = new StructList<AstNode>();
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

    void TsAddNamespaceStatements(ref StructList<AstNode> targetBody, List<AstStatement> namespaceStatements)
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

    void TsAddEnumStatements(ref StructList<AstNode> targetBody, List<AstStatement> enumStatements)
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

    bool TsBodyHasRuntimeDeclaration(StructList<AstNode> body, string name)
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

    StructList<AstNode> TsBuildNamespaceIifeBody(string namespaceName, string body, Position start, Position end,
        string? fullNamespaceName = null)
    {
        fullNamespaceName ??= namespaceName;
        _tsRuntimeEnumConstants ??= new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var parsedBody = TypeScriptParser.Parse(body, new Options
        {
            SourceType = SourceType.Module,
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
        });
        TsImportNamespaceRuntimeEnumConstants(namespaceName, fullNamespaceName, parsedBody);
        TsSyncUsingTempIndexesFromNamespaceBody(parsedBody);

        var exportedNames = TsCollectNamespaceVariableExportedNames(parsedBody);
        var iifeBody = new StructList<AstNode>();
        var namespaceDestructuringTempIndex = 0;
        var pendingDestructuringTemps = new List<string>();
        var pendingDestructuringStatements = new StructList<AstNode>();
        AstNode? pendingDestructuringAnchor = null;

        void FlushPendingDestructuring()
        {
            if (pendingDestructuringStatements.Count == 0 || pendingDestructuringAnchor == null)
                return;

            if (pendingDestructuringTemps.Count != 0)
            {
                var tempDefinitions = new StructList<AstVarDef>();
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

        var prefix = fullNamespaceName + ".";
        foreach (var pair in new List<KeyValuePair<string, Dictionary<string, string>>>(_tsRuntimeEnumConstants))
        {
            if (!pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                continue;

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

    static void TsHoistNamespaceUsingDestructuringTemps(ref StructList<AstNode> iifeBody)
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
                iifeBody.Insert((int)i - 1) = tempDeclaration;
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
        string fullParentName, Position start, Position end, bool local, ref StructList<AstNode> targetBody)
    {
        var namespaceName = namespaceNames[index];
        var fullNamespaceName = fullParentName + "." + namespaceName;

        var nestedBody = new StructList<AstNode>();
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

    bool TsTryLowerNamespaceExportedClassWithDecorators(StructList<AstNode> statements, ref uint index,
        string namespaceName, ref StructList<AstNode> iifeBody)
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

    bool TsTryLowerNamespaceExportedEnum(StructList<AstNode> statements, ref uint index, string namespaceName,
        ref StructList<AstNode> iifeBody)
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

        var localDefinitions = new StructList<AstVarDef>();
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
                Args.UnsafeBackingArray: var args
            }
        } && args[0] is AstBinary
        {
            Operator: Operator.LogicalOr,
            Left: AstSymbolRef { Name: var leftName }
        } && leftName == enumName;
    }

    AstStatement TsBuildNamespaceVariable(string namespaceName, bool isExport, Position start, Position end)
    {
        var definitions = new StructList<AstVarDef>();
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
        var definitions = new StructList<AstVarDef>();
        AstSymbolDeclaration symbol = local
            ? new AstSymbolLet(new AstSymbolRef(SourceFile, start, end, namespaceName))
            : new AstSymbolVar(new AstSymbolRef(SourceFile, start, end, namespaceName));
        definitions.Add(new AstVarDef(SourceFile, start, end, symbol));
        return local
            ? new AstLet(SourceFile, start, end, ref definitions)
            : new AstVar(SourceFile, start, end, ref definitions);
    }

    AstStatement TsBuildNamespaceIife(string namespaceName, ref StructList<AstNode> body, Position start, Position end)
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

    AstStatement TsBuildNestedNamespaceIife(string namespaceName, string parentName, ref StructList<AstNode> body,
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
        HashSet<string> exportedNames, ref StructList<AstNode> iifeBody, ref int namespaceDestructuringTempIndex)
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

    void TsLowerNamespaceBlockStatements(ref StructList<AstNode> body, string namespaceName, string fullNamespaceName,
        HashSet<string> exportedNames, ref int namespaceDestructuringTempIndex)
    {
        var loweredBody = new StructList<AstNode>();
        var pendingDestructuringTemps = new List<string>();
        var pendingDestructuringStatements = new StructList<AstNode>();
        AstNode? pendingDestructuringAnchor = null;

        void FlushPendingDestructuring()
        {
            if (pendingDestructuringStatements.Count == 0 || pendingDestructuringAnchor == null)
                return;

            if (pendingDestructuringTemps.Count != 0)
            {
                var tempDefinitions = new StructList<AstVarDef>();
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
        HashSet<string> exportedNames, ref StructList<AstNode> iifeBody, ref int namespaceDestructuringTempIndex)
    {
        if (export.ExportedDefinition is AstDefinitions definitions)
        {
            TsLowerNamespaceExportedDefinitions(definitions, namespaceName, fullNamespaceName, exportedNames, ref iifeBody,
                ref namespaceDestructuringTempIndex);
            return;
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
        HashSet<string> exportedNames, ref StructList<AstNode> iifeBody, ref int destructuringTempIndex)
    {
        for (var i = 0u; i < definitions.Definitions.Count; i++)
        {
            var definition = definitions.Definitions[i];
            if (definition.Name is not AstSymbol symbol)
            {
                TsLowerNamespaceExportedDestructuring(definition, namespaceName, exportedNames, ref iifeBody,
                    ref destructuringTempIndex);
                continue;
            }

            if (definition.Value == null)
                continue;
            if (TsIsTypeOnlyImportEqualsValue(definition.Value, namespaceName, fullNamespaceName, exportedNames))
                continue;

            var value = TsRewriteNamespaceExportReferences(definition.Value, namespaceName, exportedNames);
            iifeBody.Add(TsBuildNamespaceExportAssignment(namespaceName, symbol.Name, value, definition));
        }
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
        HashSet<string> exportedNames, ref StructList<AstNode> iifeBody, ref int tempIndex)
    {
        if (definition.Value == null)
            return;

        var assignments = new StructList<AstNode>();
        var temps = new List<string>();
        TsCollectNamespaceDestructuringAssignments(definition.Name, definition.Value!, namespaceName, exportedNames,
            ref assignments, temps, ref tempIndex);

        if (assignments.Count == 0)
            return;

        if (temps.Count != 0)
        {
            var tempDefinitions = new StructList<AstVarDef>();
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
        HashSet<string> exportedNames, List<string> temps, ref StructList<AstNode> loweredStatements,
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

            var assignments = new StructList<AstNode>();
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
        HashSet<string> exportedNames, ref StructList<AstNode> assignments, List<string> temps, ref int tempIndex)
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

    AstNode TsPrepareObjectDestructuringKey(AstNode key, ref StructList<AstNode> assignments, List<string> temps,
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
                call.Args[0] = new AstAssign(_sourceFile, currentArg.Start, currentArg.End,
                    new AstSymbolRef(_sourceFile, currentArg.Start, currentArg.End, _enumName), fallback,
                    Operator.Assignment);
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
        var members = new List<TsEnumMember>();
        while (Type != TokenType.BraceR && Type != TokenType.Eof)
        {
            var memberName = TsParseEnumMemberName();
            string? value = null;
            if (Eat(TokenType.Eq))
            {
                var valueStart = Start.Index;
                var valueEnd = TsFindEnumMemberValueEnd(valueStart);
                TsMoveToIndexAndReadToken(valueEnd);
                value = _input.Substring(valueStart, valueEnd - valueStart).Trim();
            }

            var normalized = TsNormalizeEnumMemberValue(value);
            members.Add(memberName with { Value = normalized.Value, ForceReverseMap = normalized.ForceReverseMap });
            Eat(TokenType.Comma);
        }

        Expect(TokenType.BraceR);
        if (isConst && !local && !preserveConstEnum && TsTryEvaluateConstEnumMembers(members, out var values))
        {
            _tsConstEnums ??= new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            _tsConstEnums[enumName] = values;
            statements = new List<AstStatement>();
            return true;
        }

        var parsed = Parser.Parse(TsEmitEnumJavaScript(enumName, isExport, members, _tsRuntimeEnumConstants),
            new Options { SourceType = SourceType.Module, EcmaVersion = 2022 });
        if (TsTryCollectRuntimeEnumConstants(enumName, members, _tsRuntimeEnumConstants, out var runtimeValues))
        {
            _tsRuntimeEnumConstants ??= new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            _tsRuntimeEnumConstants[enumName] = runtimeValues;
        }
        statements = new List<AstStatement>();
        foreach (var statement in parsed.Body.AsReadOnlySpan())
        {
            if (local && statement is AstVar varStatement)
            {
                var definitions = new StructList<AstVarDef>();
                definitions.AddRange(varStatement.Definitions.AsReadOnlySpan());
                statements.Add(new AstLet(varStatement.Source, varStatement.Start, varStatement.End,
                    ref definitions));
                continue;
            }
            statements.Add((AstStatement)statement);
        }
        return true;
    }

    void TsMoveToIndexAndReadToken(int index)
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
        _exprAllowed = false;
        NextToken();
    }

    int TsFindEnumMemberValueEnd(int index)
    {
        var braceDepth = 0;
        var parenDepth = 0;
        var bracketDepth = 0;
        while (index < _input.Length)
        {
            var ch = _input[index];
            if (braceDepth == 0 && parenDepth == 0 && bracketDepth == 0 && (ch == ',' || ch == '}'))
                return index;
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
                    if (braceDepth == 0) return index;
                    braceDepth--;
                    break;
                case '(': parenDepth++; break;
                case ')':
                    if (parenDepth == 0) return index;
                    parenDepth--;
                    break;
                case '[': bracketDepth++; break;
                case ']':
                    if (bracketDepth == 0) return index;
                    bracketDepth--;
                    break;
            }
            index++;
        }

        return index;
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

    TsEnumMember TsParseEnumMemberName()
    {
        if (Type == TokenType.Name)
        {
            var name = Value!.ToString()!;
            Next();
            return new TsEnumMember(name, "[\"" + TsEscapeString(name) + "\"]", "\"" + TsEscapeString(name) + "\"",
                name, null, false);
        }

        if (Type == TokenType.String)
        {
            var name = Value!.ToString()!;
            Next();
            return new TsEnumMember(name, "[\"" + TsEscapeString(name) + "\"]", "\"" + TsEscapeString(name) + "\"",
                name, null, false);
        }

        if (Type == TokenType.Num)
        {
            var raw = _input.Substring(Start.Index, End.Index - Start.Index);
            Next();
            return new TsEnumMember(raw, "[" + raw + "]", raw, null, null, false);
        }

        var keyword = TokenInformation.Types[Type].Keyword;
        if (keyword != null)
        {
            Next();
            return new TsEnumMember(keyword, "[\"" + TsEscapeString(keyword) + "\"]",
                "\"" + TsEscapeString(keyword) + "\"", keyword, null, false);
        }

        if (Type == TokenType.BracketL)
        {
            var expressionStart = End.Index;
            var end = TsFindMatchingSkippingLiterals(Start.Index, '[', ']');
            if (end < 0)
                Raise(Start, "Unexpected token");
            TsMoveToIndexAndReadToken(end);
            Expect(TokenType.BracketR);
            var expression = _input.Substring(expressionStart, end - expressionStart).Trim();
            return new TsEnumMember(expression, "[" + expression + "]", expression,
                TsTryGetLiteralEnumMemberReferenceName(expression), null, false);
        }

        Raise(Start, "Unexpected token");
        throw new InvalidOperationException();
    }

    static string? TsTryGetLiteralEnumMemberReferenceName(string expression)
    {
        if (expression.Length >= 2 && expression[0] is '"' or '\'' && expression[^1] == expression[0])
            return TsUnescapeSimpleStringLiteral(expression[1..^1]);
        if (expression.Length >= 2 && expression[0] == '`' && expression[^1] == '`' &&
            !expression.Contains("${", StringComparison.Ordinal))
            return TsUnescapeSimpleStringLiteral(expression[1..^1]);
        return null;
    }

    static string TsUnescapeSimpleStringLiteral(string value)
    {
        return Regex.Unescape(value);
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

    static bool TsTryEvaluateConstEnumMembers(List<TsEnumMember> members,
        out Dictionary<string, string> values)
    {
        return TsTryEvaluateEnumMembers(members, knownEnumConstants: null, out values);
    }

    static bool TsTryCollectRuntimeEnumConstants(string enumName, List<TsEnumMember> members,
        Dictionary<string, Dictionary<string, string>>? knownEnumConstants,
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

            var expression = member.Value;
            if (!member.ForceReverseMap)
            {
                expression = TsReplaceKnownEnumMemberReferences(expression, knownEnumConstants, enumName);
                foreach (var pair in values)
                    expression = TsReplaceCurrentEnumMemberReferences(expression, enumName: null, pair.Key, pair.Value,
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

            if (TsTryEvaluateNumericExpression(expression, out var numeric))
            {
                values[member.ReferenceName] = TsFormatEnumNumber(numeric);
                next = numeric + 1;
                continue;
            }

            next = null;
        }

        return values.Count != 0;
    }

    static bool TsTryEvaluateEnumMembers(List<TsEnumMember> members,
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

            var expression = member.Value;
            if (!member.ForceReverseMap)
            {
                if (TsContainsForwardEnumMemberReference(expression, futureReferenceNames))
                    return false;
                expression = TsReplaceKnownEnumMemberReferences(expression, knownEnumConstants);
                foreach (var pair in values)
                    expression = TsReplaceCurrentEnumMemberReferences(expression, enumName: null, pair.Key, pair.Value,
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

    static bool TsTryEvaluateNumericExpression(string expression, out double value)
    {
        return new TsConstExpressionEvaluator(expression).TryEvaluate(out value);
    }

    static HashSet<string> TsFutureEnumReferenceNames(List<TsEnumMember> members)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in members)
            if (member.ReferenceName != null)
                names.Add(member.ReferenceName);
        return names;
    }

    static bool TsContainsForwardEnumMemberReference(string expression, HashSet<string> futureReferenceNames)
    {
        foreach (var referenceName in futureReferenceNames)
            if (Regex.IsMatch(expression, $@"(?<![.\w$]){Regex.Escape(referenceName)}\b"))
                return true;
        return false;
    }

    static string TsFormatEnumNumber(double value)
    {
        return value == 0d ? "0" : value.ToString(CultureInfo.InvariantCulture);
    }

    sealed class TsConstExpressionEvaluator
    {
        readonly string _expression;
        int _index;

        public TsConstExpressionEvaluator(string expression)
        {
            _expression = expression;
        }

        public bool TryEvaluate(out double value)
        {
            value = ParseBitwiseOr();
            SkipWhiteSpace();
            return !_failed && _index == _expression.Length;
        }

        bool _failed;

        double ParseBitwiseOr()
        {
            var value = ParseBitwiseXor();
            while (Eat('|'))
                value = ToInt(value) | ToInt(ParseBitwiseXor());
            return value;
        }

        double ParseBitwiseXor()
        {
            var value = ParseBitwiseAnd();
            while (Eat('^'))
                value = ToInt(value) ^ ToInt(ParseBitwiseAnd());
            return value;
        }

        double ParseBitwiseAnd()
        {
            var value = ParseShift();
            while (Eat('&'))
                value = ToInt(value) & ToInt(ParseShift());
            return value;
        }

        double ParseShift()
        {
            var value = ParseAdditive();
            for (;;)
            {
                if (Eat(">>>"))
                    value = (int)((uint)ToInt(value) >> (ToInt(ParseAdditive()) & 31));
                else if (Eat(">>"))
                    value = ToInt(value) >> (ToInt(ParseAdditive()) & 31);
                else if (Eat("<<"))
                    value = ToInt(value) << (ToInt(ParseAdditive()) & 31);
                else
                    return value;
            }
        }

        double ParseAdditive()
        {
            var value = ParseMultiplicative();
            for (;;)
            {
                if (Eat('+'))
                    value += ParseMultiplicative();
                else if (Eat('-'))
                    value -= ParseMultiplicative();
                else
                    return value;
            }
        }

        double ParseMultiplicative()
        {
            var value = ParseExponentiation();
            for (;;)
            {
                if (Eat('*'))
                    value *= ParseExponentiation();
                else if (Eat('/'))
                    value /= ParseExponentiation();
                else if (Eat('%'))
                    value %= ParseExponentiation();
                else
                    return value;
            }
        }

        double ParseExponentiation()
        {
            var value = ParseUnary();
            if (Eat("**"))
                value = Math.Pow(value, ParseExponentiation());
            return value;
        }

        double ParseUnary()
        {
            if (Eat('+')) return ParseUnary();
            if (Eat('-')) return -ParseUnary();
            if (Eat('~')) return ~ToInt(ParseUnary());
            return ParsePrimary();
        }

        double ParsePrimary()
        {
            SkipWhiteSpace();
            if (Eat('('))
            {
                var innerValue = ParseBitwiseOr();
                if (!Eat(')')) _failed = true;
                return innerValue;
            }

            var start = _index;
            while (_index < _expression.Length &&
                   (char.IsDigit(_expression[_index]) || _expression[_index] == '.'))
                _index++;
            if (start == _index ||
                !double.TryParse(_expression.AsSpan(start, _index - start), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var value))
            {
                _failed = true;
                return 0;
            }
            return value;
        }

        bool Eat(char ch)
        {
            SkipWhiteSpace();
            if (_index >= _expression.Length || _expression[_index] != ch)
                return false;
            _index++;
            return true;
        }

        bool Eat(string text)
        {
            SkipWhiteSpace();
            if (_index + text.Length > _expression.Length ||
                !_expression.AsSpan(_index, text.Length).SequenceEqual(text.AsSpan()))
                return false;
            _index += text.Length;
            return true;
        }

        void SkipWhiteSpace()
        {
            while (_index < _expression.Length && char.IsWhiteSpace(_expression[_index]))
                _index++;
        }

        static int ToInt(double value) => unchecked((int)value);
    }

    internal static string TsEmitEnumJavaScript(string name, bool isExport, List<TsEnumMember> members,
        Dictionary<string, Dictionary<string, string>>? knownEnumConstants = null)
    {
        var builder = new StringBuilder();
        if (isExport) builder.Append("export ");
        builder.Append("var ").Append(name).Append(";\n");
        builder.Append("(function (").Append(name).Append(") {\n");
        double? nextNumeric = 0d;
        var constantValues = new Dictionary<string, string>(StringComparer.Ordinal);
        var referenceNames = new HashSet<string>(StringComparer.Ordinal);
        var stringValuedReferenceNames = new HashSet<string>(StringComparer.Ordinal);
        var futureReferenceNames = TsFutureEnumReferenceNames(members);
        foreach (var member in members)
        {
            if (member.ReferenceName != null)
                futureReferenceNames.Remove(member.ReferenceName);
            var value = member.Value;
            if (value == null)
            {
                value = nextNumeric != null ? TsFormatEnumNumber(nextNumeric.Value) : "void 0";
                if (nextNumeric != null)
                    nextNumeric++;
                if (member.ReferenceName != null)
                {
                    constantValues[member.ReferenceName] = value;
                    referenceNames.Add(member.ReferenceName);
                }
                builder.Append(name).Append('[').Append(name).Append(member.KeyExpression).Append(" = ").Append(value)
                    .Append("] = ").Append(member.ReverseNameExpression).Append(";\n");
                continue;
            }

            var expression = value;
            if (!member.ForceReverseMap)
            {
                expression = TsReplaceKnownEnumMemberReferences(expression, knownEnumConstants, name);
                foreach (var pair in constantValues)
                    expression = TsReplaceCurrentEnumMemberReferences(expression, name, pair.Key, pair.Value,
                        knownEnumConstants);
                expression = TsReplaceForwardEnumMemberReferences(expression, name, futureReferenceNames,
                    knownEnumConstants);
            }
            if (TsTryEvaluateNumericExpression(expression, out var numeric))
            {
                value = TsFormatEnumNumber(numeric);
                nextNumeric = numeric + 1;
                if (member.ReferenceName != null)
                {
                    constantValues[member.ReferenceName] = value;
                    referenceNames.Add(member.ReferenceName);
                }
                builder.Append(name).Append('[').Append(name).Append(member.KeyExpression).Append(" = ").Append(value)
                    .Append("] = ").Append(member.ReverseNameExpression).Append(";\n");
            }
            else if (!member.ForceReverseMap && TsTryEvaluateStringExpression(expression, out var stringValue))
            {
                nextNumeric = null;
                value = TsQuoteString(stringValue);
                if (member.ReferenceName != null)
                {
                    constantValues[member.ReferenceName] = value;
                    referenceNames.Add(member.ReferenceName);
                }
                builder.Append(name).Append(member.KeyExpression).Append(" = ").Append(value).Append(";\n");
            }
            else if (!member.ForceReverseMap &&
                     (TsIsStringValuedEnumExpression(expression) ||
                      TsReferencesStringValuedEnumMember(expression, name, stringValuedReferenceNames)))
            {
                nextNumeric = null;
                value = TsQualifyEnumMemberReferences(value, name,
                    TsReferenceNamesIncludingCurrent(referenceNames, member.ReferenceName),
                    member.ForceReverseMap);
                if (member.ReferenceName != null)
                {
                    referenceNames.Add(member.ReferenceName);
                    stringValuedReferenceNames.Add(member.ReferenceName);
                }
                builder.Append(name).Append(member.KeyExpression).Append(" = ").Append(value).Append(";\n");
            }
            else
            {
                nextNumeric = null;
                value = TsQualifyEnumMemberReferences(value, name,
                    TsReferenceNamesIncludingCurrent(referenceNames, member.ReferenceName),
                    member.ForceReverseMap);
                if (member.ReferenceName != null)
                    referenceNames.Add(member.ReferenceName);
                builder.Append(name).Append('[').Append(name).Append(member.KeyExpression).Append(" = ").Append(value)
                    .Append("] = ").Append(member.ReverseNameExpression).Append(";\n");
            }
        }

        builder.Append("})(").Append(name).Append(" || (").Append(name).Append(" = {}));");
        return builder.ToString();
    }

    static HashSet<string> TsReferenceNamesIncludingCurrent(HashSet<string> referenceNames, string? currentName)
    {
        if (currentName == null)
            return referenceNames;
        var result = new HashSet<string>(referenceNames, StringComparer.Ordinal);
        result.Add(currentName);
        return result;
    }

    static string TsQualifyEnumMemberReferences(string expression, string enumName, HashSet<string> referenceNames,
        bool preserveLiteralKeywords = false)
    {
        foreach (var referenceName in referenceNames)
        {
            if (!TsIsIdentifierText(referenceName))
                continue;
            if (preserveLiteralKeywords && TsIsNonReferenceEnumExpressionIdentifier(referenceName))
                continue;
            expression = Regex.Replace(expression, $@"(?<![.\w$]){Regex.Escape(referenceName)}\b",
                enumName + "." + referenceName);
        }
        return expression;
    }

    static string TsReplaceKnownEnumMemberReferences(string expression,
        Dictionary<string, Dictionary<string, string>>? knownEnumConstants, string? currentEnumName = null)
    {
        if (knownEnumConstants == null)
            return expression;

        foreach (var enumPair in knownEnumConstants)
        {
            foreach (var memberPair in enumPair.Value)
                expression = TsReplaceEnumMemberReferences(expression, enumPair.Key, memberPair.Key, memberPair.Value,
                    includeBareReference: enumPair.Key == currentEnumName);
        }

        return expression;
    }

    static string TsReplaceCurrentEnumMemberReferences(string expression, string? enumName, string referenceName,
        string replacement, Dictionary<string, Dictionary<string, string>>? knownEnumConstants)
    {
        if (enumName != null)
            expression = TsReplaceEnumMemberReferences(expression, enumName, referenceName, replacement);
        if (knownEnumConstants != null && enumName != null)
        {
            foreach (var enumPair in knownEnumConstants)
            {
                if (enumPair.Key.EndsWith("." + enumName, StringComparison.Ordinal))
                    expression = TsReplaceEnumMemberReferences(expression, enumPair.Key, referenceName, replacement);
            }
        }
        return Regex.Replace(expression, $@"(?<![.\w$]){Regex.Escape(referenceName)}\b", replacement);
    }

    static string TsReplaceForwardEnumMemberReferences(string expression, string? enumName,
        HashSet<string> futureReferenceNames, Dictionary<string, Dictionary<string, string>>? knownEnumConstants)
    {
        foreach (var referenceName in futureReferenceNames)
        {
            if (enumName != null)
                expression = TsReplaceEnumMemberReferences(expression, enumName, referenceName, "0");
            if (knownEnumConstants != null && enumName != null)
            {
                foreach (var enumPair in knownEnumConstants)
                {
                    if (enumPair.Key.EndsWith("." + enumName, StringComparison.Ordinal))
                        expression = TsReplaceEnumMemberReferences(expression, enumPair.Key, referenceName, "0");
                }
            }
            expression = Regex.Replace(expression, $@"(?<![.\w$]){Regex.Escape(referenceName)}\b", "0");
        }

        return expression;
    }

    static string TsReplaceEnumMemberReferences(string expression, string enumName, string referenceName,
        string replacement, bool includeBareReference = true)
    {
        var quoted = "\"" + Regex.Escape(TsEscapeString(referenceName)) + "\"";
        expression = Regex.Replace(expression,
            $@"\bglobalThis\s*\.\s*{Regex.Escape(enumName)}\s*\[\s*{quoted}\s*\]",
            replacement);
        expression = Regex.Replace(expression,
            $@"(?<![.\w$]){Regex.Escape(enumName)}\s*\[\s*{quoted}\s*\]",
            replacement);
        expression = Regex.Replace(expression,
            $@"(?<![.\w$]){Regex.Escape(enumName)}\s*\?\.\s*\[\s*{quoted}\s*\]",
            replacement);

        if (!TsIsIdentifierText(referenceName))
            return expression;

        expression = Regex.Replace(expression,
            $@"\bglobalThis\s*\.\s*{Regex.Escape(enumName)}\s*\.\s*{Regex.Escape(referenceName)}\b",
            replacement);
        expression = Regex.Replace(expression,
            $@"(?<![.\w$]){Regex.Escape(enumName)}\s*\.\s*{Regex.Escape(referenceName)}\b",
            replacement);
        expression = Regex.Replace(expression,
            $@"(?<![.\w$]){Regex.Escape(enumName)}\s*\?\.\s*{Regex.Escape(referenceName)}\b",
            replacement);
        return includeBareReference
            ? Regex.Replace(expression, $@"(?<![.\w$]){Regex.Escape(referenceName)}\b", replacement)
            : expression;
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

    static (string? Value, bool ForceReverseMap) TsNormalizeEnumMemberValue(string? value)
    {
        if (value == null)
            return (null, false);
        value = value.Trim();
        value = TsStripEnumErasedAssertions(value, out var forceReverseMap);
        if (value.Length >= 2 && value[0] == '`' && value[^1] == '`' && !value.Contains("${", StringComparison.Ordinal))
            return ("\"" + TsEscapeString(value[1..^1]) + "\"", forceReverseMap);
        return (value, forceReverseMap);
    }

    static string TsStripEnumErasedAssertions(string value, out bool forceReverseMap)
    {
        forceReverseMap = false;
        var result = new StringBuilder(value.Length);
        var index = 0;
        while (index < value.Length)
        {
            if ((TsTextStartsKeyword(value, index, "as") || TsTextStartsKeyword(value, index, "satisfies")) &&
                result.Length > 0 && char.IsWhiteSpace(result[^1]))
            {
                while (result.Length > 0 && char.IsWhiteSpace(result[^1]))
                    result.Length--;
                forceReverseMap = true;
                index += value[index] == 'a' ? 2 : "satisfies".Length;
                index = TsSkipTypeAssertionInText(value, index);
                continue;
            }

            if (value[index] == '!' && TsCanStripEnumNonNullAssertion(value, index, result))
            {
                forceReverseMap = true;
                index++;
                continue;
            }

            result.Append(value[index++]);
        }

        return result.ToString().Trim();
    }

    static bool TsCanStripEnumNonNullAssertion(string value, int index, StringBuilder result)
    {
        if (index + 1 < value.Length && value[index + 1] == '=')
            return false;
        var previous = result.Length - 1;
        while (previous >= 0 && char.IsWhiteSpace(result[previous]))
            previous--;
        if (previous < 0)
            return false;
        var previousChar = result[previous];
        if (!(IsIdentifierChar(previousChar, true) || previousChar is ')' or ']'))
            return false;
        var next = index + 1;
        while (next < value.Length && char.IsWhiteSpace(value[next]))
            next++;
        return next == value.Length || value[next] is '.' or '[' or ')' or ']' or '}' or ',' or '+' or '-' or '*' or
            '/' or '%' or '&' or '|' or '^' or '<' or '>' or '=' or '?' or ':';
    }

    static int TsSkipTypeAssertionInText(string value, int index)
    {
        var angle = 0;
        var brace = 0;
        var paren = 0;
        var bracket = 0;
        while (index < value.Length && char.IsWhiteSpace(value[index]))
            index++;
        while (index < value.Length)
        {
            var ch = value[index];
            if (ch is '"' or '\'' or '`')
            {
                index = TsSkipStringLike(value, index, ch);
                continue;
            }
            if (angle == 0 && brace == 0 && paren == 0 && bracket == 0 &&
                (ch == ',' || ch == ')' || ch == ']' || ch == '}' || ch == '?' || ch == ':' ||
                 TsTextStartsKeyword(value, index, "as") || TsTextStartsKeyword(value, index, "satisfies")))
                return index;
            switch (ch)
            {
                case '<': angle++; break;
                case '>': if (angle > 0) angle--; break;
                case '{': brace++; break;
                case '}':
                    if (brace > 0) brace--;
                    else return index;
                    break;
                case '(': paren++; break;
                case ')':
                    if (paren > 0) paren--;
                    else return index;
                    break;
                case '[': bracket++; break;
                case ']':
                    if (bracket > 0) bracket--;
                    else return index;
                    break;
            }
            index++;
        }

        return index;
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

    static bool TsIsStringConstantValue(string value)
    {
        value = value.Trim();
        if (!TsIsStringLiteral(value) || value.Length < 2)
            return false;
        var quote = value[0];
        if (value[^1] != quote)
            return false;
        var escaped = false;
        for (var i = 1; i < value.Length - 1; i++)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if (value[i] == '\\')
            {
                escaped = true;
                continue;
            }
            if (value[i] == quote)
                return false;
        }
        return !escaped;
    }

    static bool TsTryEvaluateStringExpression(string expression, out string value)
    {
        value = "";
        var parts = TsSplitTopLevelAddition(expression);
        if (parts.Count == 0)
            return false;

        var builder = new StringBuilder();
        var sawString = false;
        foreach (var part in parts)
        {
            if (TsTryParseStringLiteralValue(part, out var stringPart))
            {
                sawString = true;
                builder.Append(stringPart);
                continue;
            }
            if (TsTryParseTemplateLiteralValue(part, out var templatePart))
            {
                sawString = true;
                builder.Append(templatePart);
                continue;
            }
            if (TsTryEvaluateNumericExpression(part, out var numericPart))
            {
                builder.Append(TsFormatEnumNumber(numericPart));
                continue;
            }
            return false;
        }

        if (!sawString)
            return false;
        value = builder.ToString();
        return true;
    }

    static bool TsIsStringValuedEnumExpression(string expression)
    {
        var parts = TsSplitTopLevelAddition(expression);
        if (parts.Count == 0)
            return false;
        foreach (var part in parts)
        {
            if (TsTryParseStringLiteralValue(part, out _) || TsLooksLikeTemplateLiteral(part))
                return true;
        }
        return false;
    }

    static bool TsReferencesStringValuedEnumMember(string expression, string enumName,
        HashSet<string> stringValuedReferenceNames)
    {
        foreach (var referenceName in stringValuedReferenceNames)
        {
            if (Regex.IsMatch(expression, $@"(?<![.\w$]){Regex.Escape(referenceName)}\b"))
                return true;
            if (!TsIsIdentifierText(referenceName))
                continue;
            if (Regex.IsMatch(expression,
                    $@"(?<![.\w$]){Regex.Escape(enumName)}\s*\.\s*{Regex.Escape(referenceName)}\b"))
                return true;
        }

        return false;
    }

    static bool TsLooksLikeTemplateLiteral(string expression)
    {
        expression = TsStripOuterParens(expression.Trim());
        return expression.Length >= 2 && expression[0] == '`' && expression[^1] == '`';
    }

    static bool TsTryParseTemplateLiteralValue(string expression, out string value)
    {
        value = "";
        expression = TsStripOuterParens(expression.Trim());
        if (expression.Length < 2 || expression[0] != '`' || expression[^1] != '`')
            return false;

        var builder = new StringBuilder();
        for (var i = 1; i < expression.Length - 1; i++)
        {
            var ch = expression[i];
            if (ch == '\\')
            {
                if (i + 1 >= expression.Length - 1)
                    return false;
                builder.Append(expression[++i]);
                continue;
            }
            if (ch == '$' && i + 1 < expression.Length - 1 && expression[i + 1] == '{')
            {
                var close = TsFindTemplateExpressionEnd(expression, i + 2);
                if (close < 0)
                    return false;
                var inner = expression[(i + 2)..close].Trim();
                if (TsTryParseStringLiteralValue(inner, out var innerString))
                    builder.Append(innerString);
                else if (TsTryParseTemplateLiteralValue(inner, out var innerTemplate))
                    builder.Append(innerTemplate);
                else if (TsTryEvaluateNumericExpression(inner, out var innerNumber))
                    builder.Append(TsFormatEnumNumber(innerNumber));
                else
                    return false;
                i = close;
                continue;
            }
            builder.Append(ch);
        }

        value = builder.ToString();
        return true;
    }

    static int TsFindTemplateExpressionEnd(string expression, int index)
    {
        var depth = 1;
        for (var i = index; i < expression.Length; i++)
        {
            var ch = expression[i];
            if (ch is '"' or '\'' or '`')
            {
                i = TsSkipStringLike(expression, i, ch) - 1;
                continue;
            }
            if (ch == '{') depth++;
            else if (ch == '}' && --depth == 0) return i;
        }
        return -1;
    }

    static string TsStripOuterParens(string expression)
    {
        while (expression.Length >= 2 && expression[0] == '(' && expression[^1] == ')' &&
               TsMatchingParenCoversExpression(expression))
            expression = expression[1..^1].Trim();
        return expression;
    }

    static bool TsMatchingParenCoversExpression(string expression)
    {
        var depth = 0;
        for (var i = 0; i < expression.Length; i++)
        {
            var ch = expression[i];
            if (ch is '"' or '\'' or '`')
            {
                i = TsSkipStringLike(expression, i, ch) - 1;
                continue;
            }
            if (ch == '(') depth++;
            else if (ch == ')' && --depth == 0)
                return i == expression.Length - 1;
        }
        return false;
    }

    static List<string> TsSplitTopLevelAddition(string expression)
    {
        var result = new List<string>();
        var start = 0;
        var paren = 0;
        var bracket = 0;
        var brace = 0;
        for (var i = 0; i < expression.Length; i++)
        {
            var ch = expression[i];
            if (ch is '"' or '\'' or '`')
            {
                i = TsSkipStringLike(expression, i, ch) - 1;
                continue;
            }
            if (ch == '(') paren++;
            else if (ch == ')' && paren > 0) paren--;
            else if (ch == '[') bracket++;
            else if (ch == ']' && bracket > 0) bracket--;
            else if (ch == '{') brace++;
            else if (ch == '}' && brace > 0) brace--;
            else if (ch == '+' && paren == 0 && bracket == 0 && brace == 0)
            {
                var part = expression[start..i].Trim();
                if (part.Length == 0)
                    return new List<string>();
                result.Add(part);
                start = i + 1;
            }
        }

        var last = expression[start..].Trim();
        if (last.Length == 0)
            return new List<string>();
        result.Add(last);
        return result;
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

    static bool TsTryParseStringLiteralValue(string expression, out string value)
    {
        value = "";
        expression = expression.Trim();
        if (!TsIsStringConstantValue(expression))
            return false;
        try
        {
            var parsed = Parser.Parse(expression + ";", new Options
            {
                SourceType = SourceType.Script,
                EcmaVersion = 2022
            });
            if (parsed.Body.Count == 1 &&
                parsed.Body[0] is AstSimpleStatement { Body: AstString str })
            {
                value = str.Value;
                return true;
            }
        }
        catch
        {
            return false;
        }
        return false;
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
        return IsTypeScript && Type == TokenType.Name &&
               (IsContextual("public") || IsContextual("private") || IsContextual("protected") ||
                IsContextual("readonly") || IsContextual("override") || IsContextual("abstract") ||
                IsContextual("declare"));
    }

    bool TsStaticIsFollowedByClassMemberModifier()
    {
        if (!IsTypeScript || !IsContextual("static"))
            return false;

        var index = TsSkipWhitespaceAndComments(End.Index);
        return TsTextStartsKeyword(index, "readonly") || TsTextStartsKeyword(index, "override") ||
               TsTextStartsKeyword(index, "public") || TsTextStartsKeyword(index, "private") ||
               TsTextStartsKeyword(index, "protected") || TsTextStartsKeyword(index, "accessor") ||
               TsTextStartsKeyword(index, "abstract") || TsTextStartsKeyword(index, "declare");
    }

    bool TsTrySkipParameterPropertyModifiers()
    {
        if (!IsTypeScript) return false;
        var skipped = false;
        while (Type == TokenType.Name &&
               (IsContextual("public") || IsContextual("private") || IsContextual("protected") ||
                IsContextual("readonly") || IsContextual("override") || IsContextual("accessor")))
        {
            skipped = true;
            Next();
        }

        return skipped;
    }

    bool TsTrySkipStaticParameterModifier()
    {
        if (!IsTypeScript || !IsContextual("static"))
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
              IsContextual("override")))
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
        return new AstSimpleStatement(SourceFile, parameter.Start, parameter.End, assignment);
    }

    AstClassField TsBuildParameterPropertyField(AstSymbol parameter)
    {
        return new AstClassField(SourceFile, parameter.Start, parameter.End,
            new AstSymbolProperty(SourceFile, parameter.Start, parameter.End, parameter.Name), null, false);
    }

    AstSimpleStatement TsBuildStaticBlockStatement(Position start, Position end, ref StructList<AstNode> body)
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
        var index = Start.Index;
        var brace = _input.IndexOf('{', index);
        var semi = _input.IndexOf(';', index);
        if (semi < 0 || brace >= 0 && brace < semi) return false;

        TsSkipUntilStatementEnd();
        typeOnlyStatement = new AstTypeScriptOnly(SourceFile, startLocation, _lastTokEnd);
        return true;
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
        else if (Type == TokenType.Const && TsConstIsFollowedByEnum())
        {
            TsSkipAmbientDeclarationBlock();
        }
        else if (IsContextual("global") || IsContextual("namespace") || IsContextual("module") ||
                 IsContextual("enum") || IsContextual("interface"))
        {
            TsSkipAmbientDeclarationBlock();
        }
        else
        {
            TsSkipUntilStatementEnd();
        }

        return new AstTypeScriptOnly(SourceFile, startLocation, _lastTokEnd);
    }

    bool TsConstIsFollowedByEnum()
    {
        var index = End.Index;
        while (index < _input.Length && char.IsWhiteSpace(_input[index])) index++;
        return TsTextStartsKeyword(index, "enum");
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

    bool TsTryParseAccessorOverloadSignature(Position startLocation, AstNode key, PropertyKind kind, bool isStatic,
        ref StructList<AstNode> classBody)
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

        var emptyBody = new StructList<AstNode>();
        var method = new AstFunction(SourceFile, startLocation, _lastTokEnd, null, ref parameters, false, false,
            ref emptyBody);
        if (kind == PropertyKind.Get)
            classBody.Add(new AstObjectGetter(SourceFile, startLocation, _lastTokEnd, key, method, isStatic));
        else
            classBody.Add(new AstObjectSetter(SourceFile, startLocation, _lastTokEnd, key, method, isStatic));
        return true;
    }

    bool TsTryParseDeclareAccessorMember(Position methodStart, ref StructList<AstNode> classBody)
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
            var emptyBody = new StructList<AstNode>();
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

    void TsTrySkipTypeParameters()
    {
        if (!IsTypeScript || Type != TokenType.Relational || !"<".Equals(Value)) return;
        TsSkipBalancedTokenType(TokenType.Relational, "<", ">");
    }

    void TsTrySkipTypeAnnotation()
    {
        if (!IsTypeScript || !Eat(TokenType.Colon)) return;
        TsSkipType();
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
                i = TsSkipStringLike(i, ch);
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
                    if (brace == 0 && paren == 0 && bracket == 0 && --angle == 0)
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
                    if (angle == 1 && brace == 0 && paren == 0 && bracket == 0)
                        return -1;
                    break;
            }
        }

        return -1;
    }

    bool TsCanSkipInstantiationExpressionTypeArguments(int nextIndex)
    {
        if (nextIndex >= _input.Length)
            return true;
        if (TsTextStartsKeyword(nextIndex, "as") || TsTextStartsKeyword(nextIndex, "satisfies"))
            return true;
        return _input[nextIndex] is ';' or ',' or ')' or ']' or '}' or '.' or '?' or '`' or '\n' or '\r';
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

    bool TsTryParseAutoAccessor(AstSymbol? className, ref StructList<AstNode> classBody,
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

    void TsInsertPendingClassComputedKeyStatements(ref StructList<AstNode> body)
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
            body.Insert(insertIndex++) = pendingStatement.Statement;
            _tsPendingClassComputedKeyStatements.RemoveAt(i);
            i--;
        }
        if (_tsPendingClassComputedKeyStatements.Count == 0)
            _tsPendingClassComputedKeyStatements = null;
    }

    static int TsDirectivePrefixLength(StructList<AstNode> body)
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

        var definitions = new StructList<AstVarDef>();
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
        var body = new StructList<AstNode>();
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
        var body = new StructList<AstNode>();
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

        var end = TsFindStatementEndIndex(Start.Index, stopAtLineBreak: false);
        while (Type != TokenType.Eof && _lastTokEnd.Index < end)
            Next();
        Eat(TokenType.Semi);
    }

    void TsSkipClassLikeDeclaration()
    {
        while (Type != TokenType.BraceL && Type != TokenType.Semi && Type != TokenType.Eof) Next();
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

    void TsSkipType(bool stopAtExpressionOperators = false, bool stopAtForInOf = false)
    {
        var angle = 0;
        var brace = 0;
        var paren = 0;
        var bracket = 0;
        var startedType = false;
        var lastWasTypePredicateIs = false;
        var lastWasPipeOrAmp = false;
        var justClosedParen = false;
        var justHadArrowAfterParen = false;
        var sawTopLevelExtends = false;
        var conditionalTypeDepth = 0;
        while (Type != TokenType.Eof)
        {
            var allowTypeLiteral = Type == TokenType.BraceL && (!startedType || lastWasTypePredicateIs || lastWasPipeOrAmp || justHadArrowAfterParen);
            if (angle == 0 && brace == 0 && paren == 0 && bracket == 0 &&
                (Type == TokenType.Comma || Type == TokenType.ParenR ||
                 (Type == TokenType.BraceL && startedType && !allowTypeLiteral) ||
                 Type == TokenType.BraceR || Type == TokenType.Eq || Type == TokenType.Semi ||
                 (stopAtExpressionOperators && startedType && Type == TokenType.Colon &&
                  conditionalTypeDepth == 0) ||
                 (Type == TokenType.Question && !sawTopLevelExtends) ||
                 (stopAtExpressionOperators && startedType && TsIsExpressionOperatorAfterType()) ||
                 (stopAtExpressionOperators && startedType && TsIsRelationalExpressionOperatorAfterType()) ||
                 (stopAtForInOf && startedType && (Type == TokenType.In || IsContextual("of"))) ||
                 (Type == TokenType.Arrow && !justClosedParen) ||
                 (startedType && (IsContextual("as") || IsContextual("satisfies")))))
                return;

            var isArrow = Type == TokenType.Arrow;
            if (isArrow && justClosedParen)
                justHadArrowAfterParen = true;
            else
                justHadArrowAfterParen = false;
            justClosedParen = false;
            startedType = true;
            lastWasTypePredicateIs = IsContextual("is");
            lastWasPipeOrAmp = Type is TokenType.BitwiseOr or TokenType.BitwiseAnd;
            if (angle == 0 && brace == 0 && paren == 0 && bracket == 0 &&
                (Type == TokenType.Extends || IsContextual("extends")))
                sawTopLevelExtends = true;
            else if (angle == 0 && brace == 0 && paren == 0 && bracket == 0 && Type == TokenType.Question &&
                     sawTopLevelExtends)
            {
                conditionalTypeDepth++;
                sawTopLevelExtends = false;
            }
            else if (angle == 0 && brace == 0 && paren == 0 && bracket == 0 && Type == TokenType.Colon &&
                     conditionalTypeDepth > 0)
            {
                conditionalTypeDepth--;
            }
            if (Type == TokenType.Relational && "<".Equals(Value)) angle++;
            else if (Type == TokenType.Relational && ">".Equals(Value) && angle > 0) angle--;
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
                if (paren == 0) justClosedParen = true;
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

    bool TsIsExpressionOperatorAfterType()
    {
        return Type is TokenType.PlusMin or TokenType.Star or TokenType.Slash or TokenType.Modulo or TokenType.Starstar
            or TokenType.BitShift or TokenType.Equality or TokenType.LogicalOr or TokenType.LogicalAnd
            or TokenType.NullishCoalescing or TokenType.Instanceof or TokenType.In;
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
        return TsFindMatching(Start.Index, '<', '>') < 0;
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
            if (Type == openType && openValue.Equals(Value))
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
        while (index < _input.Length)
        {
            var ch = _input[index];
            if (angle == 0 && brace == 0 && paren == 0 && bracket == 0 && (ch == ';' || ch == '=' || ch == '{'))
                return index;
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
