using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Njsast.SourceMap
{
    public class SourceReplacer : ISourceReplacer
    {
        struct Pos : IEquatable<Pos>
        {
            public Pos(int line, int col)
            {
                Line = line;
                Col = col;
            }

            public readonly int Line;
            public readonly int Col;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            ulong Key() => ((ulong) Line << 32) + (ulong) Col;

            public bool Equals(Pos other)
            {
                return Line == other.Line && Col == other.Col;
            }

            public override bool Equals(object? obj)
            {
                return obj is Pos other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Line, Col);
            }

            public static bool operator ==(Pos left, Pos right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(Pos left, Pos right)
            {
                return !left.Equals(right);
            }

            public static bool operator <(Pos left, Pos right)
            {
                return left.Key() < right.Key();
            }

            public static bool operator >(Pos left, Pos right)
            {
                return left.Key() > right.Key();
            }

            public static bool operator <=(Pos left, Pos right)
            {
                return left.Key() <= right.Key();
            }

            public static bool operator >=(Pos left, Pos right)
            {
                return left.Key() >= right.Key();
            }
        }

        struct Modification
        {
            public Pos From;
            public Pos To;
            public Pos Start;
            public Pos End;
            public string? Content;

            public bool IsUseless => From == To && Start == End && string.IsNullOrEmpty(Content);
        }

        StructList<Modification> _modifications;

        public void Apply(ISourceAdder sourceAdder)
        {
            var cur = new Pos(0, 0);
            for (var i = 0u; i < _modifications.Count; i++)
            {
                ref var m = ref _modifications[i];
                if (cur != m.From)
                {
                    Debug.Assert(cur < m.From);
                    sourceAdder.Add(cur.Line, cur.Col, m.From.Line, m.From.Col);
                }

                if (m.Start != m.End)
                {
                    sourceAdder.Add(m.Start.Line, m.Start.Col, m.End.Line, m.End.Col);
                }

                if (!string.IsNullOrEmpty(m.Content))
                {
                    sourceAdder.Add(m.Content);
                }

                cur = m.To;
            }

            sourceAdder.Add(cur.Line, cur.Col, int.MaxValue, 0);
            sourceAdder.FlushLine();
        }

        public void Replace(int fromLine, int fromCol, int toLine, int toCol, string? content)
        {
            var removeFrom = new Pos(fromLine, fromCol);
            var removeTo = new Pos(toLine, toCol);
            Debug.Assert(removeFrom <= removeTo);
            if (removeFrom == removeTo && string.IsNullOrEmpty(content))
                return;
            var l = 0;
            for (var i = 0; i < _modifications.Count; i++)
            {
                ref var m = ref _modifications[i];
                if (removeFrom >= m.From) l = i + 1;
                if (m.Start <= removeFrom && removeTo <= m.End)
                {
                    if (m.Start == removeFrom)
                    {
                        if (removeTo == m.End)
                        {
                            m.Start = new Pos(0, 0);
                            m.End = new Pos(0, 0);
                            if (!string.IsNullOrEmpty(content))
                                m.Content = string.IsNullOrEmpty(m.Content) ? content : content + m.Content;
                            if (m.IsUseless)
                                _modifications.RemoveAt(i);
                            return;
                        }

                        if (!string.IsNullOrEmpty(content))
                        {
                            ref var inserted2 = ref _modifications.Insert(i);
                            m = ref _modifications[++i];
                            inserted2.From = m.From;
                            inserted2.To = m.From;
                            inserted2.Start = new Pos(0, 0);
                            inserted2.End = new Pos(0, 0);
                            inserted2.Content = content;
                        }

                        m.Start = removeTo;
                        return;
                    }

                    if (removeTo == m.End)
                    {
                        m.End = removeFrom;
                        if (!string.IsNullOrEmpty(content))
                            m.Content = string.IsNullOrEmpty(m.Content) ? content : content + m.Content;
                        return;
                    }

                    ref var inserted3 = ref _modifications.Insert(i);
                    m = ref _modifications[++i];
                    inserted3.From = m.From;
                    inserted3.To = m.From;
                    inserted3.Start = m.Start;
                    inserted3.End = removeFrom;
                    inserted3.Content = content;
                    m.Start = removeTo;
                    return;
                }

                Debug.Assert(removeTo <= m.Start || m.End <= removeFrom);
            }

            ref var inserted = ref _modifications.Insert(l);
            inserted.From = removeFrom;
            inserted.To = removeTo;
            inserted.Start = new Pos(0, 0);
            inserted.End = new Pos(0, 0);
            inserted.Content = content;
        }

        public void Move(int fromLine, int fromCol, int toLine, int toCol, int placeLine, int placeCol)
        {
            var from = new Pos(fromLine, fromCol);
            var to = new Pos(toLine, toCol);
            var place = new Pos(placeLine, placeCol);
            if (from == to || from == place)
                return;
            var l = 0u;
            var r = _modifications.Count;
            while (l < r)
            {
                var m = (l + r) >> 1;
                ref var mid = ref _modifications[m];
                if (place < mid.From)
                {
                    r = m;
                }
                else
                {
                    l = m + 1;
                }
            }

            ref var inserted = ref _modifications.Insert(l);
            inserted.From = place;
            inserted.To = place;
            inserted.Start = from;
            inserted.End = to;
            inserted.Content = null;

            l = 0u;
            r = _modifications.Count;
            while (l < r)
            {
                var m = (l + r) >> 1;
                ref var mid = ref _modifications[m];
                if (from < mid.From)
                {
                    r = m;
                }
                else
                {
                    l = m + 1;
                }
            }

            inserted = ref _modifications.Insert(l);
            inserted.From = from;
            inserted.To = to;
            inserted.Start = new Pos(0, 0);
            inserted.End = new Pos(0, 0);
            inserted.Content = null;
        }
    }
}