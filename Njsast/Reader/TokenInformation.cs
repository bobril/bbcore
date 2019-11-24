using System;
using System.Collections.Generic;

namespace Njsast.Reader
{
    sealed class TokenInformation
    {
        public static readonly Dictionary<TokenType, TokenInformation> Types;

        public static readonly Dictionary<string, TokenType> Keywords = new Dictionary<string, TokenType>
        {
            {"break", TokenType.Break},
            {"case", TokenType.Case},
            {"catch", TokenType.Catch},
            {"continue", TokenType.Continue},
            {"debugger", TokenType.Debugger},
            {"default", TokenType.Default},
            {"do", TokenType.Do},
            {"else", TokenType.Else},
            {"finally", TokenType.Finally},
            {"for", TokenType.For},
            {"function", TokenType.Function},
            {"if", TokenType.If},
            {"return", TokenType.Return},
            {"switch", TokenType.Switch},
            {"throw", TokenType.Throw},
            {"try", TokenType.Try},
            {"var", TokenType.Var},
            {"const", TokenType.Const},
            {"while", TokenType.While},
            {"with", TokenType.With},
            {"new", TokenType.New},
            {"this", TokenType.This},
            {"super", TokenType.Super},
            {"class", TokenType.Class},
            {"extends", TokenType.Extends},
            {"export", TokenType.Export},
            {"import", TokenType.Import},
            {"null", TokenType.Null},
            {"true", TokenType.True},
            {"false", TokenType.False},
            {"in", TokenType.In},
            {"instanceof", TokenType.Instanceof},
            {"typeof", TokenType.Typeof},
            {"void", TokenType.Void},
            {"delete", TokenType.Delete},
            {"yield", TokenType.Name} // TODO refactor TokenType.Yield used to ensure Yield keyword does not contains unicode sequence and preserve current functionality
        };

