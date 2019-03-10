namespace Lib.Utils.CommandLineParser.Definitions
{
    public class FindUnusedCommand : CommonParametersBaseCommand
    {
        public override string[] Words => new[] { "findunused", "fu" };

        protected override string Description => "find unused TypeScript sources";
    }
}