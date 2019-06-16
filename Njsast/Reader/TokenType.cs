namespace Njsast.Reader
{
    // The assignment of fine-grained, information-carrying type objects
    // allows the tokenizer to store the information it has about a
    // token in a way that is very cheap for the parser to look up.

    // All token type variables start with an underscore, to make them
    // easy to recognize.

    // The `beforeExpr` property is used to disambiguate between regular
    // expressions and divisions. It is set on all token types that can
    // be followed by an expression (thus, a slash after them would be a
    // regular expression).
    //
    // The `startsExpr` property is used to check if the token ends a
    // `yield` expression. It is set on all token types that either can
    // directly start an expression (like a quotation mark) or can
    // continue an expression (like the body of a string).
    //
    // `isLoop` marks a keyword as starting a loop, which is important
    // to know when parsing a label, in order to allow or disallow
    // continue jumps to that label.
    public enum TokenType
    {
        Eof,
        Num,
        Regexp,
        String,
        Name,

        BracketL,

        BracketR,
        BraceL,
        BraceR,
        ParenL,
        ParenR,
        Comma,
        Semi,
        Colon,
        Dot,
        Question,
        Arrow,
        Template,
        InvalidTemplate,
        Ellipsis,
        BackQuote,
        DollarBraceL,

        Eq,
        Assign,
        IncDec,
        Prefix,
        LogicalOr,
        LogicalAnd,
        BitwiseOr,
        BitwiseXor,
        BitwiseAnd,
        Equality,
        Relational,
        BitShift,
        PlusMin,
        Modulo,
        Star,
        Slash,
        Starstar,

        Break,

        Case,
        Catch,
        Continue,
        Debugger,
        Default,
        Do,
        Else,
        Finally,
        For,
        Function,
        If,
        Return,
        Switch,
        Throw,
        Try,
        Var,
        Const,
        While,
        With,
        New,
        This,
        Super,
        Class,
        Extends,
        Export,
        Import,
        Null,
        True,
        False,
        In,
        Instanceof,
        Typeof,
        Void,
        Delete
    }
}