using System;
using System.Collections.Generic;

namespace Lib.Utils.CommandLineParser.Parser
{
    public class CommandLineArgumentsDynamicStrings : CommandLineArgument
    {
        public List<string> Value { get; private set; }

        public CommandLineArgumentsDynamicStrings(string description, string[]? words) : base(description, words, "")
        {
            Value = new List<string>();
        }

        public override string[] SetValue(string[] args)
        {
            for (var i = 1; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                {
                    break;
                }
                Value.Add(args[i]);
            }

            
            var returnArgs = new string[args.Length - Value.Count - 1];
            Array.Copy(args, Value.Count + 1, returnArgs, 0, returnArgs.Length);

            return returnArgs;
        }
    }
}