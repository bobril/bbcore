using System;
using System.Collections.Generic;

namespace Lib.Utils.CommandLineParser.Parser
{
    public class CommandLineArgumentBoolNullable : CommandLineArgument
    {
        public bool? Value { get; private set; }

        public CommandLineArgumentBoolNullable(string description, string[]? words, bool? defaultValue = null) : base(description, words, defaultValue?.ToString())
        {
            Value = defaultValue;
        }

        public override string[] SetValue(string[] args)
        {
            if (args.Length < 2)
                return null;

            var secondArg = args[1];
            var value = Parse(secondArg);
            if (!TrySetValueLocal(value))
                return null;

            var returnArgs = new string[args.Length - 2];
            Array.Copy(args, 2, returnArgs, 0, returnArgs.Length);
            return returnArgs;
        }

        protected virtual bool TrySetValueLocal(bool? value)
        {
            Value = value;
            return true;
        }

        static bool? Parse(string value)
        {
            return TrueValues.Contains(value) ? true : (FalseValues.Contains(value) ? false : (bool?)null);
        }

        static List<string> TrueValues { get; } = new List<string> {"true", "True", "1", "t", "T", "y", "Y"};

        static List<string> FalseValues { get; } = new List<string> {"false", "False", "0", "f", "F", "n", "N"};

        protected override string AdditionalHelpInfo { get; } = $"<{string.Join("|", TrueValues)}|{string.Join("|", FalseValues)}>";
    }
}
