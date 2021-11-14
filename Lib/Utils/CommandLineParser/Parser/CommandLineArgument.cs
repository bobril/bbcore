using System;

namespace Lib.Utils.CommandLineParser.Parser
{
    /// <summary>
    /// Command line Argument base class
    /// </summary>
    public abstract class CommandLineArgument
    {
        /// <summary>
        /// Argument aliases
        /// </summary>
        public string[]? Words { get; private set; }

        /// <summary>
        /// Argument description
        /// </summary>
        protected string Description { get; private set; }

        /// <summary>
        /// Default value in string
        /// </summary>
        string _defaultValueString;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="description">Description</param>
        /// <param name="words">Argument aliases</param>
        /// <param name="defaultValue">Default value</param>
        protected CommandLineArgument(string description, string[]? words, string defaultValue)
        {
            Words = words;
            Description = description;
            _defaultValueString = defaultValue;
        }

        /// <summary>
        /// Set value of argument
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Unused command line arguments</returns>
        public abstract string[] SetValue(string[] args);

        /// <summary>
        /// Show help
        /// </summary>
        public void ShowHelp()
        {
            if (Words == null)
                Console.WriteLine(
                    $"{(string.IsNullOrWhiteSpace(AdditionalHelpInfo) ? "  [name]" : $"  {AdditionalHelpInfo}")}{(string.IsNullOrWhiteSpace(_defaultValueString) ? "" : $"  [default value: <{_defaultValueString}>]")}  ({Description})");
            else
                Console.WriteLine(
                    $"  {string.Join("|", Words)}{(string.IsNullOrWhiteSpace(AdditionalHelpInfo) ? "" : $"  {AdditionalHelpInfo}")}{(string.IsNullOrWhiteSpace(_defaultValueString) ? "" : $"  [default value: <{_defaultValueString}>]")}  ({Description})");
        }

        /// <summary>
        /// Additional help info
        /// </summary>
        /// <returns>Additional help info</returns>
        protected virtual string AdditionalHelpInfo => null;
    }
}
