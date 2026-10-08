using System.Collections.Generic;
using Lib.Spriter;
using Njsast.Bobril;

namespace Lib.TSCompiler;

public interface ISpriteGenerator
{
    void Process(List<SourceInfo.Sprite> sprites);
    void ProcessNew();
    List<OutputSprite> Retrieve(List<SourceInfo.Sprite> sprites);
    IReadOnlyList<ImageBytesWithQuality>? BuildImage(bool maxCompression);
}

public struct ImageBytesWithQuality
{
    public float Quality;
    public byte[] Content;
}
