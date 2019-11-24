namespace Njsast.Reader
{
    public sealed partial class Parser
    {
        // This function is used to raise exceptions on parse errors. It
        // takes an offset integer (into the current `input`) to indicate
        // the location of the error, attaches the position to the end
        // of the error message, and then raises a `SyntaxError` with that
        // message.
        static void Raise(Position position, string message)
        {
            throw NewSyntaxError(position, message);
        }

        public static SyntaxError NewSyntaxError(Position position, string message)
        {
            message += " (" + (position.Line + 1) + ":" + (position.Column + 1) + ")";
            return new SyntaxError(message, position);
        }

        static void RaiseRecoverable(Position position, string message)
        {
            Raise(position, message);
        }

        public Position CurPosition()
        {
            return new Position(_pos.Line, _pos.Column, _pos.Index);
        }
    }
}
