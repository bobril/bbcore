using Njsast.Ast;

namespace Njsast.Reader
{
    public delegate void OnCommentAction(bool block, string content, SourceLocation sourceLocation);

    public sealed class Options
    {
        public const int DefaultEcmaVersion = 7;
        // `ecmaVersion` indicates the ECMAScript version to parse. Must
        // be either 3, 5, 6 (2015), 7 (2016), or 8 (2017). This influences support
        // for strict mode, the set of reserved words, and support for
        // new syntax features. The default is 7.
        public int EcmaVersion;
        // `sourceType` indicates the mode the code should be parsed in.
        // Can be either `"script"` or `"module"`. This influences global
        // strict mode and parsing of `import` and `export` declarations.
        public SourceType SourceType = SourceType.Script;
        // By default, reserved words are only enforced if ecmaVersion >= 5.
        // Set `allowReserved` to a boolean value to explicitly turn this on
        // an off. When this option has the value "never", reserved words
        // and keywords can also not be used as property names.
        public object? AllowReserved;
        // When enabled, a return at the top level is not considered an
        // error.
        public bool AllowReturnOutsideFunction = false;
        // When enabled, import/export statements are not constrained to
        // appearing at the top of the program.
        public bool AllowImportExportEverywhere = false;
        // When enabled, hashbang directive in the beginning of file
        // is allowed and treated as a line comment.
        public bool AllowHashBang = false;
        // It is possible to parse multiple files into a single AST by
        // passing the tree produced by parsing the first file as
        // `program` option in subsequent parses. This will add the
        // toplevel forms of the parsed file to the `Program` (top) node
        // of an existing parse tree.
        public AstToplevel? Program = null;
        // When `locations` is on, you can pass this to record the source
        // file in every node's `loc` object.
        public string? SourceFile = null;
        public bool StartInFunction;
        public OnCommentAction? OnComment;

        public static Options GetOptions(Options? options)
        {
            if (options == null)
                options = new Options();

            if (options.EcmaVersion == 0)
                options.EcmaVersion = DefaultEcmaVersion;

            if (options.EcmaVersion >= 2015)
                options.EcmaVersion -= 2009;

            if (options.AllowReserved == null)
                options.AllowReserved = options.EcmaVersion < 5;

            return options;
        }
    }
}
