using System;

namespace BobrilMdx.Test;

class Program
{
    static void Main(string[] args)
    {
        var main = new MdxToTsx();
        main.Parse("``` inline lineno\nfunction A() { return <div>hello</div>; }\n```\n");
        Console.WriteLine(main.Render().content);
    }
}