        static TokenInformation()
        {
            var num = new TokenInformation(startsExpr: true);
            var regexp = new TokenInformation(startsExpr: true);
            var @string = new TokenInformation(startsExpr: true);
            var name = new TokenInformation(startsExpr: true);
            var eof = new TokenInformation();

            // Punctuation token types.
            var bracketL = new TokenInformation(beforeExpr: true, startsExpr: true);

            var bracketR = new TokenInformation();
            var braceL = new TokenInformation(beforeExpr: true, startsExpr: true);
            var braceR = new TokenInformation();
            var parenL = new TokenInformation(beforeExpr: true, startsExpr: true);
            var parenR = new TokenInformation();
            var comma = new TokenInformation(beforeExpr: true);
            var semi = new TokenInformation(beforeExpr: true);
            var colon = new TokenInformation(beforeExpr: true);
            var dot = new TokenInformation();
            var question = new TokenInformation(beforeExpr: true);
            var arrow = new TokenInformation(beforeExpr: true);
            var template = new TokenInformation();
            var invalidTemplate = new TokenInformation();
            var ellipsis = new TokenInformation(beforeExpr: true);
            var backQuote = new TokenInformation(startsExpr: true);
            var dollarBraceL = new TokenInformation(beforeExpr: true, startsExpr: true);

            var eq = new TokenInformation(beforeExpr: true);
            var assign = new TokenInformation(beforeExpr: true);
            var incDec = new TokenInformation(prefix: true, postfix: true, startsExpr: true);
            var prefix = new TokenInformation(beforeExpr: true, prefix: true, startsExpr: true);
            var logicalOr = CreateBinaryOperation(1);
            var logicalAnd = CreateBinaryOperation(2);
            var bitwiseOr = CreateBinaryOperation(3);
            var bitwiseXor = CreateBinaryOperation(4);
            var bitwiseAnd = CreateBinaryOperation(5);
            var equality = CreateBinaryOperation(6);
            var relational = CreateBinaryOperation(7);
            var bitShift = CreateBinaryOperation(8);
            var plusMin = new TokenInformation(beforeExpr: true, binaryOperation: 9, prefix: true, startsExpr: true);
            var modulo = CreateBinaryOperation(10);
            var star = CreateBinaryOperation(10);
            var slash = CreateBinaryOperation(10);
            var starstar = new TokenInformation(beforeExpr: true);

            // Keyword token types.
            var _break = CreateKeyword("break");

            var _case = CreateKeyword("case", true);
            var _catch = CreateKeyword("catch");
            var _continue = CreateKeyword("continue");
            var debugger = CreateKeyword("debugger");
            var _default = CreateKeyword("default", true);
            var _do = CreateKeyword("do", isLoop: true, beforeExpr: true);
            var _else = CreateKeyword("else", true);
            var _finally = CreateKeyword("finally");
            var _for = CreateKeyword("for", isLoop: true);
            var function = CreateKeyword("function", startsExpr: true);
            var _if = CreateKeyword("if");
            var _return = CreateKeyword("return", true);
            var _switch = CreateKeyword("switch");
            var _throw = CreateKeyword("throw", true);
            var _try = CreateKeyword("try");
            var var = CreateKeyword("var");
            var _const = CreateKeyword("const");
            var _while = CreateKeyword("while", isLoop: true);
            var with = CreateKeyword("with");
            var _new = CreateKeyword("new", true, true);
            var _this = CreateKeyword("this", startsExpr: true);
            var super = CreateKeyword("super", startsExpr: true);
            var _class = CreateKeyword("class", startsExpr: true);
            var extends = CreateKeyword("extends", true);
            var export = CreateKeyword("export");
            var import = CreateKeyword("import");
            var _null = CreateKeyword("null", startsExpr: true);
            var _true = CreateKeyword("true", startsExpr: true);
            var _false = CreateKeyword("false", startsExpr: true);
            var _in = CreateKeyword("in", true, binop: 7);
            var instanceof = CreateKeyword("instanceof", true, binop: 7);
            var _typeof = CreateKeyword("typeof", true, prefix: true, startsExpr: true);
            var _void = CreateKeyword("void", true, prefix: true, startsExpr: true);
            var delete = CreateKeyword("delete", true, prefix: true, startsExpr: true);

            Types = new Dictionary<TokenType, TokenInformation>
            {
                {TokenType.Num, num},
                {TokenType.Regexp, regexp},
                {TokenType.String, @string},
                {TokenType.Name, name},
                {TokenType.Eof, eof},

                // Punctuation token types.
                {TokenType.BracketL, bracketL},
                {TokenType.BracketR, bracketR},
                {TokenType.BraceL, braceL},
                {TokenType.BraceR, braceR},
                {TokenType.ParenL, parenL},
                {TokenType.ParenR, parenR},
                {TokenType.Comma, comma},
                {TokenType.Semi, semi},
                {TokenType.Colon, colon},
                {TokenType.Dot, dot},
                {TokenType.Question, question},
                {TokenType.Arrow, arrow},
                {TokenType.Template, template},
                {TokenType.InvalidTemplate, invalidTemplate},
                {TokenType.Ellipsis, ellipsis},
                {TokenType.BackQuote, backQuote},
                {TokenType.DollarBraceL, dollarBraceL},

                // Operators. These carry several kinds of properties to help the
                // parser use them properly (the presence of these properties is
                // what categorizes them as operators).
                //
                // `binop`, when present, specifies that this operator is a binary
                // operator, and will refer to its precedence.
                //
                // `prefix` and `postfix` mark the operator as a prefix or postfix
                // unary operator.
                //
                // `isAssign` marks all of `=`, `+=`, `-=` etcetera, which act as
                // binary operators with a very low precedence, that should result
                // in AssignmentExpression nodes.

                {TokenType.Eq, eq},
                {TokenType.Assign, assign},
                {TokenType.IncDec, incDec},
                {TokenType.Prefix, prefix},
                {TokenType.LogicalOr, logicalOr},
                {TokenType.LogicalAnd, logicalAnd},
                {TokenType.BitwiseOr, bitwiseOr},
                {TokenType.BitwiseXor, bitwiseXor},
                {TokenType.BitwiseAnd, bitwiseAnd},
                {TokenType.Equality, equality},
                {TokenType.Relational, relational},
                {TokenType.BitShift, bitShift},
                {TokenType.PlusMin, plusMin},
                {TokenType.Modulo, modulo},
                {TokenType.Star, star},
                {TokenType.Slash, slash},
                {TokenType.Starstar, starstar},

                // Keyword token types.
                {TokenType.Break, _break},
                {TokenType.Case, _case},
                {TokenType.Catch, _catch},
                {TokenType.Continue, _continue},
                {TokenType.Debugger, debugger},
                {TokenType.Default, _default},
                {TokenType.Do, _do},
                {TokenType.Else, _else},
                {TokenType.Finally, _finally},
                {TokenType.For, _for},
                {TokenType.Function, function},
                {TokenType.If, _if},
                {TokenType.Return, _return},
                {TokenType.Switch, _switch},
                {TokenType.Throw, _throw},
                {TokenType.Try, _try},
                {TokenType.Var, var},
                {TokenType.Const, _const},
                {TokenType.While, _while},
                {TokenType.With, with},
                {TokenType.New, _new},
                {TokenType.This, _this},
                {TokenType.Super, super},
                {TokenType.Class, _class},
                {TokenType.Extends, extends},
                {TokenType.Export, export},
                {TokenType.Import, import},
                {TokenType.Null, _null},
                {TokenType.True, _true},
                {TokenType.False, _false},
                {TokenType.In, _in},
                {TokenType.Instanceof, instanceof},
                {TokenType.Typeof, _typeof},
                {TokenType.Void, _void},
                {TokenType.Delete, delete}
            };

            // Token-specific context update code
            parenR.UpdateContext = braceR.UpdateContext = Parser.ParenBraceRUpdateContext;
            braceL.UpdateContext = Parser.BraceLUpdateContext;
            dollarBraceL.UpdateContext = Parser.DollarBraceLUpdateContext;
            parenL.UpdateContext = Parser.ParenLUpdateContext;
            incDec.UpdateContext = Parser.IncDecUpdateContext;
            function.UpdateContext = _class.UpdateContext = Parser.FunctionClassUpdateContext;
            backQuote.UpdateContext = Parser.BackQuoteUpdateContext;
            star.UpdateContext = Parser.StarUpdateContext;
            name.UpdateContext = Parser.NameUpdateContext;
        }

