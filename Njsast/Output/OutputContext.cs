using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using Njsast.Ast;
using Njsast.Reader;
using Njsast.SourceMap;

namespace Njsast.Output;

public class OutputContext
{
    public readonly OutputOptions Options;
    readonly SourceMapBuilder? _sourceMapBuilder;
    StructList<char> _storage;
    StructList<AstNode> _stack;
    bool _mightNeedSpace;
    bool _mightNeedSemicolon;
    bool _frequencyCounting;
    uint[] _frequency = new uint[128];
    int _currentCol;
    public int Indentation;
    const string Spaces = "                ";
    char _lastChar = char.MinValue;

    public OutputContext(OutputOptions? options = null, SourceMapBuilder? sourceMapBuilder = null)
    {
        Options = options ?? new OutputOptions();
        _sourceMapBuilder = sourceMapBuilder;
    }

    public void InitializeForFrequencyCounting()
    {
        if (!_frequencyCounting)
        {
            _frequencyCounting = true;
            return;
        }

        _frequency = new uint[128];
    }

    public char[] FinishFrequencyCounting()
    {
        var letters = CountAndSort("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ$_");
        var numbers = CountAndSort("0123456789");
        letters.AddRange(numbers);
        _frequencyCounting = false;
        return letters.ToArray();
    }

    List<char> CountAndSort(string chars)
    {
        var list = new List<(uint, char)>(chars.Length);
        foreach (var ch in chars)
        {
            list.Add((_frequency[ch], ch));
        }

        list = list.OrderBy(i => i.Item1).ToList(); // Stable sort
        var res = new List<char>(chars.Length);
        for (var i = 0; i < list.Count; i++)
        {
            res.Add(list[list.Count - 1 - i].Item2);
        }

        return res;
    }

    public override string ToString()
    {
        return new(_storage.AsSpan());
    }

    public void TruePrint(ReadOnlySpan<char> text)
    {
        if (text.Length == 0) return;
        if (_frequencyCounting)
        {
            foreach (var ch in text)
            {
                if (ch < 128) _frequency[ch]++;
            }

            _lastChar = text[^1];
            return;
        }

        if (_sourceMapBuilder != null)
        {
            _sourceMapBuilder.AddTextWithMapping(text);
        }
        else
        {
            _storage.AddRange(text);
        }

        _lastChar = text[^1];
        _currentCol += text.Length;
        var pos = text.IndexOf('\n');
        while (pos >= 0)
        {
            text = text[(pos + 1)..];
            _currentCol = text.Length;
            pos = text.IndexOf('\n');
        }
    }

    public void Print(ReadOnlySpan<char> text)
    {
        if (text.Length == 0) return;
        _needDotAfterNumber = false;
        var ch = text[0];
        _hasParens = ch == '(' && text.Length == 1;

        if (_mightNeedSemicolon)
        {
            _mightNeedSemicolon = false;
            if (_lastChar == ':' && ch == '}' || _lastChar != ';' && ch != ';' && ch != '}')
            {
                if (_currentCol < Options.MaxLineLen || "\n([+*/-,.`".Contains(ch)
                   ) // these characters cannot be on start of new line without semicolon
                {
                    TruePrint(";");
                }
                else
                {
                    TruePrint("\n");
                }

                _mightNeedSpace = false;
            }
        }

        if (_mightNeedSpace)
        {
            _mightNeedSpace = false;
            if (Parser.IsIdentifierChar(_lastChar)
                && (Parser.IsIdentifierChar(ch) || ch == '\\')
                || ch == '/' && ch == _lastChar
                || ch is '+' or '-' && ch == _lastChar
               )
            {
                TruePrint(" ");
            }
        }

        TruePrint(text);
    }

    public void PushNode(AstNode node)
    {
        _stack.Add(node);
    }

    public void PopNode()
    {
        _stack.Pop();
    }

    public void Comma()
    {
        Print(",");
        Space();
    }

    public void Space()
    {
        if (Options.Beautify)
        {
            Print(" ");
        }
        else
        {
            _mightNeedSpace = true;
        }
    }

    public void Semicolon()
    {
        if (Options.Beautify)
        {
            Print(";");
        }
        else
        {
            _mightNeedSemicolon = true;
        }
    }

    public void ForceSemicolon()
    {
        _mightNeedSemicolon = false;
        Print(";");
    }

