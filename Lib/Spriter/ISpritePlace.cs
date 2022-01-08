using System.Collections.Generic;
using System.Linq;

namespace Lib.Spriter;

public interface ISpritePlace
{
    int Width { get; }
    int Height { get; }
    int X { get; set; }
    int Y { get; set; }
}