        public TokenInformation(string? keyword = null,
            bool beforeExpr = false,
            bool startsExpr = false,
            bool isLoop = false,
            bool prefix = false,
            bool postfix = false,
            int binaryOperation = -1)
        {
            Keyword = keyword;
            BeforeExpression = beforeExpr;
            StartsExpression = startsExpr;
            IsLoop = isLoop;
            Prefix = prefix;
            Postfix = postfix;
            BinaryOperation = binaryOperation;
            UpdateContext = null;
        }

        static TokenInformation CreateBinaryOperation(int prec)
        {
            return new TokenInformation(beforeExpr: true, binaryOperation: prec);
        }

        // Succinct definitions of keyword token types
        static TokenInformation CreateKeyword(string keyword,
            bool beforeExpr = false,
            bool startsExpr = false,
            bool isLoop = false,
            bool prefix = false,
            bool postfix = false,
            int binop = -1)
        {
            return new TokenInformation(keyword, beforeExpr, startsExpr, isLoop, prefix, postfix, binop);
        }

        public string? Keyword { get; }
        public bool BeforeExpression { get; }
        public bool StartsExpression { get; }
        public bool IsLoop { get; }
        public bool Prefix { get; }
        public bool Postfix { get; }
        public int BinaryOperation { get; }
        public Action<Parser, TokenType>? UpdateContext { get; internal set; }
    }
}
