namespace Lib.Utils.CommandLineParser.Parser
{
    /// <summary>
    /// Boolean Command line Argument
    /// </summary>
    public class CommandLineArgumentBool : CommandLineArgumentBoolNullable
    {
        /// <summary>
        /// Value
        /// </summary>
        public new bool Value { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="description">Description</param>
        /// <param name="words">Argument aliases</param>
        /// <param name="defaultValue">Default value</param>
        public CommandLineArgumentBool(string description, string[]? words, bool defaultValue = false) : base(description: description, words: words, defaultValue: defaultValue)
        {
            Value = defaultValue;
        }

        /// <summary>
        /// Try to set value locally
        /// </summary>
        /// <param name="value">Value</param>
        /// <returns>Value was set</returns>
        protected override bool TrySetValueLocal(bool? value)
        {
            if (value.HasValue)
                Value = value.Value;
            return value.HasValue;
        }
    }
}
