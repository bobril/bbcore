using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Lib.DiskCache;
using Lib.Spriter;
using Lib.Utils;
using Lib.Utils.Logger;
using Njsast.Bobril;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;

namespace Lib.TSCompiler
{
    public class SpriteHolder : ISpritePlace
    {
        IDiskCache _dc;
        ILogger _logger;
        Sprite2dPlacer _placer;
        List<OutputSprite> _allSprites;
        List<OutputSprite> _newSprites;
        IReadOnlyList<ImageBytesWithQuality> _result;
        Dictionary<string, TSFileAdditionalInfo> _imageCache = new Dictionary<string, TSFileAdditionalInfo>();
        bool _wasChange;

        public SpriteHolder(IDiskCache dc, ILogger logger)
        {
            _dc = dc;
            _logger = logger;
            _placer = new Sprite2dPlacer();
            _allSprites = new List<OutputSprite>();
            _newSprites = new List<OutputSprite>();
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
                if (sprite.Name != null && sprite.Height == -1 && sprite.Width == -1)
                {
                    if (FindSprite(_allSprites, sprite) < 0 && FindSprite(_newSprites, sprite) < 0)
                        _newSprites.Add(new OutputSprite { Me = sprite });
                }
            }
        }

        public void ProcessNew()
        {
            for (int i = 0; i < _allSprites.Count; i++)
            {
                var sprite = _allSprites[i];
                _allSprites[i] = ProcessOneSprite(sprite.Me);
            }
            if (_newSprites.Count == 0)
                return;
            _wasChange = true;
            for (int i = 0; i < _newSprites.Count; i++)
            {
                var sprite = _newSprites[i];
                _newSprites[i] = ProcessOneSprite(sprite.Me);
            }
            _newSprites.Sort((l, r) => r.oheight.CompareTo(l.oheight));
            for (int i = 0; i < _newSprites.Count; i++)
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
            if (dirc is IDirectoryCache)
            {
                _dc.UpdateIfNeeded((IDirectoryCache)dirc);
                foreach (var item in (IDirectoryCache)dirc)
                {
                    if (!item.IsFile || item.IsInvalid) continue;
                    var (Name, Quality) = PathUtils.ExtractQuality(item.Name);
                    if (Name.AsSpan().SequenceEqual(fnF))
                    {
                        if (!_imageCache.TryGetValue(item.FullPath,out var fi))
                        {
                            fi = TSFileAdditionalInfo.Create(item as IFileCache, _dc);
                            _imageCache.Add(item.FullPath, fi);
                        }
                        if (fi.ImageCacheId != item.ChangeId)
                        {
                            _wasChange = true;
                            try
                            {
                                fi.Image = Image.Load((item as IFileCache).ByteContent);
                            }
                            catch (Exception ex)
                            {
                                _logger.Error("Failed to load sprite " + item.FullPath + " as image. " + ex.Message);
                                continue;
                            }
                            fi.ImageCacheId = item.ChangeId;
                        }
                        slices.Add(new SpriteSlice { name = item.Name, quality = Quality, width = fi.Image.Width, height = fi.Image.Height });
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
            return new OutputSprite();
        }

        public List<OutputSprite> Retrieve(List<SourceInfo.Sprite> sprites)
        {
            var res = new List<OutputSprite>(sprites.Count);
            for (int i = 0; i < sprites.Count; i++)
            {
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

        public struct ImageBytesWithQuality
        {
            public float Quality;
            public byte[] Content;
        }

        public IReadOnlyList<ImageBytesWithQuality> BuildImage(bool maxCompression)
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
                var resultImage = new Image<Rgba32>((int)Math.Ceiling(_placer.Dim.Width * q), (int)Math.Ceiling(_placer.Dim.Height * q));
                resultImage.Mutate(c =>
                {
                    for (int j = 0; j < _allSprites.Count; j++)
                    {
                        var sprite = _allSprites[j];
                        var fn = sprite.Me.Name;
                        var slice = FindBestSlice(sprite.slices, q);
                        var fi = _imageCache[PathUtils.InjectQuality(fn, slice.quality)];
                        if (fi != null)
                        {
                            var image = fi.Image;
                            if (sprite.Me.Color != null)
                            {
                                var rgbColor = ParseColor(sprite.Me.Color);
                                image = image.Clone(operation =>
                                {
                                    operation.ApplyProcessor(new Recolor(rgbColor));
                                });
                            }
                            image = image.Clone(operation =>
                            {
                                if (q != slice.quality)
                                    operation = operation.Resize((int)Math.Round(image.Width * q / slice.quality), (int)Math.Round(image.Height * q / slice.quality));
                                operation.Crop(new Rectangle(new Point((int)(q * Math.Max(0,sprite.Me.X)), (int)(q * Math.Max(0,sprite.Me.Y))), new Size((int)(sprite.owidth * q), (int)(sprite.oheight * q))));
                            });
                            c.DrawImage(image, new Point((int)(sprite.ox * q), (int)(sprite.oy * q)), new GraphicsOptions());
                        }
                    }
                });
                var ms = new MemoryStream();
                resultImage.Save(ms, new SixLabors.ImageSharp.Formats.Png.PngEncoder { CompressionLevel = maxCompression ? 9 : 1 });
                result[i].Content = ms.ToArray();
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

        static Regex _rgbaColorParser = new Regex(@"\s*rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d+|\d*\.\d+)\s*\)\s*", RegexOptions.ECMAScript);

        public static Rgba32 ParseColor(string color)
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
                return new Rgba32(
                    (byte)int.Parse(color.Substring(1, 2), NumberStyles.HexNumber),
                    (byte)int.Parse(color.Substring(3, 2), NumberStyles.HexNumber),
                    (byte)int.Parse(color.Substring(5, 2), NumberStyles.HexNumber));
            }
            if (color.Length == 9 && color[0] == '#')
            {
                return new Rgba32(
                    (byte)int.Parse(color.Substring(1, 2), NumberStyles.HexNumber),
                    (byte)int.Parse(color.Substring(3, 2), NumberStyles.HexNumber),
                    (byte)int.Parse(color.Substring(5, 2), NumberStyles.HexNumber),
                    (byte)int.Parse(color.Substring(7, 2), NumberStyles.HexNumber));
            }
            var mrgba = _rgbaColorParser.Match(color);
            if (mrgba.Success)
            {
                return new Rgba32(
                    (byte)int.Parse(mrgba.Groups[1].Value),
                    (byte)int.Parse(mrgba.Groups[2].Value),
                    (byte)int.Parse(mrgba.Groups[3].Value),
                    (byte)Math.Round(float.Parse(mrgba.Groups[4].Value, CultureInfo.InvariantCulture) * 255));
            }
            throw new InvalidDataException("Cannot parse color " + color);
        }

        class RealRecolor : IImageProcessor<Rgba32>
        {
            Rgba32 _rgbColor;
            Image<Rgba32> _source;
            Rectangle _sourceRectangle;

            public RealRecolor(Rgba32 rgbColor, Image<Rgba32> source, Rectangle sourceRectangle)
            {
                _rgbColor = rgbColor;
                _source = source;
                _sourceRectangle = sourceRectangle;
            }

            public void Execute()
            {
                var cgray = new Bgr24(128, 128, 128);
                var frame = _source.Frames.RootFrame;
                if (_rgbColor.A == 255)
                {
                    for (var y = 0; y < frame.Height; y++)
                    for (var x = 0; x < frame.Width; x++)
                    {
                        var c = frame[x, y];
                        if (cgray.Equals(c.Bgr))
                        {
                            var alpha = c.A;
                            c = _rgbColor;
                            c.A = alpha;
                            frame[x, y] = c;
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < frame.Height; y++)
                    for (int x = 0; x < frame.Width; x++)
                    {
                        var c = frame[x, y];
                        if (cgray.Equals(c.Bgr))
                        {
                            var alpha = c.A;
                            c = _rgbColor;
                            c.A = (byte)(((c.A * alpha) * 32897) >> 23); // clever divide by 255
                            frame[x, y] = c;
                        }
                    }
                }
            }

            public void Dispose()
            {
            }
        }

        class Recolor : IImageProcessor
        {
            Rgba32 _rgbColor;

            public Recolor(Rgba32 rgbColor)
            {
                _rgbColor = rgbColor;
            }

            public IImageProcessor<TPixel> CreatePixelSpecificProcessor<TPixel>(SixLabors.ImageSharp.Configuration configuration, Image<TPixel> source,
                Rectangle sourceRectangle) where TPixel : struct, IPixel<TPixel>
            {
                if (typeof(TPixel)==typeof(Rgba32))
                    return Unsafe.As<IImageProcessor<TPixel>>(new RealRecolor(_rgbColor, Unsafe.As<Image<Rgba32>>(source), sourceRectangle));
                throw new NotSupportedException();
            }
        }
    }
}
