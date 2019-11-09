using System.Linq;

namespace Lib.Utils.CommandLineParser.Parser
{
    /// <summary>
    /// Command line argument with enum
    /// </summary>
    public class CommandLineArgumentEnumValues : CommandLineArgumentString
    {
        /// <summary>
        /// Enum values
        /// </summary>
        string[] _enumValues;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="description">Description</param>
        /// <param name="words">Argument aliases</param>
        /// <param name="enumValues">Enum values</param>
        /// <param name="defaultValue">Default value</param>
        public CommandLineArgumentEnumValues(string description, string[] words, string[] enumValues, string? defaultValue = null) : base(
            description, words, defaultValue)
        {
            _enumValues = enumValues;
        }

        /// <summary>
        /// Additional help info
        /// </summary>
        /// <returns>Additional help info</returns>
        protected override string AdditionalHelpInfo => $"<{string.Join("|", _enumValues)}>";

        /// <summary>
        /// Check value
        /// </summary>
        /// <param name="value">Value</param>
        /// <returns>Correct</returns>
        protected override bool CheckValue(string value)
        {
            return _enumValues.Contains(value);
        }
    }
}
