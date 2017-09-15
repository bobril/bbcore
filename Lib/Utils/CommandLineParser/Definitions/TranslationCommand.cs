using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    /// <summary>
    /// Build command with translations
    /// </summary>
    public class TranslationCommand : CommandLineCommand
    {
        /// <summary>
        /// Command aliases
        /// </summary>
        public override string[] Words => new[] { "t", "translation" };

        /// <summary>
        /// Command description
        /// </summary>
        protected override string Description => "everything around translations";

        /// <summary>
        /// AddLang
        /// </summary>
        public CommandLineArgumentString AddLang { get; private set; } = new CommandLineArgumentString(description: "add new language", words: new[] { "-a", "--addlang" });

        /// <summary>
        /// RemoveLang
        /// </summary>
        public CommandLineArgumentString RemoveLang { get; private set; } = new CommandLineArgumentString(description: "remove language", words: new[] { "-r", "--removelang" });

        /// <summary>
        /// Export
        /// </summary>
        public CommandLineArgumentString Export { get; private set; } = new CommandLineArgumentString(description: "export untranslated languages", words: new[] { "-e", "--export" });

        /// <summary>
        /// ExportAll
        /// </summary>
        public CommandLineArgumentString ExportAll { get; private set; } = new CommandLineArgumentString(description: "export all texts from all languages", words: new[] { "-x", "--exportAll" });

        /// <summary>
        /// Import
        /// </summary>
        public CommandLineArgumentString Import { get; private set; } = new CommandLineArgumentString(description: "import translated language", words: new[] { "-i", "--import" });

        /// <summary>
        /// SpecificPath
        /// </summary>
        public CommandLineArgumentString SpecificPath { get; private set; } = new CommandLineArgumentString(description: "specify path for export from / import to", words: new[] { "-p", "--specificPath" });

        /// <summary>
        /// Lang
        /// </summary>
        public CommandLineArgumentString Lang { get; private set; } = new CommandLineArgumentString(description: "specify language for export", words: new[] { "-l", "--lang" });

        /// <summary>
        /// Union
        /// </summary>
        public CommandLineArgumentStrings Union { get; private set; } = new CommandLineArgumentStrings(description: "make union from paths <sourcePath1,sourcePath2,destinationPath>", words: new[] { "-u", "--union" }, valuesCount: 3);

        /// <summary>
        /// Subtract
        /// </summary>
        public CommandLineArgumentStrings Subtract { get; private set; } = new CommandLineArgumentStrings(description: "make subtract of paths <sourcePath1,sourcePath2,destinationPath>", words: new[] { "-s", "--subtract" }, valuesCount: 3);
    }
}
