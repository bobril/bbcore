using System;
using System.Collections.Generic;
using Lib.DiskCache;
using Lib.TSCompiler;
using Njsast.Bobril;
using SkiaSharp;
using Xunit;

namespace Lib.Test;

public class SpriteHolderTests
{
    // A straight-alpha PNG encoded independently of Skia. Pixels: exact gray at
    // alpha 255/127/1, three near misses at alpha 255, then transparent exact gray.
    static readonly byte[] MarkerPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAcAAAABCAYAAAASC7TOAAAAG0lEQVR4nGNoaGj4D8T1QMxYD2LXg/iNIDEGANvQDvx0Lw5wAAAAAElFTkSuQmCC");

    [Fact]
    public void RecolorPreservesExactMarkerAndAlphaForEveryAlphaCombination()
    {
        byte[] pixels = new byte[256 * 4];
        for (var colorAlpha = 0; colorAlpha < 256; colorAlpha++)
        {
            for (var alpha = 0; alpha < 256; alpha++)
            {
                pixels[alpha * 4] = pixels[alpha * 4 + 1] = pixels[alpha * 4 + 2] = 128;
                pixels[alpha * 4 + 3] = (byte)alpha;
            }
            SpriteHolder.Recolor(pixels, new SKColor(17, 53, 219, (byte)colorAlpha));
            for (var alpha = 0; alpha < 256; alpha++)
            {
                Assert.Equal(17, pixels[alpha * 4]);
                Assert.Equal(53, pixels[alpha * 4 + 1]);
                Assert.Equal(219, pixels[alpha * 4 + 2]);
                Assert.Equal(alpha * colorAlpha / 255, pixels[alpha * 4 + 3]);
            }
        }
    }

    [Fact]
    public void RecolorDoesNotChangeAnyNonMarkerChannelValue()
    {
        byte[] pixel = new byte[4];
        for (var channel = 0; channel < 3; channel++)
        for (var value = 0; value < 256; value++)
        {
            if (value == 128) continue;
            pixel[0] = pixel[1] = pixel[2] = 128;
            pixel[3] = 37;
            pixel[channel] = (byte)value;
            var expected = (byte[])pixel.Clone();
            SpriteHolder.Recolor(pixel, SKColors.Red);
            Assert.Equal(expected, pixel);
        }
    }

    [Theory]
    [InlineData(false, "#ff0000", 255)]
    [InlineData(true, "#ff0000", 255)]
    [InlineData(false, "#ff000080", 128)]
    [InlineData(true, "#ff000080", 128)]
    public void AtlasRecolorsStraightAlphaPixelsWithoutChangingCachedSource(bool compression, string color, int alpha)
    {
        var fs = new InMemoryFs();
        fs.WriteAllBytes("/project/icon.png", MarkerPng);
        var dc = new DiskCache.DiskCache(fs, () => fs);
        var holder = new SpriteHolder(dc, new DummyLogger());
        List<SourceInfo.Sprite> sprites = [Sprite("icon.png", color), Sprite("icon.png")];
        holder.Process(sprites);
        holder.ProcessNew();
        var result = holder.BuildImage(compression);
        var atlas = Assert.Single(result!);
        Assert.Equal(1, atlas.Quality);
        using var bitmap = SKBitmap.Decode(atlas.Content)!;
        var positions = holder.Retrieve(sprites);
        var red = positions[0];
        Assert.Equal(new SKColor(255, 0, 0, (byte)alpha), bitmap.GetPixel(red.ox, red.oy));
        Assert.Equal(new SKColor(255, 0, 0, (byte)(127 * alpha / 255)), bitmap.GetPixel(red.ox + 1, red.oy));
        var faint = bitmap.GetPixel(red.ox + 2, red.oy);
        Assert.Equal(alpha / 255, faint.Alpha);
        if (faint.Alpha != 0) Assert.Equal(SKColors.Red.WithAlpha(1), faint);
        Assert.Equal(new SKColor(127, 128, 128), bitmap.GetPixel(red.ox + 3, red.oy));
        Assert.Equal(new SKColor(128, 127, 128), bitmap.GetPixel(red.ox + 4, red.oy));
        Assert.Equal(new SKColor(128, 128, 129), bitmap.GetPixel(red.ox + 5, red.oy));
        Assert.Equal(0, bitmap.GetPixel(red.ox + 6, red.oy).Alpha);
        var plain = positions[1];
        Assert.Equal(new SKColor(128, 128, 128), bitmap.GetPixel(plain.ox, plain.oy));
        Assert.Same(result, holder.BuildImage(compression));
    }

    [Fact]
    public void AtlasBuildsMultipleQualitiesAndResizesMissingSlices()
    {
        var fs = new InMemoryFs();
        fs.WriteAllBytes("/project/red.png", SolidPng(2, 2, SKColors.Red));
        fs.WriteAllBytes("/project/red@2.png", SolidPng(4, 4, SKColors.Red));
        fs.WriteAllBytes("/project/blue@1.5.png", SolidPng(3, 3, SKColors.Blue));
        var dc = new DiskCache.DiskCache(fs, () => fs);
        var holder = new SpriteHolder(dc, new DummyLogger());
        List<SourceInfo.Sprite> sprites = [Sprite("red.png"), Sprite("blue.png")];
        holder.Process(sprites);
        holder.ProcessNew();
        var result = holder.BuildImage(true)!;
        Assert.Collection(result, s => Assert.Equal(1, s.Quality),
            s => Assert.Equal(1.5f, s.Quality), s => Assert.Equal(2, s.Quality));
        var positions = holder.Retrieve(sprites);
        foreach (var slice in result)
        {
            using var bitmap = SKBitmap.Decode(slice.Content)!;
            for (var i = 0; i < positions.Count; i++)
            {
                var position = positions[i];
                Assert.Equal(2, position.owidth);
                Assert.Equal(2, position.oheight);
                Assert.Equal(i == 0 ? SKColors.Red : SKColors.Blue,
                    bitmap.GetPixel((int)(position.ox * slice.Quality), (int)(position.oy * slice.Quality)));
            }
        }
    }

    [Fact]
    public void ChangedImageIsReloadedDuringIncrementalBuild()
    {
        var fs = new InMemoryFs();
        fs.WriteAllBytes("/project/icon.png", SolidPng(2, 2, SKColors.Red));
        var dc = new DiskCache.DiskCache(fs, () => fs);
        var holder = new SpriteHolder(dc, new DummyLogger());
        holder.Process([Sprite("icon.png")]);
        holder.ProcessNew();
        var original = holder.BuildImage(false);
        fs.WriteAllBytes("/project/icon.png", SolidPng(2, 2, SKColors.Blue));
        fs.OnFileChange("/project/icon.png");
        dc.CheckForTrueChange();
        holder.ProcessNew();
        var updated = holder.BuildImage(false);
        Assert.NotSame(original, updated);
        using var bitmap = SKBitmap.Decode(Assert.Single(updated!).Content)!;
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(0, 0));
    }

    static SourceInfo.Sprite Sprite(string name, string? color = null) => new()
    {
        Name = "/project/" + name, Color = color, Width = -1, Height = -1
    };

    static byte[] SolidPng(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var pixels = bitmap.PeekPixels();
        using var encoded = pixels.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, 1));
        Assert.NotNull(encoded);
        return encoded.ToArray();
    }
}