    public void Newline()
    {
        if (Options.Beautify)
        {
            Print("\n");
        }
        else
        {
            if (!_mightNeedSemicolon && _currentCol > Options.MaxLineLen)
            {
                Print("\n");
            }
        }
    }

    public void Indent(bool half = false)
    {
        if (Options.Beautify)
        {
            var c = Options.IndentStart + Indentation;
            if (half) c -= Options.IndentLevel / 2;
            while (c >= Spaces.Length)
            {
                Print(Spaces);
                c -= Spaces.Length;
            }

            if (c > 0)
            {
                Print(Spaces.AsSpan(0, c));
            }
        }
    }

    public void Colon()
    {
        Print(":");
        Space();
    }

    public void PrintBraced(in StructList<AstNode> body, bool hasUseStrictDirective)
    {
        if (body.Count > 0)
        {
            Print("{");
            Newline();
            Indentation += Options.IndentLevel;
            if (hasUseStrictDirective)
            {
                AddMapping(null, new(), true);
                Indent();
                Print("\"use strict\"");
                ForceSemicolon();
                Newline();
            }

            var last = body.Count - 1;
            for (var i = 0; i <= last; i++)
            {
                var stmt = body[(uint) i];
                if (stmt is AstEmptyStatement) continue;
                Indent();
                stmt.Print(this);
                Newline();
            }

            Indentation -= Options.IndentLevel;
            Indent();
            Print("}");
        }
        else
        {
            Print("{}");
        }
    }

    public void PrintBraced(AstBlock block, bool hasUseStrictDirective)
    {
        PrintBraced(block.Body, hasUseStrictDirective);
    }

    public void MakeBlock(AstStatement stmt)
    {
        if (stmt is AstEmptyStatement)
            Print("{}");
        else if (stmt is AstBlock)
            stmt.Print(this);
        else
        {
            Print("{");
            Newline();
            Indentation += Options.IndentLevel;
            Indent();
            stmt.Print(this);
            Newline();
            Indentation -= Options.IndentLevel;
            Indent();
            Print("}");
        }
    }

    public void PrintBody(AstStatement body)
    {
        ForceStatement(body);
    }

    public void ForceStatement(AstStatement body)
    {
        if (Options.Braces)
        {
            MakeBlock(body);
        }
        else
        {
            if (body is AstEmptyStatement)
            {
                ForceSemicolon();
            }
            else
            {
                body.Print(this);
            }
        }
    }

    class NoInWalker : TreeWalker
    {
        internal bool Parens;

        protected override void Visit(AstNode node)
        {
            if (Parens || node is AstScope)
            {
                StopDescending();
                return;
            }

            if (node is AstBinary {Operator: Operator.In})
            {
                Parens = true;
                StopDescending();
            }
        }
    }

    public void ParenthesizeForNoIn(AstNode node, bool noIn)
    {
        var parens = false;
        // need to take some precautions here:
        //    https://github.com/mishoo/UglifyJS2/issues/60
        if (noIn)
        {
            var w = new NoInWalker();
            w.Walk(node);
            parens = w.Parens;
        }

        node.Print(this, parens);
    }

    public AstNode? Parent(int distance = 0)
    {
        if (distance > (int) _stack.Count - 2)
            return null;
        return _stack[(uint) (_stack.Count - 2 - distance)];
    }

    public void AddMapping(string? sourceFile, in Position position, bool allowMerge)
    {
        if (_frequencyCounting)
            return;
        _sourceMapBuilder?.AddMapping(sourceFile, position.Line, position.Column, allowMerge);
    }

    public bool NeedConstructorParens(AstCall call)
    {
        // Always print parentheses with arguments
        return call.Args.Count > 0 || Options.Beautify;
    }

    public bool ShouldBreak()
    {
        return _currentCol > Options.MaxLineLen;
    }

    public bool WasNewLine()
    {
        return _currentCol == 0;
    }

    public bool NeedDotAfterNumber()
    {
        return _needDotAfterNumber;
    }

    public void SetNeedDotAfterNumber()
    {
        _needDotAfterNumber = true;
    }

    public void PrintName(string name)
    {
        if (_frequencyCounting)
        {
            Print("a");
            _frequency['a']--;
        }

        Print(name);
    }

    public static bool OperatorStartsWithPlusOrMinus(Operator op)
    {
        var ch = OperatorToString(op)[0];
        return ch == '+' || ch == '-';
    }

