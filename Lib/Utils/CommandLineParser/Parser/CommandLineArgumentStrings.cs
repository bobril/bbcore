using System;
using System.Linq;

namespace Lib.Utils.CommandLineParser.Parser
{
    /// <summary>
    /// Command line argument with three values
    /// </summary>
    public class CommandLineArgumentStrings : CommandLineArgument
    {
        /// <summary>
        /// Value
        /// </summary>
        public string[] Value { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="description">Description</param>
        /// <param name="words">Words</param>
        /// <param name="valuesCount">Count of values</param>
        /// <param name="defaultValue">Default value</param>
        public CommandLineArgumentStrings(string description, string[] words, int valuesCount, string[] defaultValue = null) : base(description: description, words: words, defaultValue: defaultValue?.Length == valuesCount ? $" <{string.Join("> <", defaultValue)}> " : "")
        {
            Value = new string[valuesCount];
            if (defaultValue?.Length == valuesCount)
                Array.Copy(sourceArray: defaultValue, destinationArray: Value, length: valuesCount);
        }

        /// <summary>
        /// Set value
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Rest arguments</returns>
        public override string[] SetValue(string[] args)
        {
            if (args?.Length < Value.Length + 1)
                return null;

            Array.Copy(sourceArray: args, sourceIndex: 1, destinationArray: Value, destinationIndex: 0, length: Value.Length);
            string[] returnArgs = new string[args.Length - Value.Length - 1];
            Array.Copy(sourceArray: args, sourceIndex: Value.Length + 1, destinationArray: returnArgs, destinationIndex: 0, length: returnArgs.Length);

            return returnArgs;
        }

        /// <summary>
        /// Additional helper info
        /// </summary>
        protected override string AdditionalHelpInfo => $"<{string.Join("> <", Enumerable.Repeat("value", Value.Length))}>";
    }
}
