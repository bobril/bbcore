using System;

namespace BobrilMdx.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var main = new MdxToTsx();
            main.Parse("import * as bobx from \"bobx\";\n\n<button onClick={()=>alert()}>Click me</button>\n");
            Console.WriteLine(main.Render().content);
        }
    }
}