    public static bool OperatorEndsWithPlusOrMinus(Operator op)
    {
        var str = OperatorToString(op);
        var ch = str[^1];
        return ch == '+' || ch == '-';
    }

    public static bool OperatorStartsWithLetter(Operator op)
    {
        switch (op)
        {
            case Operator.Delete:
            case Operator.In:
            case Operator.InstanceOf:
            case Operator.Void:
            case Operator.TypeOf:
                return true;
        }

        return false;
    }

    public static string OperatorToString(Operator op)
    {
        switch (op)
        {
            case Operator.Addition:
                return "+";
            case Operator.Subtraction:
                return "-";
            case Operator.Multiplication:
                return "*";
            case Operator.Division:
                return "/";
            case Operator.Modulus:
                return "%";
            case Operator.Power:
                return "**";
            case Operator.LeftShift:
                return "<<";
            case Operator.RightShift:
                return ">>";
            case Operator.RightShiftUnsigned:
                return ">>>";
            case Operator.BitwiseAnd:
                return "&";
            case Operator.BitwiseOr:
                return "|";
            case Operator.BitwiseXOr:
                return "^";
            case Operator.Equals:
                return "==";
            case Operator.StrictEquals:
                return "===";
            case Operator.NotEquals:
                return "!=";
            case Operator.StrictNotEquals:
                return "!==";
            case Operator.LessThan:
                return "<";
            case Operator.LessEquals:
                return "<=";
            case Operator.GreaterThan:
                return ">";
            case Operator.GreaterEquals:
                return ">=";
            case Operator.LogicalAnd:
                return "&&";
            case Operator.LogicalOr:
                return "||";
            case Operator.NullishCoalescing:
                return "??";
            case Operator.Assignment:
                return "=";
            case Operator.AdditionAssignment:
                return "+=";
            case Operator.SubtractionAssignment:
                return "-=";
            case Operator.MultiplicationAssignment:
                return "*=";
            case Operator.DivisionAssignment:
                return "/=";
            case Operator.ModulusAssignment:
                return "%=";
            case Operator.PowerAssignment:
                return "**=";
            case Operator.LeftShiftAssignment:
                return "<<=";
            case Operator.RightShiftAssignment:
                return ">>=";
            case Operator.RightShiftUnsignedAssignment:
                return ">>>=";
            case Operator.LogicalOrAssignment:
                return "||=";
            case Operator.LogicalAndAssignment:
                return "&&=";
            case Operator.NullishCoalescingAssignment:
                return "??=";
            case Operator.BitwiseAndAssignment:
                return "&=";
            case Operator.BitwiseOrAssignment:
                return "|=";
            case Operator.BitwiseXOrAssignment:
                return "^=";
            case Operator.Increment:
            case Operator.IncrementPostfix:
                return "++";
            case Operator.Decrement:
            case Operator.DecrementPostfix:
                return "--";
            case Operator.BitwiseNot:
                return "~";
            case Operator.LogicalNot:
                return "!";
            case Operator.Delete:
                return "delete";
            case Operator.In:
                return "in";
            case Operator.InstanceOf:
                return "instanceof";
            case Operator.Void:
                return "void";
            case Operator.TypeOf:
                return "typeof";
            default:
                throw new ArgumentOutOfRangeException(nameof(op), op, null);
        }
    }

    public void Print(Operator op)
    {
        Print(OperatorToString(op));
    }

    public void PrintString(ReadOnlySpan<char> str)
    {
        var sq = 0;
        var dq = 0;
        foreach (var ch in str)
        {
            if (ch == '\'')
                sq++;
            else if (ch == '\"') dq++;
        }

        if (sq < dq)
        {
            Print("\'");
            PrintStringChars(str, QuoteType.Single);
            Print("\'");
        }
        else
        {
            Print("\"");
            PrintStringChars(str, QuoteType.Double);
            Print("\"");
        }
    }

