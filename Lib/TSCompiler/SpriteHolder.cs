using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Lib.DiskCache;
using Lib.Spriter;
using Lib.Utils;
using Lib.Utils.Logger;
using Njsast.Bobril;
using SkiaSharp;

namespace Lib.TSCompiler;

public class SpriteHolder : ISpritePlace, ISpriteGenerator
{
    readonly IDiskCache _dc;
    readonly ILogger _logger;
    readonly Sprite2dPlacer _placer;
    readonly List<OutputSprite> _allSprites;
    readonly List<OutputSprite> _newSprites;
    IReadOnlyList<ImageBytesWithQuality>? _result;
    readonly Dictionary<string, CachedImage> _imageCache = new();
    bool _wasChange;

    public SpriteHolder(IDiskCache dc, ILogger logger)
    {
        _dc = dc;
        _logger = logger;
        _placer = new();
        _allSprites = new();
        _newSprites = new();
    }

    int _sX, _sY, _sW, _sH;

    int ISpritePlace.Width => _sW;
    int ISpritePlace.Height => _sH;
    int ISpritePlace.X { get => _sX; set => _sX = value; }
    int ISpritePlace.Y { get => _sY; set => _sY = value; }

    static int FindSprite(List<OutputSprite> where, in SourceInfo.Sprite what)
    {
        for (int i = 0; i < where.Count; i++)
        {
            var item = where[i];
            if (item.Me.Name == what.Name && item.Me.Color == what.Color)
                return i;
        }
        return -1;
    }

    public void Process(List<SourceInfo.Sprite> sprites)
    {
        foreach (var sprite in sprites)
        {
            if (sprite.Name != null && !sprite.IsSvg() && sprite.Height == -1 && sprite.Width == -1)
            {
                if (FindSprite(_allSprites, sprite) < 0 && FindSprite(_newSprites, sprite) < 0)
                    _newSprites.Add(new() { Me = sprite });
            }
        }
    }

    public void ProcessNew()
    {
        for (var i = 0; i < _allSprites.Count; i++)
        {
            var sprite = _allSprites[i];
            ProcessOneSprite(sprite.Me);
        }
        if (_newSprites.Count == 0)
            return;
        _wasChange = true;
        for (var i = 0; i < _newSprites.Count; i++)
        {
            var sprite = _newSprites[i];
            _newSprites[i] = ProcessOneSprite(sprite.Me);
        }
        _newSprites.Sort((l, r) => r.oheight.CompareTo(l.oheight));
        for (var i = 0; i < _newSprites.Count; i++)
        {
            var sprite = _newSprites[i];
            _sW = sprite.owidth;
            _sH = sprite.oheight;
            _placer.Add(this);
            sprite.ox = _sX;
            sprite.oy = _sY;
            _allSprites.Add(sprite);
        }
        _newSprites.Clear();
    }

    OutputSprite ProcessOneSprite(SourceInfo.Sprite sprite)
    {
        var fn = sprite.Name;
        var fnD = PathUtils.SplitDirAndFile(fn, out var fnF);
        var dirc = _dc.TryGetItem(fnD);
        var slices = new List<SpriteSlice>();
        if (dirc is IDirectoryCache directoryCache)
        {
            _dc.WatchDirectChildren(directoryCache, null, true, false);
            foreach (var item in directoryCache)
            {
                if (!item.IsFile || item.IsInvalid) continue;
                var (Name, Quality) = PathUtils.ExtractQuality(item.Name);
                if (Name.AsSpan().SequenceEqual(fnF))
                {
                    if (!_imageCache.TryGetValue(item.FullPath, out var image) || image.ChangeId != item.ChangeId)
                    {
                        _wasChange = true;
                        try
                        {
                            image = CachedImage.Load(((IFileCache)item).ByteContent, item.ChangeId);
                            _imageCache[item.FullPath] = image;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("Failed to load sprite " + item.FullPath + " as image. " + ex.Message);
                            continue;
                        }
                    }
                    slices.Add(new() { name = item.Name, quality = Quality, width = image.Width, height = image.Height });
                }
            }
            slices.Sort((l, r) => l.quality < r.quality ? -1 : l.quality > r.quality ? 1 : 0);
            var res = new OutputSprite
            {
                Me = sprite,
                slices = slices.ToArray()
            };
            if (slices.Count > 0)
            {
                res.owidth = (int)(slices[0].width / slices[0].quality + 0.5);
                res.oheight = (int)(slices[0].height / slices[0].quality + 0.5);
            }
            else
            {
                res.owidth = 0;
                res.oheight = 0;
            }
            return res;
        }
        return new();
    }

