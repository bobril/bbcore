namespace Njsast.Reader
{
    public struct Token
    {
        object _value;
        SourceLocation _location;

        public Token(TokenType type, object value, SourceLocation location)
        {
            Type = type;
            _value = value;
            _location = location;
        }

        public TokenType Type { get; }
    }
}
