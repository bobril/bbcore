using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Lib.DiskCache;
using Lib.Spriter;
using Lib.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Drawing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Processing.Transforms;
using SixLabors.Primitives;

namespace Lib.TSCompiler
{
    public class SpriteHolder : ISpritePlace
    {
        IDiskCache _dc;
        Sprite2dPlacer _placer;
        List<SourceInfo.Sprite> _allSprites;
        List<SourceInfo.Sprite> _newSprites;
        IReadOnlyList<ImageBytesWithQuality> _result;
        bool _wasChange;

        public SpriteHolder(IDiskCache dc)
        {
            this._dc = dc;
            _placer = new Sprite2dPlacer();
            _allSprites = new List<SourceInfo.Sprite>();
            _newSprites = new List<SourceInfo.Sprite>();
        }

        int _sX, _sY, _sW, _sH;

        int ISpritePlace.Width => _sW;
        int ISpritePlace.Height => _sH;
        int ISpritePlace.X { get => _sX; set => _sX = value; }
        int ISpritePlace.Y { get => _sY; set => _sY = value; }

        static int FindSprite(List<SourceInfo.Sprite> where, in SourceInfo.Sprite what)
        {
            for (int i = 0; i < where.Count; i++)
            {
                var item = where[i];
                if (item.name == what.name && item.color == what.color)
                    return i;
            }
            return -1;
        }

        public void Process(List<SourceInfo.Sprite> sprites)
        {
            foreach (var sprite in sprites)
            {
                if (sprite.name != null && sprite.oheight == 0 && sprite.owidth == 0)
                {
                    if (FindSprite(_allSprites, sprite) < 0 && FindSprite(_newSprites, sprite) < 0)
                        _newSprites.Add(sprite);
                }
            }
        }

        public void ProcessNew()
        {
            for (int i = 0; i < _allSprites.Count; i++)
            {
                var sprite = _allSprites[i];
                ProcessOneSprite(ref sprite);
                _allSprites[i] = sprite;
            }
            if (_newSprites.Count == 0)
                return;
            _wasChange = true;
            for (int i = 0; i < _newSprites.Count; i++)
            {
                var sprite = _newSprites[i];
                ProcessOneSprite(ref sprite);
                _newSprites[i] = sprite;
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

        void ProcessOneSprite(ref SourceInfo.Sprite sprite)
        {
            var fn = sprite.name;
            var fnSplit = PathUtils.SplitDirAndFile(fn);
            var dirc = _dc.TryGetItem(fnSplit.Item1);
            var slices = new List<SpriteSlice>();
            if (dirc is IDirectoryCache)
            {
                _dc.UpdateIfNeeded((IDirectoryCache)dirc);
                foreach (var item in (IDirectoryCache)dirc)
                {
                    if (!item.IsFile || item.IsInvalid) continue;
                    var (Name, Quality) = PathUtils.ExtractQuality(item.Name);
                    if (Name == fnSplit.Item2)
                    {
                        var fi = TSFileAdditionalInfo.Get(item as IFileCache, _dc);
                        if (fi.ImageCacheId != item.ChangeId)
                        {
                            _wasChange = true;
                            fi.Image = Image.Load((item as IFileCache).ByteContent);
                            fi.ImageCacheId = item.ChangeId;
                        }
                        slices.Add(new SpriteSlice { name = item.Name, quality = Quality, width = fi.Image.Width, height = fi.Image.Height });
                    }
                }
                slices.Sort((l, r) => l.quality < r.quality ? -1 : l.quality > r.quality ? 1 : 0);
                sprite.slices = slices.ToArray();
                if (sprite.slices.Length>0)
                {
                    sprite.owidth = (int)(slices[0].width / slices[0].quality + 0.5);
                    sprite.oheight = (int)(slices[0].height / slices[0].quality + 0.5);
                }
                else
                {
                    sprite.owidth = 0;
                    sprite.oheight = 0;
                }
            }
        }

        public void Retrieve(List<SourceInfo.Sprite> sprites)
        {
            for (int i = 0; i < sprites.Count; i++)
            {
                var sprite = sprites[i];
                if (sprite.name != null && sprite.oheight == 0 && sprite.owidth == 0)
                {
                    var idx = FindSprite(_allSprites, sprite);
                    var s = _allSprites[idx];
                    if (sprite.height != null)
                        sprite.oheight = Math.Min(s.oheight, sprite.height.Value);
                    else
                        sprite.oheight = s.oheight;
                    if (sprite.width != null)
                        sprite.owidth = Math.Min(s.owidth, sprite.width.Value);
                    else
                        sprite.owidth = s.owidth;
                    sprite.ox = s.ox + Math.Max(0, Math.Min(sprite.x.GetValueOrDefault(), s.owidth - sprite.owidth));
                    sprite.oy = s.oy + Math.Max(0, Math.Min(sprite.y.GetValueOrDefault(), s.oheight - sprite.oheight));
                    sprites[i] = sprite;
                }
            }
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
                    c.Fill(Rgba32.Transparent);
                    for (int j = 0; j < _allSprites.Count; j++)
                    {
                        var sprite = _allSprites[j];
                        var fn = sprite.name;
                        var slice = FindBestSlice(sprite.slices, q);
                        var f = _dc.TryGetItem(PathUtils.InjectQuality(fn, slice.quality));
                        if (f is IFileCache)
                        {
                            var fi = TSFileAdditionalInfo.Get(f as IFileCache, _dc);
                            var image = fi.Image;
                            if (sprite.color != null)
                            {
                                Rgba32 rgbColor = ParseColor(sprite.color);
                                image = image.Clone(operation =>
                                {
                                    operation.ApplyProcessor(new Recolor(rgbColor));
                                });
                            }
                            image = image.Clone(operation =>
                            {
                                if (q != slice.quality)
                                    operation = operation.Resize((int)Math.Round(image.Width * q / slice.quality), (int)Math.Round(image.Height * q / slice.quality));
                                operation.Crop(new Rectangle(new Point((int)(q * (sprite.x ?? 0)), (int)(q * (sprite.y ?? 0))), new Size((int)(sprite.owidth * q), (int)(sprite.oheight * q))));
                            });
                            c.DrawImage(new GraphicsOptions(), image, new Point((int)(sprite.ox * q), (int)(sprite.oy * q)));
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

        class Recolor : IImageProcessor<Rgba32>
        {
            Rgba32 _rgbColor;

            public Recolor(Rgba32 rgbColor)
            {
                _rgbColor = rgbColor;
            }

            public void Apply(Image<Rgba32> source, Rectangle sourceRectangle)
            {
                var cgray = new Bgr24(128, 128, 128);
                var frame = source.Frames.RootFrame;
                var crgb = new Bgr24();
                if (_rgbColor.A == 255)
                {
                    for (int y = 0; y < frame.Height; y++)
                        for (int x = 0; x < frame.Width; x++)
                        {
                            var c = frame[x, y];
                            c.ToBgr24(ref crgb);
                            if (cgray.Equals(crgb))
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
                            c.ToBgr24(ref crgb);
                            if (cgray.Equals(crgb))
                            {
                                var alpha = c.A;
                                c = _rgbColor;
                                c.A = (byte)(((c.A * alpha) * 32897) >> 23); // clever divide by 255
                                frame[x, y] = c;
                            }
                        }
                }
            }
        }
    }
}