    public void PrintNumber(double value)
    {
        Span<char> buf = stackalloc char[31];
        if (!value.TryFormat(buf[..30], out var charLen, "R", CultureInfo.InvariantCulture))
            throw new ArgumentOutOfRangeException(nameof(value));
        buf[charLen] = (char)0;
        if (buf[0] == '0' && buf[1] == '.')
        {
            Print(buf.Slice(1, charLen - 1));
            _needDotAfterNumber = false;
            return;
        }

        if (charLen > 3 && buf[charLen - 1] == '0' && buf[charLen - 2] == '0' && buf[charLen - 3] == '0')
        {
            var z = charLen - 3;
            while (buf[z] == '0') z--;
            z++;
            buf[z] = 'e';
            var exp = charLen - z;
            z++;
            exp.TryFormat(buf.Slice(z, 3), out charLen, default, CultureInfo.InvariantCulture);
            charLen += z;
            Print(buf[..charLen]);
            _needDotAfterNumber = false;
            return;
        }

        var str = buf[..charLen];
        Print(str);
        _needDotAfterNumber = !(str.Contains('.') ||
                                str.Contains('e'));
    }

    public void PrintBigInt(BigInteger value)
    {
        Span<char> buf = stackalloc char[512];
        if (value.TryFormat(buf, out var len))
        {
            Print(buf[..len]);
        }
        else
        {
            Print(value.ToString());
        }
        TruePrint("n");
    }

    public void PrintPropertyName(string name)
    {
        if (!name.StartsWith('+') && uint.TryParse(name, out var _))
        {
            Print(name);
        }
        else if (IsIdentifierString(name))
        {
            Print(name);
        }
        else
        {
            PrintString(name);
        }
    }

    static readonly Regex SimpleIdentifierRegex = new Regex("^[a-zA-Z_$][a-zA-Z0-9_$]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsIdentifierString(string key)
    {
        return SimpleIdentifierRegex.IsMatch(key);
    }

    const string KeywordsStr =
        "break case catch class const continue debugger default delete do else export extends finally for function if in instanceof let new return switch throw try typeof var void while with";

    const string KeywordsAtomStr = "false null true";

    static readonly string ReservedWordsStr =
        "enum implements import interface package private protected public static super this " + KeywordsAtomStr +
        " " + KeywordsStr;

    public static readonly HashSet<string> ReservedWords = new HashSet<string>();

    static OutputContext()
    {
        foreach (var s in ReservedWordsStr.Split(' '))
        {
            ReservedWords.Add(s);
        }
    }

    public static bool IsIdentifier(string key)
    {
        return !ReservedWords.Contains(key);
    }


    static readonly string[] SpecialChars =
    {
        "\\x00", "\\x01", "\\x02", "\\x03", "\\x04", "\\x05", "\\x06", "\\x07",
        "\\b", "\\t", "\\n", "\\v", "\\f", "\\r", "\\x0e", "\\x0f",
        "\\x10", "\\x11", "\\x12", "\\x13", "\\x14", "\\x15", "\\x16", "\\x17",
        "\\x18", "\\x19", "\\x1a", "\\x1b", "\\x1c", "\\x1d", "\\x1e", "\\x1f",
    };

    bool _hasParens;
    bool _needDotAfterNumber;

    public void PrintStringChars(ReadOnlySpan<char> s, QuoteType quoteType)
    {
        var lastOk = 0;
        var quoteChar = quoteType == QuoteType.Single ? '\'' : quoteType == QuoteType.Double ? '"' : '`';
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (ch == quoteChar || ch == '$' && quoteType == QuoteType.Template)
            {
                TruePrint(s.Slice(lastOk, i - lastOk));
                lastOk = i;
                TruePrint("\\");
            }
            else if (ch == 0)
            {
                var followedByNumber = false;
                if (i + 1 < s.Length)
                {
                    var next = s[i + 1];
                    if (next is >= '0' and <= '9')
                    {
                        followedByNumber = true;
                    }
                }

                TruePrint(s.Slice(lastOk, i - lastOk));
                lastOk = i + 1;
                TruePrint(followedByNumber ? "\\x00" : "\\0");
            }
            else if (ch < 32)
            {
                TruePrint(s.Slice(lastOk, i - lastOk));
                lastOk = i + 1;
                TruePrint(SpecialChars[ch]);
            }
            else if (ch == '\\')
            {
                TruePrint(s.Slice(lastOk, i + 1 - lastOk));
                lastOk = i;
            }
            else if (ch == 0x2028)
            {
                TruePrint(s.Slice(lastOk, i - lastOk));
                lastOk = i + 1;
                TruePrint("\\u2028");
            }
            else if (ch == 0x2029)
            {
                TruePrint(s.Slice(lastOk, i - lastOk));
                lastOk = i + 1;
                TruePrint("\\u2029");
            }
            else if (ch == 0xfeff)
            {
                TruePrint(s.Slice(lastOk, i - lastOk));
                lastOk = i + 1;
                TruePrint("\\ufeff");
            }
            else if (Options.InlineScript)
            {
                if (s.Length-i >= 9 && s[i..].StartsWith("</script",StringComparison.OrdinalIgnoreCase) && s[i+8] is '>' or '\\' or '/' or '\t' or '\n' or '\f' or '\r' or ' ')
                {
                    i++;
                    TruePrint(s.Slice(lastOk, i - lastOk));
                    lastOk = i + 1;
                    TruePrint("\\/");
                }
                else if (s[i..].StartsWith("<!--",StringComparison.Ordinal)) {
                    TruePrint(s.Slice(lastOk, i - lastOk));
                    lastOk = i + 1;
                    TruePrint("\\x3c");
                }
                else if (s[i..].StartsWith("-->", StringComparison.Ordinal))
                {
                    i += 2;
                    TruePrint(s.Slice(lastOk, i - lastOk));
                    lastOk = i + 1;
                    TruePrint("\\x3e");
                }
            }
        }

        TruePrint(s.Slice(lastOk));
    }

