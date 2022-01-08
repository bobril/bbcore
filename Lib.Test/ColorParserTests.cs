using Lib.TSCompiler;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Lib.Test;

public class ColorParserTests
{
    [Fact]
    public void SimpleRGB()
    {
        Assert.Equal(new Rgba32(255, 255, 255, 255), SpriteHolder.ParseColor("#fff"));
        Assert.Equal(new Rgba32(0x11, 0x22, 0x33, 255), SpriteHolder.ParseColor("#123"));
        Assert.Equal(new Rgba32(0x12, 0x34, 0x56, 255), SpriteHolder.ParseColor("#123456"));
    }

    [Fact]
    public void ComplexRGBA()
    {
        Assert.Equal(new Rgba32(0x11, 0x22, 0x33, 0x44), SpriteHolder.ParseColor("#1234"));
        Assert.Equal(new Rgba32(0x12, 0x34, 0x56, 0x78), SpriteHolder.ParseColor("#12345678"));
        Assert.Equal(new Rgba32(0, 0, 0, 128), SpriteHolder.ParseColor("rgba(0,0,0,0.5)"));
        Assert.Equal(new Rgba32(17, 1, 55, 26), SpriteHolder.ParseColor("rgba(17,1,55,0.1)"));
    }
}