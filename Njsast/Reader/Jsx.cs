using System;
using Njsast.Ast;

namespace Njsast.Reader;

public sealed partial class Parser
{
    bool CanStartJsx()
    {
        if (!Options.ParseJSX)
            return false;
        if (Type != TokenType.Relational || !"<".Equals(Value))
            return false;
        var next = _input.Get(End.Index);
        if (next == '>' || next == '_' || next == '$' || next == ':' || next == '-' ||
            next >= 'a' && next <= 'z' || next >= 'A' && next <= 'Z')
        {
            if (Options.ParseTypeScript && TsLooksLikeGenericOrTypeAssertion() &&
                !TsLooksLikeJsxOpeningWithTypeArguments() &&
                !TsLooksLikeJsxOpeningWithAttributes() &&
                !TsLooksLikeJsxOpeningWithClosingTag())
                return false;
            return true;
        }

        return false;
    }

    bool TsLooksLikeGenericOrTypeAssertion()
    {
        var end = TsFindTypeArgumentListEnd(Start.Index);
        if (end < 0)
            return false;
        var next = end + 1;
        while (next < _input.Length && char.IsWhiteSpace(_input[next]))
            next++;
        if (next >= _input.Length)
            return false;
        var ch = _input[next];
        return ch == '(' || ch == '=' || ch == '?' || ch == ',';
    }

