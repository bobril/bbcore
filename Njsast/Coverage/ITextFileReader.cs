using System;

namespace Njsast.Coverage
{
    public interface ITextFileReader
    {
        ReadOnlySpan<byte> ReadUtf8(string fileName);
    }
}