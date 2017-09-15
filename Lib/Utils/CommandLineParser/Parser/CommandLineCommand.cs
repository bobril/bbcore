using System;
using System.Collections.Generic;
using System.Linq;

namespace Lib.Utils.CommandLineParser.Parser
{
    /// <summary>
    /// Command line Command class
    /// </summary>
    public abstract class CommandLineCommand
    {
        /// <summary>
        /// Command aliases
        /// </summary>
        public abstract string[] Words { get; }

        /// <summary>
        /// Command description
        /// </summary>
        protected abstract string Description { get; }

        /// <summary>
        /// Command arguments
        /// </summary>
        private List<CommandLineArgument> Arguments
        {
            get
            {
                var argumentProperties = GetType().GetProperties()
                    .Where(p => p.PropertyType.IsSubclassOf(typeof(CommandLineArgument))).ToList();
                return argumentProperties.ToList().Select(ap => (CommandLineArgument)ap.GetValue(this))
                    .ToList();
            }
        }

        /// <summary>
        /// Read arguments from command line arguments
        /// </summary>
        /// <param name="args">Command line arguments (at least one)</param>
        public CommandLineCommand ParseArguments(string[] args)
        {
            // help
            string firstArg = args[0];
            if (CommandLineParser.HelpWords.Contains(firstArg))
            {
                ShowHelp();
                return null;
            }

            // list of arguments
            var arguments = Arguments;

            while (args?.Length > 0)
                args = ParseOneArgument(args: args, arguments: arguments);

            return this;
        }

        /// <summary>
        /// Read one argument
        /// </summary>
        /// <param name="args">Command line arguments (at least one)</param>
        /// <param name="arguments">Argument definitions</param>
        /// <returns>Rest command line arguments</returns>
        private string[] ParseOneArgument(string[] args, List<CommandLineArgument> arguments)
        {
            string firstArg = args[0].Trim();
            CommandLineArgument argument = arguments.FirstOrDefault(a => a.Words.Contains(firstArg));
            if (argument == null)
            {
                ShowHelp();
                return null;
            }

            string[] returnArgs = argument.SetValue(args);
            if (returnArgs == null)
            {
                ShowHelp();
                return null;
            }

            arguments.Remove(argument);
            return returnArgs;
        }

        /// <summary>
        /// Show help
        /// </summary>
        public void ShowHelp()
        {
            Console.WriteLine($"{(Words?.Length > 0 ? $"{string.Join("|", Words)}  " : "")}({Description})");
            Console.WriteLine($"  {string.Join("|", CommandLineParser.HelpWords)}  (help)");
            Arguments.ForEach(a => a.ShowHelp());
        }

        /// <summary>
        /// Writes all values, just for test
        /// </summary>
        public void WriteAllValues()
        {
            Arguments.ForEach(a =>
            {
                string name = string.Join("|", a.Words);
                var property = a.GetType().GetProperty(name: "Value",
                    bindingAttr: System.Reflection.BindingFlags.GetProperty | System.Reflection.BindingFlags.Instance |
                                 System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly);
                if (property == null)
                    property = a.GetType().GetProperty(name: "Value",
                        bindingAttr: System.Reflection.BindingFlags.GetProperty | System.Reflection.BindingFlags.Instance |
                                     System.Reflection.BindingFlags.Public);
                var value = property?.GetValue(a);
                if (value is string[])
                    value = string.Join("|", (string[])value);
                Console.WriteLine($"{name}: {value}");
            });
        }
    }
}
