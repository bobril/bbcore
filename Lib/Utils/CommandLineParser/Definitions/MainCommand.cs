using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    /// <summary>
    /// Main build command
    /// </summary>
    public class MainCommand : CommandLineCommand
    {
        /// <summary>
        /// Command aliases
        /// </summary>
        public override string[] Words => null;

        /// <summary>
        /// Command description
        /// </summary>
        protected override string Description => "build and host";
    }
}
