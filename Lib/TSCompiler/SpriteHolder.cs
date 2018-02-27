using System;
using System.Collections.Generic;
using System.IO;
using Lib.DiskCache;
using Lib.Spriter;
using SixLabors.ImageSharp;
using SixLabors.Primitives;

namespace Lib.TSCompiler
{
    public class SpriteHolder : ISpritePlace
    {
        IDiskCache _dc;
        Sprite2dPlacer _placer;
        List<SourceInfo.Sprite> _allSprites;
        List<SourceInfo.Sprite> _newSprites;
        byte[] _result;
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
                var fn = sprite.name;
                var f = _dc.TryGetItem(fn);
                if (f is IFileCache)
                {
                    var fi = TSFileAdditionalInfo.Get(f as IFileCache, _dc);
                    if (fi.ImageCacheId != f.ChangeId)
                    {
                        fi.Image = Image.Load((f as IFileCache).ByteContent);
                        fi.ImageCacheId = f.ChangeId;
                    }
                    sprite.owidth = fi.Image.Width;
                    sprite.oheight = fi.Image.Height;
                    _allSprites[i] = sprite;
                }
            }
            if (_newSprites.Count == 0)
                return;
            _wasChange = true;
            for (int i = 0; i < _newSprites.Count; i++)
            {
                var sprite = _newSprites[i];
                var fn = sprite.name;
                var f = _dc.TryGetItem(fn);
                if (f is IFileCache)
                {
                    var fi = TSFileAdditionalInfo.Get(f as IFileCache, _dc);
                    if (fi.Image == null)
                    {
                        fi.Image = Image.Load((f as IFileCache).ByteContent);
                        fi.ImageCacheId = f.ChangeId;
                    }
                    sprite.owidth = fi.Image.Width;
                    sprite.oheight = fi.Image.Height;
                    _newSprites[i] = sprite;
                }
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

        public byte[] BuildImage(bool maxCompression)
        {
            if (!_wasChange)
            {
                return _result;
            }
            var resultImage = new Image<Rgba32>(_placer.Dim.Width, _placer.Dim.Height);
            resultImage.Mutate(c =>
            {
                c.Fill(Rgba32.Transparent);
                for (int i = 0; i < _allSprites.Count; i++)
                {
                    var sprite = _allSprites[i];
                    var fn = sprite.name;
                    var f = _dc.TryGetItem(fn);
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
                        c.DrawImage(image, new Size(sprite.owidth, sprite.oheight), new Point(sprite.ox, sprite.oy), new GraphicsOptions());
                    }
                }
            });
            var ms = new MemoryStream();
            resultImage.Save(ms, new SixLabors.ImageSharp.Formats.Png.PngEncoder { CompressionLevel = maxCompression ? 9 : 1 });
            _result = ms.ToArray();
            _wasChange = false;
            return _result;
        }

        Rgba32 ParseColor(string color)
        {
            if (color.Length == 4)
            {
                color = "#" + color[1] + color[1] + color[2] + color[2] + color[3] + color[3];
            }
            if (color.Length == 7)
            {
                return new Rgba32(
                    (byte)int.Parse(color.Substring(1, 2), System.Globalization.NumberStyles.HexNumber),
                    (byte)int.Parse(color.Substring(3, 2), System.Globalization.NumberStyles.HexNumber),
                    (byte)int.Parse(color.Substring(5, 2), System.Globalization.NumberStyles.HexNumber));
            }
            return Rgba32.Transparent;
        }

        private class Recolor : IImageProcessor<Rgba32>
        {
            private Rgba32 rgbColor;

            public Recolor(Rgba32 rgbColor)
            {
                this.rgbColor = rgbColor;
            }

            public void Apply(Image<Rgba32> source, Rectangle sourceRectangle)
            {
                var cgray = new SixLabors.ImageSharp.PixelFormats.Bgr24(128, 128, 128);
                var frame = source.Frames.RootFrame;
                for (int y = 0; y < frame.Height; y++)
                    for (int x = 0; x < frame.Width; x++)
                    {
                        var c = frame[x, y];
                        var crgb = new SixLabors.ImageSharp.PixelFormats.Bgr24();
                        c.ToBgr24(ref crgb);
                        if (cgray.Equals(crgb))
                        {
                            var alpha = c.A;
                            c = rgbColor;
                            c.A = alpha;
                            frame[x, y] = c;
                        }
                    }
            }
        }
    }
}