    public List<OutputSprite> Retrieve(List<SourceInfo.Sprite> sprites)
    {
        var res = new List<OutputSprite>(sprites.Count);
        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i].IsSvg()) continue;
            var sprite = new OutputSprite { Me = sprites[i] };
            if (sprite.Me.Name != null && sprite.Me.Height == -1 && sprite.Me.Width == -1)
            {
                var idx = FindSprite(_allSprites, sprite.Me);
                var s = _allSprites[idx];
                if (sprite.Me.Height >= 0)
                    sprite.oheight = Math.Min(s.oheight, sprite.Me.Height);
                else
                    sprite.oheight = s.oheight;
                if (sprite.Me.Width >= 0)
                    sprite.owidth = Math.Min(s.owidth, sprite.Me.Width);
                else
                    sprite.owidth = s.owidth;
                sprite.ox = s.ox + Math.Max(0, Math.Min(sprite.Me.X, s.owidth - sprite.owidth));
                sprite.oy = s.oy + Math.Max(0, Math.Min(sprite.Me.Y, s.oheight - sprite.oheight));
            }
            res.Add(sprite);
        }
        return res;
    }

    public IReadOnlyList<ImageBytesWithQuality>? BuildImage(bool maxCompression)
    {
        if (!_wasChange)
        {
            return _result;
        }
        var qualities = new HashSet<float>();
        var i = 0;
        for (i = 0; i < _allSprites.Count; i++)
        {
            var slices = _allSprites[i].slices;
            foreach (var s in slices)
            {
                qualities.Add(s.quality);
            }
        }
        var result = new ImageBytesWithQuality[qualities.Count];
        i = 0;
        foreach (var q in qualities.OrderBy(a => a))
        {
            result[i++].Quality = q;
        }
        for (i = 0; i < result.Length; i++)
        {
            var q = result[i].Quality;
            using var resultImage = new SKBitmap(new SKImageInfo(
                (int)Math.Ceiling(_placer.Dim.Width * q), (int)Math.Ceiling(_placer.Dim.Height * q),
                SKColorType.Rgba8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(resultImage))
            {
                canvas.Clear(SKColors.Transparent);
                foreach (var sprite in _allSprites)
                {
                    var slice = FindBestSlice(sprite.slices, q);
                    var cached = _imageCache[PathUtils.InjectQuality(sprite.Me.Name!, slice.quality)];
                    using var image = cached.CreateBitmap();
                    if (sprite.Me.Color != null)
                        Recolor(image.GetPixelSpan(), ParseColor(sprite.Me.Color));

                    // Keep the original resize-then-crop order and integer rounding.
                    using var resized = q != slice.quality
                        ? image.Resize(new SKImageInfo(
                                (int)Math.Round(image.Width * q / slice.quality),
                                (int)Math.Round(image.Height * q / slice.quality),
                                SKColorType.Rgba8888, SKAlphaType.Premul),
                            new SKSamplingOptions(SKCubicResampler.CatmullRom))
                            ?? throw new InvalidDataException("Cannot resize sprite " + sprite.Me.Name)
                        : null;
                    var bitmap = resized ?? image;
                    var x = (int)(q * Math.Max(0, sprite.Me.X));
                    var y = (int)(q * Math.Max(0, sprite.Me.Y));
                    var width = (int)(sprite.owidth * q);
                    var height = (int)(sprite.oheight * q);
                    if (width <= 0 || height <= 0 || x + width > bitmap.Width || y + height > bitmap.Height)
                        throw new InvalidDataException("Invalid crop rectangle for sprite " + sprite.Me.Name);
                    using var drawable = SKImage.FromBitmap(bitmap);
                    canvas.DrawImage(drawable, SKRect.Create(x, y, width, height),
                        SKRect.Create((int)(sprite.ox * q), (int)(sprite.oy * q), width, height),
                        new SKSamplingOptions(SKFilterMode.Nearest));
                }
            }
            using var pixels = resultImage.PeekPixels();
            using var encoded = pixels.Encode(new SKPngEncoderOptions(
                SKPngEncoderFilterFlags.AllFilters, maxCompression ? 9 : 1))
                ?? throw new InvalidDataException("Cannot encode sprite atlas as PNG.");
            result[i].Content = encoded.ToArray();
        }
        _wasChange = false;
        _result = result;
        return _result;
    }

    SpriteSlice FindBestSlice(SpriteSlice[] slices, float quality)
    {
        for (var i = 0; i < slices.Length; i++)
        {
            if (slices[i].quality >= quality) return slices[i];
        }
        return slices.Last();
    }

    static readonly Regex RgbaColorParser = new(@"\s*rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d+|\d*\.\d+)\s*\)\s*", RegexOptions.ECMAScript);

    public static SKColor ParseColor(string color)
    {
        if (color.Length == 4 && color[0] == '#')
        {
            color = "#" + color[1] + color[1] + color[2] + color[2] + color[3] + color[3];
        }
        if (color.Length == 5 && color[0] == '#')
        {
            color = "#" + color[1] + color[1] + color[2] + color[2] + color[3] + color[3] + color[4] + color[4];
        }
        if (color.Length == 7 && color[0] == '#')
        {
            return new(
                (byte)int.Parse(color.Substring(1, 2), NumberStyles.HexNumber),
                (byte)int.Parse(color.Substring(3, 2), NumberStyles.HexNumber),
                (byte)int.Parse(color.Substring(5, 2), NumberStyles.HexNumber));
        }
        if (color.Length == 9 && color[0] == '#')
        {
            return new(
                (byte)int.Parse(color.Substring(1, 2), NumberStyles.HexNumber),
                (byte)int.Parse(color.Substring(3, 2), NumberStyles.HexNumber),
                (byte)int.Parse(color.Substring(5, 2), NumberStyles.HexNumber),
                (byte)int.Parse(color.Substring(7, 2), NumberStyles.HexNumber));
        }
        var mrgba = RgbaColorParser.Match(color);
        if (mrgba.Success)
        {
            return new(
                (byte)int.Parse(mrgba.Groups[1].Value),
                (byte)int.Parse(mrgba.Groups[2].Value),
                (byte)int.Parse(mrgba.Groups[3].Value),
                (byte)Math.Round(float.Parse(mrgba.Groups[4].Value, CultureInfo.InvariantCulture) * 255));
        }
        throw new InvalidDataException("Cannot parse color " + color);
    }

    // Pixels must be RGBA8888 with straight (unpremultiplied) alpha. Comparing after
    // premultiplication would lose the exact 128 marker, especially at low alpha.
    internal static void Recolor(Span<byte> pixels, SKColor color)
    {
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 128 || pixels[i + 1] != 128 || pixels[i + 2] != 128)
                continue;
            pixels[i] = color.Red;
            pixels[i + 1] = color.Green;
            pixels[i + 2] = color.Blue;
            pixels[i + 3] = (byte)(((color.Alpha * pixels[i + 3]) * 32897) >> 23);
        }
    }

    // Cache managed pixel data, not native bitmaps, so project/watch cache lifetimes
    // cannot retain unmanaged image allocations. Every Skia object is short-lived.
    sealed class CachedImage(int width, int height, byte[] pixels, int changeId)
    {
        public int Width => width;
        public int Height => height;
        public int ChangeId => changeId;

        public static CachedImage Load(byte[] content, int changeId)
        {
            using var data = SKData.CreateCopy(content);
            using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Unsupported image format.");
            using var bitmap = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height,
                SKColorType.Rgba8888, SKAlphaType.Unpremul));
            var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
            if (result != SKCodecResult.Success)
                throw new InvalidDataException("Cannot decode image: " + result);
            return new(bitmap.Width, bitmap.Height, bitmap.GetPixelSpan().ToArray(), changeId);
        }

        public SKBitmap CreateBitmap()
        {
            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            pixels.AsSpan().CopyTo(bitmap.GetPixelSpan());
            return bitmap;
        }
    }
}
