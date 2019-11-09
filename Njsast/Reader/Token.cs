namespace Njsast.Reader
{
    public struct Token
    {
        public TokenType Type { get; }
        public object? Value { get; }
        public SourceLocation Location { get; }

        public Token(TokenType type, object? value, SourceLocation location)
        {
            Type = type;
            Value = value;
            Location = location;
        }
    }
}
