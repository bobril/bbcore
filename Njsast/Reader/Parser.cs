using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Njsast.Ast;

namespace Njsast.Reader
{
    public sealed partial class Parser
    {
        internal readonly string? SourceFile;
        readonly string _input;
        bool _containsEsc;
        readonly Regex _keywords;
        readonly Regex _reservedWords;
        readonly Regex _reservedWordsStrict;
        readonly Regex _reservedWordsStrictBind;
        Position _pos;
        readonly Stack<Scope> _scopeStack;
        StructList<AstLabel> _labels;
        Position _awaitPos;
        Position _yieldPos;
        Position _potentialArrowAt;
        bool _inAsync;
        bool _inGenerator;
        bool _inFunction;
        internal TokenType Type;
        internal object? Value;
        internal Position Start;
        internal Position End;
        Position _lastTokStart;
        Position _lastTokEnd;
        bool _exprAllowed;
        readonly List<TokContext> _context;
        readonly bool _inModule;
        bool _strict;
        bool _inTemplateElement;
        bool _allowBreak;
        bool _allowContinue;
        bool _canBeDirective;
        bool _wasImportKeyword;

        public static AstToplevel Parse(string input, Options? options = null)
        {
            return new Parser(options, input).Parse();
        }

        public Parser(Options? options, string input, int? startPos = null)
        {
            Options = options = Options.GetOptions(options);
            SourceFile = options.SourceFile;
            _keywords = options.EcmaVersion >= 6 ? Ecmascript6KeywordsRegex : Ecmascript5KeywordsRegex;

            if (options.AllowReserved == null || options.AllowReserved is bool && (bool)options.AllowReserved == false)
            {
                var isModule = options.SourceType == SourceType.Module;
                if (options.EcmaVersion < 3)
                    (_reservedWords, _reservedWordsStrict, _reservedWordsStrictBind) = isModule ? EcmascriptModuleNoReservedRegex : EcmascriptNoReservedRegex;
                else if (options.EcmaVersion < 5)
                    (_reservedWords, _reservedWordsStrict, _reservedWordsStrictBind) = isModule ? Ecmascript3ModuleReservedRegex : Ecmascript3ReservedRegex;
                else if (options.EcmaVersion < 6)
                    (_reservedWords, _reservedWordsStrict, _reservedWordsStrictBind) = isModule ? Ecmascript5ModuleReservedRegex : Ecmascript5ReservedRegex;
                else
                    (_reservedWords, _reservedWordsStrict, _reservedWordsStrictBind) = isModule ? Ecmascript6ModuleReservedRegex : Ecmascript6ReservedRegex;
            }
            else
            {
                (_reservedWords, _reservedWordsStrict, _reservedWordsStrictBind) = EcmascriptNoReservedRegex;
            }

            _input = input;

            // Used to signal to callers of `readWord1` whether the word
            // contained any escape sequences. This is needed because words with
            // escape sequences must not be interpreted as keywords.
            _containsEsc = false;

            // Set up token state

            // The current position of the tokenizer in the input.
            if (startPos.HasValue && startPos.Value > 0)
            {
                var lineStart = input.LastIndexOf('\n', startPos.Value - 1) + 1;
                var currentLine = LineBreak.Matches(input.Substring(0, lineStart)).Count;
                _pos = new Position(currentLine, startPos.Value - lineStart, startPos.Value);
            }
            else
            {
                _pos = new Position(0, 0, 0);
            }

            // Properties of the current token:
            // Its type
            Type = TokenType.Eof;
            // For tokens that include more information than their type, the value
            Value = null;
            // Its start and end
            Start = End = CurPosition();

            // Position information for the previous token
            _lastTokStart = _lastTokEnd = CurPosition();

            // The context stack is used to superficially track syntactic
            // context to predict whether a regular expression is allowed in a
            // given position.
            _context = InitialContext();
            _exprAllowed = true;

            // Figure out if it's a module code.
            _inModule = options.SourceType == SourceType.Module;
            _strict = _inModule;

            // Used to signify the start of a potential arrow function
            _potentialArrowAt = default;

            // Flags to track whether we are in a function, a generator, an async function.
            _inFunction = options.StartInFunction;
            // Positions to delayed-check that yield/await does not exist in default parameters.
            _yieldPos = _awaitPos = default;
            // Labels in scope.
            _labels = new StructList<AstLabel>();
            _allowBreak = false;
            _allowContinue = false;

            // If enabled, skip leading hashbang line.
            if (_pos.Index == 0 && options.AllowHashBang && _input.Substring(0, 2) == "#!")
                SkipLineComment(2);

            // Scope tracking for duplicate variable names (see scope.js)
            _scopeStack = new Stack<Scope>();
            EnterFunctionScope();
        }

        public AstToplevel Parse()
        {
            var node = Options.Program ?? new AstToplevel(SourceFile, Start, _lastTokEnd);
            NextToken();
            ParseTopLevel(node);
            return node;
        }

        public Options Options { get; }

        object GetValue()
        {
            if (Value == null)
                throw new NullReferenceException($"{nameof(Value)} is null");
            return Value;
        }
    }
}