    public bool HasParens()
    {
        return _hasParens;
    }

    public bool FirstInStatement()
    {
        var node = Parent(-1); // Current Node
        AstNode? p;
        for (var i = 0; (p = Parent(i)) != null; i++)
        {
            if (p is AstArrow { Body: { Count: 1 } body } && body[0] == node)
                return true;
            if (p is IAstStatementWithBody statementWithBody && statementWithBody.GetBody() == node)
                return true;
            if (p is AstSequence sequence && sequence.Expressions[0] == node ||
                p.GetType() == typeof(AstCall) && ((AstCall) p).Expression == node ||
                p is AstDot dot && dot.Expression == node ||
                p is AstSub sub && sub.Expression == node ||
                p is AstConditional conditional && conditional.Condition == node ||
                p is AstBinary binary && binary.Left == node ||
                p is AstUnaryPostfix unaryPostfix && unaryPostfix.Expression == node
               )
            {
                node = p;
            }
            else
            {
                return false;
            }
        }

        return false;
    }

    public static int Precedence(Operator @operator)
    {
        return @operator switch
        {
            Operator.Assignment => 0,
            Operator.PowerAssignment => 0,
            Operator.ModulusAssignment => 0,
            Operator.AdditionAssignment => 0,
            Operator.DivisionAssignment => 0,
            Operator.SubtractionAssignment => 0,
            Operator.MultiplicationAssignment => 0,
            Operator.BitwiseOrAssignment => 0,
            Operator.LeftShiftAssignment => 0,
            Operator.BitwiseAndAssignment => 0,
            Operator.RightShiftAssignment => 0,
            Operator.BitwiseXOrAssignment => 0,
            Operator.RightShiftUnsignedAssignment => 0,
            Operator.LogicalOrAssignment => 0,
            Operator.NullishCoalescingAssignment => 0,
            Operator.LogicalAndAssignment => 0,
            Operator.LogicalOr => 1,
            Operator.NullishCoalescing => 1,
            Operator.LogicalAnd => 2,
            Operator.BitwiseOr => 3,
            Operator.BitwiseXOr => 4,
            Operator.BitwiseAnd => 5,
            Operator.Equals => 6,
            Operator.NotEquals => 6,
            Operator.StrictEquals => 6,
            Operator.StrictNotEquals => 6,
            Operator.LessThan => 7,
            Operator.GreaterThan => 7,
            Operator.LessEquals => 7,
            Operator.GreaterEquals => 7,
            Operator.In => 7,
            Operator.InstanceOf => 7,
            Operator.RightShift => 8,
            Operator.LeftShift => 8,
            Operator.RightShiftUnsigned => 8,
            Operator.Addition => 9,
            Operator.Subtraction => 9,
            Operator.Multiplication => 10,
            Operator.Division => 10,
            Operator.Modulus => 10,
            Operator.Power => 11,
            _ => throw new ArgumentOutOfRangeException(nameof(@operator), $"Must be binary operator: {@operator}")
        };
    }

    public bool NeedNodeParens(AstNode node)
    {
        PushNode(node);
        var needParens = node.NeedParens(this);
        PopNode();
        return needParens;
    }
}
