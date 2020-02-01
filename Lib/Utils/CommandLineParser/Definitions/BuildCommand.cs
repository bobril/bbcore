using Lib.Utils.CommandLineParser.Parser;

namespace Lib.Utils.CommandLineParser.Definitions
{
    public class BuildCommand : CommonParametersBaseCommand
    {
        public override string[] Words => new[] {"b", "build"};

        protected override string Description => "just build and stop";

        public CommandLineArgumentString Dir { get; } = new CommandLineArgumentString(
            description: "define where to put build result", words: new[] {"-d", "--dir"}, defaultValue: null);

        public CommandLineArgumentBool Fast { get; } =
            new CommandLineArgumentBool(description: "quick debuggable bundling", words: new[] {"-f", "--fast"});

        public CommandLineArgumentBool NewBundler { get; } = new CommandLineArgumentBool(
            description: "use new Njsast based bundler", words: new[] {"-x", "--newBundler"}, defaultValue: true);

        public CommandLineArgumentEnumValues SourceMap { get; } =
            new CommandLineArgumentEnumValues("build sourcemaps", new[] {"-g", "--sourcemap"}, new[] {"no", "yes"});

        public CommandLineArgumentString SourceMapRoot { get; } =
            new CommandLineArgumentString("sets sourceRoot for sourcemap", new[] {"--sourceRoot"});

        public CommandLineArgumentBool Compress { get; } = new CommandLineArgumentBool(description: "remove dead code",
            words: new[] {"-c", "--compress"}, defaultValue: true);

        public CommandLineArgumentBool Mangle { get; } = new CommandLineArgumentBool(description: "minify names",
            words: new[] {"-m", "--mangle"}, defaultValue: true);

        public CommandLineArgumentBool Beautify { get; } =
            new CommandLineArgumentBool(description: "readable formatting", words: new[] {"-b", "--beautify"});

        public CommandLineArgumentEnumValues Style { get; } = new CommandLineArgumentEnumValues(
            description: "override styleDef className preservation level", words: new[] {"-s", "--style"},
            enumValues: new[] {"0", "1", "2"});

        public CommandLineArgumentBool Sprite { get; } = new CommandLineArgumentBool(
            description: "enable/disable creation of sprites", words: new[] {"-p", "--sprite"}, defaultValue: true);

        public CommandLineArgumentBoolNullable Localize { get; } = new CommandLineArgumentBoolNullable(
            description: "create localized resources (default: autodetect)", words: new[] {"-l", "--localize"});

        public CommandLineArgumentBool UpdateTranslations { get; } =
            new CommandLineArgumentBool(description: "update translations",
                words: new[] {"-u", "--updateTranslations"});

        public CommandLineArgumentString VersionDir { get; } = new CommandLineArgumentString(
            description: "store all resources except index.html in this directory",
            words: new[] {"-v", "--versiondir"});

        public CommandLineArgumentSwitch NoUpdate { get; } =
            new CommandLineArgumentSwitch(description: "do not install dependencies at start",
                words: new[] {"-n", "--noupdate"});
    }
}
