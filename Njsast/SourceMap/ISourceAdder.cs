using System;

namespace Njsast.SourceMap
{
    public interface ISourceAdder
    {
        void Add(int fromLine, int fromCol, int toLine, int toCol);
        void Add(ReadOnlySpan<char> text);
        void FlushLine();
    }
}
