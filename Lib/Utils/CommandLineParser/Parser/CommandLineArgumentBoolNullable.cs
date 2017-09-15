using System;
using System.Collections.Generic;

namespace Lib.Utils.CommandLineParser.Parser
{
    public class CommandLineArgumentBoolNullable : CommandLineArgument
    {
        /// <summary>
        /// Value
        /// </summary>
        public bool? Value { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="description">Description</param>
        /// <param name="words">Argument aliases</param>
        /// <param name="defaultValue">Default value</param>
        public CommandLineArgumentBoolNullable(string description, string[] words, bool? defaultValue = null) : base(description: description, words: words, defaultValue: defaultValue?.ToString())
        {
            Value = defaultValue;
        }

        /// <summary>
        /// Set value of argument
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Unused command line arguments</returns>
        public override string[] SetValue(string[] args)
        {
            if (args.Length < 2)
                return null;

            string secondArg = args[1];
            bool? value = Parse(secondArg);
            if (!TrySetValueLocal(value))
                return null;

            string[] returnArgs = new string[args.Length - 2];
            Array.Copy(sourceArray: args, sourceIndex: 2, destinationArray: returnArgs, destinationIndex: 0, length: returnArgs.Length);
            return returnArgs;
        }

        /// <summary>
        /// Set value locally
        /// </summary>
        /// <param name="value">Value</param>
        protected virtual bool TrySetValueLocal(bool? value)
        {
            Value = value;
            return true;
        }

        /// <summary>
        /// Parse 'true/false' values
        /// </summary>
        /// <param name="value">Values</param>
        /// <returns>Parse</returns>
        private bool? Parse(string value)
        {
            return _trueValues.Contains(value) ? true : (_falseValues.Contains(value) ? false : (bool?)null);
        }

        /// <summary>
        /// String representation of true value
        /// </summary>
        private List<string> _trueValues => new List<string>() { "true", "True", "1", "t", "T", "y", "Y" };

        /// <summary>
        /// String representation of false value
        /// </summary>
        private List<string> _falseValues => new List<string>() { "false", "False", "0", "f", "F", "n", "N" };

        /// <summary>
        /// Additional help info
        /// </summary>
        /// <returns>Additional help info</returns>
        protected override string AdditionalHelpInfo => $"<{string.Join("|", _trueValues)}|{string.Join("|", _falseValues)}>";
    }
}
