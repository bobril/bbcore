using Lib.TSCompiler;

namespace Bbcore.Lib.Test;

public class ImageDependencyTests
{
    [Fact]
    public void LibraryDoesNotReferenceImageProcessingAssemblies()
    {
        var assembly = typeof(BbcoreLibrary).Assembly;
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a =>
            a.Name!.Contains("ImageSharp", StringComparison.OrdinalIgnoreCase) ||
            a.Name.Contains("SkiaSharp", StringComparison.OrdinalIgnoreCase));
        Assert.Null(assembly.GetType("Lib.TSCompiler.SpriteHolder"));
    }

    [Fact]
    public void BuiltInSpriteGenerationIsExplicitlyUnavailable()
    {
        var options = new ProjectOptions { SpriteGeneration = true };
        var error = Assert.Throws<NotSupportedException>(() =>
            options.SpriterInitialization(new MainBuildResult(false, null, null)));
        Assert.Contains("Bbcore.Lib", error.Message);
    }
}
