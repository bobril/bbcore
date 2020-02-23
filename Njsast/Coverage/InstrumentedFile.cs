using System;
using System.Linq;
using System.Text.Json.Serialization;
using Njsast.Utils;

namespace Njsast.Coverage
{
    [JsonConverter(typeof(InstrumentedFileConverter))]
    public class InstrumentedFile
    {
        public string FileName;
        public StructList<InstrumentedInfo> Infos;

        public InstrumentedFile(string name)
        {
            FileName = name;
        }

        public void AddInfo(InstrumentedInfo info)
        {
            foreach (var ii in Infos)
            {
                if (info.Start <= ii.Start && ii.End <= info.Start)
                {
                    if (info.Start == ii.Start)
                    {
                        info.Start = ii.End;
                    }
                    else
                    {
                        info.End = ii.Start;
                    }
                }
                else if (ii.Start <= info.Start && info.End <= ii.End)
                {
                    if (ii.Start == info.Start)
                    {
                        ii.Start = info.End;
                    }
                    else
                    {
                        ii.End = info.Start;
                    }
                }
            }

            if (info.Start < info.End)
                Infos.Add(info);
        }

        public void Sort()
        {
            for (int i = 0; i < Infos.Count; i++)
            {
                if (Infos[i].Start == Infos[i].End)
                {
                    Infos.RemoveAt(i);
                    i--;
                }
            }

            Infos = new StructList<InstrumentedInfo>(Infos.OrderBy(i => i.Start).ToArray());
        }

        public void PruneWhiteSpace(ReadOnlySpan<byte> content)
        {
            var pos = new LineCol(0, 0);
            foreach (var info in Infos)
            {
                var wasWhiteSpace = true;
                while (pos < info.Start && !content.IsEmpty) AdvanceRune(ref content, ref pos, out wasWhiteSpace);
                if (content.IsEmpty) return;
                if (pos != info.Start) continue;
                var firstNonWhiteSpace = pos;
                while (pos < info.End && !content.IsEmpty && wasWhiteSpace)
                {
                    firstNonWhiteSpace = pos;
                    AdvanceRune(ref content, ref pos, out wasWhiteSpace);
                }

                if (pos >= info.End) continue;
                info.Start = firstNonWhiteSpace;
                var lastNonWhiteSpace = pos;
                while (pos < info.End && !content.IsEmpty)
                {
                    AdvanceRune(ref content, ref pos, out wasWhiteSpace);
                    if (!wasWhiteSpace) lastNonWhiteSpace = pos;
                }

                info.End = lastNonWhiteSpace;
            }
        }

        static void AdvanceRune(ref ReadOnlySpan<byte> content, ref LineCol pos, out bool wasWhiteSpace)
        {
            oneMoreTime:
            System.Text.Rune.DecodeFromUtf8(content, out var rune, out var consumed);
            content = content.Slice(consumed);
            if (rune.Value == 13)
            {
                if (content.IsEmpty)
                {
                    pos = new LineCol(pos.Line + 1, 0);
                    wasWhiteSpace = true;
                    return;
                }

                goto oneMoreTime;
            }

            if (rune.Value == 10)
            {
                pos = new LineCol(pos.Line + 1, 0);
                wasWhiteSpace = true;
                return;
            }

            wasWhiteSpace = rune.Value == 32 || rune.Value == 9;
            pos = new LineCol(pos.Line, pos.Col + 1);
        }
    }
}
