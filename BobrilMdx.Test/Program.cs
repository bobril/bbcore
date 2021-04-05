using System;

namespace BobrilMdx.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            var main = new MdxToTsx();
            main.Parse("Hello *cool* **hot**");
            Console.WriteLine(main.Render());
        }
    }
}
