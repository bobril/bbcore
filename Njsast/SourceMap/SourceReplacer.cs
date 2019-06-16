using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Njsast.SourceMap
{
    public class SourceReplacer : ISourceReplacer
    {
        struct Modification
        {
            public int FromLine;
            public int FromCol;
            public int ToLine;
            public int ToCol;
            public string Content;
        }

        StructList<Modification> _modifications = new StructList<Modification>();

        public void Apply(ISourceAdder sourceAdder)
        {
            int curLine = 0;
            int curCol = 0;
            for (var i = 0u; i < _modifications.Count; i++)
            {
                ref var m = ref _modifications[i];
                if (curLine != m.FromLine || curCol != m.FromCol)
                {
                    sourceAdder.Add(curLine, curCol, m.FromLine, m.FromCol);
                }
                if (!string.IsNullOrEmpty(m.Content))
                {
                    sourceAdder.Add(m.Content);
                }
                curLine = m.ToLine;
                curCol = m.ToCol;
            }
            sourceAdder.Add(curLine, curCol, int.MaxValue, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong MakeOne(int line, int col) => ((ulong)line << 32) + (ulong)col;

        public void Replace(int fromLine, int fromCol, int toLine, int toCol, string content)
        {
            if (fromLine == toLine && fromCol == toCol && string.IsNullOrEmpty(content))
                return;
            var l = 0u;
            var r = _modifications.Count;
            while (l < r)
            {
                var m = (l + r) >> 1;
                ref var mid = ref _modifications[m];
                if (MakeOne(fromLine, fromCol) < MakeOne(mid.FromLine, mid.FromCol))
                {
                    r = m;
                }
                else
                {
                    Debug.Assert(MakeOne(fromLine, fromCol) != MakeOne(mid.FromLine, mid.FromCol));
                    l = m + 1;
                }
            }
            ref var inserted = ref _modifications.Insert(l);
            inserted.FromLine = fromLine;
            inserted.FromCol = fromCol;
            inserted.ToLine = toLine;
            inserted.ToCol = toCol;
            inserted.Content = content;
        }
    }
}