    bool TsLooksLikeJsxOpeningWithAttributes()
    {
        var index = End.Index;
        while (index < _input.Length)
        {
            var ch = _input.Get(index);
            if (!(ch == '_' || ch == '$' || ch == '-' || ch == ':' || ch == '.' ||
                  ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9'))
                break;
            index++;
        }

        if (index >= _input.Length || !char.IsWhiteSpace(_input[index]))
            return false;
        while (index < _input.Length && char.IsWhiteSpace(_input[index]))
            index++;
        if (index >= _input.Length)
            return false;
        var next = _input.Get(index);
        if (next == '>' || next == '/' || next == '{')
            return true;
        if (!(next == '_' || next == '$' || next == ':' ||
              next >= 'a' && next <= 'z' || next >= 'A' && next <= 'Z'))
            return false;
        var attrStart = index;
        index++;
        while (index < _input.Length)
        {
            var ch = _input.Get(index);
            if (!(ch == '_' || ch == '$' || ch == '-' || ch == ':' ||
                  ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9'))
                break;
            index++;
        }
        if (_input.AsSpan(attrStart, index - attrStart).SequenceEqual("extends".AsSpan()))
            return false;
        while (index < _input.Length && char.IsWhiteSpace(_input[index]))
            index++;
        if (index >= _input.Length)
            return false;
        next = _input.Get(index);
        return next == '=' || next == '>' || next == '/' || next == '{' ||
               next == '_' || next == '$' || next == ':' ||
               next >= 'a' && next <= 'z' || next >= 'A' && next <= 'Z';
    }

    bool TsLooksLikeJsxOpeningWithClosingTag()
    {
        var index = End.Index;
        while (index < _input.Length)
        {
            var ch = _input.Get(index);
            if (!(ch == '_' || ch == '$' || ch == '-' || ch == ':' || ch == '.' ||
                  ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9'))
                break;
            index++;
        }

        if (index >= _input.Length || _input.Get(index) != '>')
            return false;
        var tagName = _input.Substring(End.Index, index - End.Index);
        return tagName.Length > 0 &&
               _input.IndexOf("</" + tagName + ">", index + 1, StringComparison.Ordinal) >= 0;
    }

    bool TsLooksLikeJsxOpeningWithTypeArguments()
    {
        var index = End.Index;
        while (index < _input.Length)
        {
            var ch = _input.Get(index);
            if (!(ch == '_' || ch == '$' || ch == '-' || ch == ':' || ch == '.' ||
                  ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9'))
                break;
            index++;
        }
        if (index >= _input.Length || _input.Get(index) != '<')
            return false;
        var end = TsFindTypeArgumentListEnd(index);
        if (end < 0)
            return false;
        index = end + 1;
        while (index < _input.Length && char.IsWhiteSpace(_input[index]))
            index++;
        if (index >= _input.Length)
            return false;
        var next = _input.Get(index);
        return next == '>' || next == '/' || next == '{' || next == '_' || next == '$' || next == '-' ||
               next >= 'a' && next <= 'z' || next >= 'A' && next <= 'Z';
    }

    AstNode ParseJsxElementOrFragment(Position startLocation)
    {
        _pos = End;
        if (_input.Get(_pos.Index) == '>')
        {
            _pos = _pos.Increment(1);
            var children = ParseJsxChildren(null);
            var end = CurPosition();
            Start = end;
            NextToken();
            return new AstJsxFragment(SourceFile, startLocation, end, ref children);
        }

        return ParseJsxElement(startLocation, true);
    }

    AstJsxElement ParseJsxElement(Position startLocation, bool finishToken)
    {
        var name = ParseJsxName();
        TsTrySkipJsxTypeArguments();
        var attributes = new StructList<AstJsxAttributeLike>();
        SkipSpace();
        while (_input.Get(_pos.Index) != '>' &&
               !(_input.Get(_pos.Index) == '/' && _input.Get(_pos.Index + 1) == '>'))
        {
            attributes.Add(ParseJsxAttribute());
            SkipSpace();
        }

        var selfClosing = false;
        if (_input.Get(_pos.Index) == '/' && _input.Get(_pos.Index + 1) == '>')
        {
            _pos = _pos.Increment(2);
            selfClosing = true;
        }
        else
        {
            ReadJsxExpected(">");
        }

        var children = selfClosing ? new StructList<AstNode>() : ParseJsxChildren(name.AsString());
        var end = CurPosition();
        if (finishToken)
        {
            Start = end;
            NextToken();
        }
        return new AstJsxElement(SourceFile, startLocation, end, name, ref attributes, ref children,
            selfClosing);
    }

    AstJsxAttributeLike ParseJsxAttribute()
    {
        var start = CurPosition();
        if (_input.Get(_pos.Index) == '{')
        {
            Start = start;
            NextToken();
            Expect(TokenType.BraceL);
            Expect(TokenType.Ellipsis);
            var expression = ParseExpression(Start);
            var closeEnd = End;
            if (Type != TokenType.BraceR)
                Raise(Start, "Unexpected token");
            _pos = End;
            return new AstJsxSpreadAttribute(SourceFile, start, closeEnd, expression);
        }

        var name = ParseJsxName();
        SkipSpace();
        AstNode? value = null;
        if (_input.Get(_pos.Index) == '=')
        {
            _pos = _pos.Increment(1);
            SkipSpace();
            var quote = _input.Get(_pos.Index);
            if (quote is '"' or '\'')
            {
                value = ParseJsxString();
            }
            else if (quote == '{')
            {
                value = ParseJsxExpressionContainer();
            }
            else if (quote == '<')
            {
                _pos = _pos.Increment(1);
                value = ParseJsxElement(CurPosition().Increment(-1), false);
            }
            else
            {
                Raise(CurPosition(), "Unexpected token");
            }
        }

        return new AstJsxAttribute(SourceFile, start, CurPosition(), name, value);
    }

    AstNode ParseJsxExpressionContainer()
    {
        var start = CurPosition();
        Start = start;
        NextToken();
        Expect(TokenType.BraceL);
        if (Type == TokenType.BraceR)
        {
            var end = End;
            _pos = end;
            return new AstJsxExpression(SourceFile, start, end, null);
        }

        var expression = ParseExpression(Start);
        var closeEnd = End;
        if (Type != TokenType.BraceR)
            Raise(Start, "Unexpected token");
        _pos = End;
        return new AstJsxExpression(SourceFile, start, closeEnd, expression);
    }

    AstString ParseJsxString()
    {
        var start = CurPosition();
        var quote = _input.Get(_pos.Index);
        _pos = _pos.Increment(1);
        var valueStart = _pos;
        while (_pos.Index < _input.Length && _input.Get(_pos.Index) != quote)
        {
            var ch = _input.Get(_pos.Index);
            _pos = IsNewLine(ch) ? new Position(_pos.Line + 1, 0, _pos.Index + 1) : _pos.Increment(1);
        }

        if (_pos.Index >= _input.Length)
            Raise(start, "Unterminated string constant");
        var value = _input.Substring(valueStart.Index, _pos.Index - valueStart.Index);
        _pos = _pos.Increment(1);
        return new AstString(SourceFile, start, CurPosition(), value);
    }

    StructList<AstNode> ParseJsxChildren(string? closingName)
    {
        var children = new StructList<AstNode>();
        while (true)
        {
            var start = CurPosition();
            if (_pos.Index >= _input.Length)
                Raise(start, "Unterminated JSX element");
            if (_input.Get(_pos.Index) == '<' && _input.Get(_pos.Index + 1) == '/')
            {
                _pos = _pos.Increment(2);
                if (closingName == null)
                {
                    ReadJsxExpected(">");
                    return children;
                }

                var closeName = ParseJsxName();
                if (closeName.AsString() != closingName)
                    Raise(closeName.Start, "Expected closing JSX tag " + closingName);
                SkipSpace();
                ReadJsxExpected(">");
                return children;
            }

            if (_input.Get(_pos.Index) == '<')
            {
                if (_input.Get(_pos.Index + 1) == '>')
                {
                    _pos = _pos.Increment(2);
                    var fragmentChildren = ParseJsxChildren(null);
                    children.Add(new AstJsxFragment(SourceFile, start, CurPosition(), ref fragmentChildren));
                    continue;
                }

                _pos = _pos.Increment(1);
                children.Add(ParseJsxElement(start, false));
                continue;
            }

            if (_input.Get(_pos.Index) == '{')
            {
                children.Add(ParseJsxChildExpression());
                continue;
            }

            var text = ReadJsxText();
            if (text.Length > 0)
                children.Add(new AstJsxText(SourceFile, start, CurPosition(), text));
        }
    }

    AstNode ParseJsxChildExpression()
    {
        var start = CurPosition();
        Start = start;
        NextToken();
        Expect(TokenType.BraceL);
        var isSpread = Eat(TokenType.Ellipsis);
        if (!isSpread && Type == TokenType.BraceR)
        {
            var end = End;
            Next();
            _pos = Start;
            return new AstJsxExpression(SourceFile, start, end, null);
        }

        var expression = ParseExpression(Start);
        if (Type != TokenType.BraceR)
            Raise(Start, "Unexpected token");
        var closeEnd = End;
        _pos = closeEnd;
        return isSpread
            ? new AstJsxSpreadChild(SourceFile, start, closeEnd, expression)
            : new AstJsxExpression(SourceFile, start, closeEnd, expression);
    }

    string ReadJsxText()
    {
        var start = _pos;
        while (_pos.Index < _input.Length)
        {
            var ch = _input.Get(_pos.Index);
            if (ch == '<' || ch == '{')
                break;
            _pos = IsNewLine(ch) ? new Position(_pos.Line + 1, 0, _pos.Index + 1) : _pos.Increment(1);
        }

        return _input.Substring(start.Index, _pos.Index - start.Index);
    }

    AstJsxNameBase ParseJsxName()
    {
        var start = CurPosition();
        var name = ParseJsxSimpleName();
        AstJsxNameBase res = name;
        if (_input.Get(_pos.Index) == ':')
        {
            _pos = _pos.Increment(1);
            res = new AstJsxNamespacedName(SourceFile, start, CurPosition(), name, ParseJsxSimpleName());
        }

        while (_input.Get(_pos.Index) == '.')
        {
            _pos = _pos.Increment(1);
            res = new AstJsxMemberName(SourceFile, start, CurPosition(), res, ParseJsxSimpleName());
        }

        return res;
    }

    AstJsxName ParseJsxSimpleName()
    {
        var start = CurPosition();
        while (_pos.Index < _input.Length)
        {
            var ch = _input.Get(_pos.Index);
            if (!(ch == '_' || ch == '$' || ch == '-' || ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' ||
                  ch >= '0' && ch <= '9'))
                break;
            _pos = _pos.Increment(1);
        }

        if (_pos.Index == start.Index)
            Raise(start, "Expected JSX name");
        return new AstJsxName(SourceFile, start, CurPosition(), _input.Substring(start.Index, _pos.Index - start.Index));
    }

    void TsTrySkipJsxTypeArguments()
    {
        if (!Options.ParseTypeScript)
            return;
        SkipSpace();
        if (_input.Get(_pos.Index) != '<')
            return;
        var end = TsFindTypeArgumentListEnd(_pos.Index);
        if (end < 0)
            Raise(CurPosition(), "Unexpected token");
        _pos = _pos.Increment(end - _pos.Index + 1);
    }

    void ReadJsxExpected(string expected)
    {
        if (!_input.AsSpan(_pos.Index).StartsWith(expected.AsSpan(), StringComparison.Ordinal))
            Raise(CurPosition(), "Unexpected token");
        _pos = _pos.Increment(expected.Length);
    }
}
