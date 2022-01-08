using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace Lib.Registry;

public static class TarExtractor
{
    public static async Task ExtractTgzAsync(byte[] source, Func<String, Stream, ulong, Task<bool>> fileCallback)
    {
        var ms = new MemoryStream(source);
        using (var stream = new GZipStream(ms, CompressionMode.Decompress, true))
        {
            await ExtractTarAsync(stream, fileCallback);
        }
    }

    public static int ReadFull(this Stream stream, Span<byte> buffer)
    {
        var res = 0;
        while (!buffer.IsEmpty)
        {
            var r = stream.Read(buffer);
            if (r == 0) return res;
            buffer = buffer[r..];
            res += r;
        }

        return res;
    }

    public static async Task ExtractTarAsync(Stream source, Func<String, Stream, ulong, Task<bool>> fileCallback)
    {
        var pos = 0ul;
        var buffer = new byte[512];
        while (true)
        {
            var read = source.ReadFull(buffer.AsSpan(0, 512));
            if (read == 0)
                return;
            if (read != 512)
                throw new InvalidDataException("Incomplete header in tar");
            pos += (ulong)read;
            var nameEndIndex = buffer.AsSpan().IndexOf((byte) 0);
            if (nameEndIndex < 0 || nameEndIndex > 100) nameEndIndex = 100;
            if (nameEndIndex == 0)
                return;
            var name = Encoding.UTF8.GetString(buffer.AsSpan(0, nameEndIndex));
            if (String.IsNullOrWhiteSpace(name))
                break;
            if (name.StartsWith("../") || name.Contains("/../"))
                throw new InvalidDataException("File name in tar contains up directory " + name);
            var sizeString = Encoding.UTF8.GetString(buffer.AsSpan(124, 12)).Trim((char) 0, ' ');
            var size = Convert.ToUInt64(sizeString, 8);
            pos += size;
            var offset = (512 - (pos & 511)) & 511;
            if (!await fileCallback(name, source, size))
                return;
            if (offset == 0) continue;
            read = source.ReadFull(buffer.AsSpan(0, (int) offset));
            if (read == 0)
                return;
            pos += offset;
        }
    }
}