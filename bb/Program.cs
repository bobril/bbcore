namespace bb
{
    class Program
    {
        static void Main(string[] args)
        {
            var composition = new Lib.Composition.Composition();
            composition.ParseCommandLine(args);
            composition.RunCommand();
        }
    }
}
