using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    /// <summary>
    /// Build command without hosting
    /// </summary>
    public class BuildCommand : CommandLineCommand
    {
        /// <summary>
        /// Command aliases
        /// </summary>
        public override string[] Words => new[] { "b", "build" };

        /// <summary>
        /// Command description
        /// </summary>
        protected override string Description => "just build and stop";

        /// <summary>
        /// Directory
        /// </summary>
        public CommandLineArgumentString Dir { get; private set; } = new CommandLineArgumentString(description: "define where to put build result", words: new[] { "-d", "--dir" }, defaultValue: "./dist");

        /// <summary>
        /// Fast
        /// </summary>
        public CommandLineArgumentBool Fast { get; private set; } = new CommandLineArgumentBool(description: "quick debuggable bundling", words: new[] { "-f", "--fast" });

        /// <summary>
        /// Compress
        /// </summary>
        public CommandLineArgumentBool Compress { get; private set; } = new CommandLineArgumentBool(description: "remove dead code", words: new[] { "-c", "--compress" }, defaultValue: true);

        /// <summary>
        /// Mangle
        /// </summary>
        public CommandLineArgumentBool Mangle { get; private set; } = new CommandLineArgumentBool(description: "minify names", words: new[] { "-m", "--mangle" }, defaultValue: true);

        /// <summary>
        /// Beautify
        /// </summary>
        public CommandLineArgumentBool Beautify { get; private set; } = new CommandLineArgumentBool(description: "readable formatting", words: new[] { "-b", "--beautify" });

        /// <summary>
        /// Style
        /// </summary>
        public CommandLineArgumentEnumValues Style { get; private set; } = new CommandLineArgumentEnumValues(description: "override styleDef className preservation level", words: new[] { "-s", "--style" }, enumValues: new[] { "0", "1", "2" });

        /// <summary>
        /// Sprite
        /// </summary>
        public CommandLineArgumentBool Sprite { get; private set; } = new CommandLineArgumentBool(description: "enable/disable creation of sprites", words: new[] { "-p", "--sprite" });

        /// <summary>
        /// Localize
        /// </summary>
        public CommandLineArgumentBoolNullable Localize { get; private set; } = new CommandLineArgumentBoolNullable(description: "create localized resources (default: autodetect)", words: new[] { "-l", "--localize" });

        /// <summary>
        /// UpdateTranslations
        /// </summary>
        public CommandLineArgumentBool UpdateTranslations { get; private set; } = new CommandLineArgumentBool(description: "update translations", words: new[] { "-u", "--updateTranslations" });

        /// <summary>
        /// VersionDir
        /// </summary>
        public CommandLineArgumentString VersionDir { get; private set; } = new CommandLineArgumentString(description: "store all resources except index.html in this directory", words: new[] { "-v", "--versiondir" });

        /// <summary>
        /// NoUpdate
        /// </summary>
        public CommandLineArgumentSwitch NoUpdate { get; private set; } = new CommandLineArgumentSwitch(description: "update translations", words: new[] { "-n", "--noupdate" });
    }
}
