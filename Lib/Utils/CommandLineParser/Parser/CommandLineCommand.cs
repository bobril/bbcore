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
        /// List of sub commands
        /// </summary>
        public virtual List<CommandLineCommand> SubCommands { get; } = null;

        /// <summary>
        /// Command arguments
        /// </summary>
        List<CommandLineArgument> Arguments
        {
            get
            {
                var argumentProperties = GetType().GetProperties()
                    .Where(p => p.PropertyType.IsSubclassOf(typeof(CommandLineArgument))).ToList();
                return argumentProperties.ToList().Select(ap => (CommandLineArgument) ap.GetValue(this))
                    .ToList();
            }
        }

        /// <summary>
        /// Read arguments from command line arguments
        /// </summary>
        /// <param name="args">Command line arguments (at least one)</param>
        public CommandLineCommand ParseArguments(string[] args)
        {
            var firstArg = args[0];

            if (CommandLineParser.HelpWords.Contains(firstArg))
            {
                ShowHelp();
                return null;
            }

            var subCommands = SubCommands;

            if (!firstArg.StartsWith("-") && subCommands != null)
            {
                // get the command by the first argument
                var command = subCommands.FirstOrDefault(c => c.Words?.Contains(firstArg) ?? false);

                // remove first argument
                var commandArgs = args.Skip(1).ToArray();

                if (command == null)
                {
                    CommandLineParser.ShowHelp(subCommands);
                    return null;
                }

                if (commandArgs.Length > 0)
                    command = command.ParseArguments(commandArgs);

                return command;
            }

            if (subCommands != null)
            {
                var command = subCommands.FirstOrDefault(c => c.Words == null || c.Words.Contains(""));
                if (command != null)
                {
                    command = command.ParseArguments(args);
                    return command;
                }

                CommandLineParser.ShowHelp(subCommands);
                return null;
            }

            // list of arguments (it always creates new list instance, so it could be modified by ParseOneArgument)
            var arguments = Arguments;

            while (args?.Length > 0)
                args = ParseOneArgument(args, arguments);

            return args == null ? null : this;
        }

        /// <summary>
        /// Read one argument
        /// </summary>
        /// <param name="args">Command line arguments (at least one)</param>
        /// <param name="arguments">Argument definitions</param>
        /// <returns>Rest command line arguments</returns>
        string[] ParseOneArgument(string[] args, List<CommandLineArgument> arguments)
        {
            var firstArg = args[0].Trim();
            var argument = arguments.FirstOrDefault(a =>
                a.Words?.Contains(firstArg) ?? !firstArg.StartsWith('-'));
            if (argument == null)
            {
                Console.WriteLine("Unknown parameter " + firstArg);
                ShowHelp();
                return null;
            }

            var returnArgs = argument.SetValue(args);
            if (returnArgs == null)
            {
                ShowHelp();
                return null;
            }

            arguments.Remove(argument);
            return returnArgs;
        }

        public void ShowCommandHelp()
        {
            Console.WriteLine($"{(Words?.Length > 0 ? $"{string.Join("|", Words)}  " : "")}({Description})");
        }

        /// <summary>
        /// Show help
        /// </summary>
        public void ShowHelp()
        {
            ShowCommandHelp();
            Console.WriteLine($"  {string.Join("|", CommandLineParser.HelpWords)}  (help)");
            Arguments.ForEach(a => a.ShowHelp());
            var subCommands = SubCommands;
            if (subCommands != null)
            {
                Console.WriteLine();
                Console.WriteLine("Sub commands:");
                foreach (var subCommand in subCommands)
                {
                    Console.WriteLine();
                    subCommand.ShowHelp();
                }
            }
        }

        /// <summary>
        /// Writes all values, just for test
        /// </summary>
        public void WriteAllValues()
        {
            Arguments.ForEach(a =>
            {
                var name = string.Join("|", a.Words);
                var property = a.GetType().GetProperty("Value",
                    System.Reflection.BindingFlags.GetProperty | System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly);
                if (property == null)
                    property = a.GetType().GetProperty("Value",
                        System.Reflection.BindingFlags.GetProperty |
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Public);
                var value = property?.GetValue(a);
                if (value is string[])
                    value = string.Join("|", (string[]) value);
                Console.WriteLine($"{name}: {value}");
            });
        }
    }
}
