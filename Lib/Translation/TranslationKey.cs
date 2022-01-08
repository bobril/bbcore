using System;

namespace Lib.Translation;

public readonly struct TranslationKey : IEquatable<TranslationKey>
{
    public TranslationKey(string message, string? hint, bool withParams)
    {
        Message = message;
        Hint = hint;
        WithParams = withParams;
    }

    public readonly string Message;
    public readonly string? Hint;
    public readonly bool WithParams;

    public bool Equals(TranslationKey other)
    {
        return Message == other.Message && Hint == other.Hint && WithParams == other.WithParams;
    }

    public override int GetHashCode()
    {
        return Message.GetHashCode() * 31 + (Hint?.Length ?? 0) * 2 + (WithParams ? 1 : 0);
    }
}