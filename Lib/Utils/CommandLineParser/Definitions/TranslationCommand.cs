using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public class TranslationCommand : CommandLineCommand
    {
        public override string[] Words => new[] { "t", "translation" };

        protected override string Description => "everything around translations";

        public CommandLineArgumentString AddLang { get; } = new CommandLineArgumentString(description: "add new language", words: new[] { "-a", "--addlang" });

        public CommandLineArgumentString RemoveLang { get; } = new CommandLineArgumentString(description: "remove language", words: new[] { "-r", "--removelang" });


        public CommandLineArgumentString Export { get; } = new CommandLineArgumentString(description: "export untranslated languages", words: new[] { "-e", "--export" });

        public CommandLineArgumentString ExportAll { get; } = new CommandLineArgumentString(description: "export all texts from all languages", words: new[] { "-x", "--exportAll" });

        public CommandLineArgumentString Import { get; } = new CommandLineArgumentString(description: "import translated language", words: new[] { "-i", "--import" });

        public CommandLineArgumentString SpecificPath { get; } = new CommandLineArgumentString(description: "specify path for export from / import to", words: new[] { "-p", "--specificPath" });

        public CommandLineArgumentString Lang { get; } = new CommandLineArgumentString(description: "specify language for export", words: new[] { "-l", "--lang" });

        public CommandLineArgumentStrings Union { get; } = new CommandLineArgumentStrings(description: "make union from paths <sourcePath1,sourcePath2,destinationPath>", words: new[] { "-u", "--union" }, valuesCount: 3);

        public CommandLineArgumentStrings Subtract { get; } = new CommandLineArgumentStrings(description: "make subtract of paths <sourcePath1,sourcePath2,destinationPath>", words: new[] { "-s", "--subtract" }, valuesCount: 3);
    }
}
