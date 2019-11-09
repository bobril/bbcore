using System;

namespace Lib.Utils.CommandLineParser.Parser
{
    /// <summary>
    /// String Command line Argument
    /// </summary>
    public class CommandLineArgumentString : CommandLineArgument
    {
        /// <summary>
        /// Value
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="description">Description</param>
        /// <param name="words">Argument aliases</param>
        /// <param name="defaultValue">Default value</param>
        public CommandLineArgumentString(string description, string[] words, string? defaultValue = null) : base(description, words, defaultValue)
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
            if (Words == null)
            {
                if (args.Length < 1)
                    return null;

                if (!CheckValue(args[0]))
                    return null;

                Value = args[0];
                var returnArgs = new string[args.Length - 1];
                Array.Copy(args, 1, returnArgs, 0, returnArgs.Length);
                return returnArgs;
            }
            else
            {
                if (args.Length < 2)
                    return null;

                if (!CheckValue(args[1]))
                    return null;

                Value = args[1];
                var returnArgs = new string[args.Length - 2];
                Array.Copy(args, 2, returnArgs, 0, returnArgs.Length);
                return returnArgs;
            }
        }

        /// <summary>
        /// Additional help info
        /// </summary>
        /// <returns>Additional help info</returns>
        protected override string AdditionalHelpInfo => "<value>";

        /// <summary>
        /// Check value
        /// </summary>
        /// <param name="value">Value</param>
        /// <returns>Correct</returns>
        protected virtual bool CheckValue(string value)
        {
            return true;
        }
    }
}
