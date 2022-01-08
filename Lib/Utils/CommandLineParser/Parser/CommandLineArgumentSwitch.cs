using System;

namespace Lib.Utils.CommandLineParser.Parser;

/// <summary>
/// Switch Command line Argument
/// </summary>
public class CommandLineArgumentSwitch : CommandLineArgument
{
    /// <summary>
    /// Value
    /// </summary>
    public bool Value { get; private set; }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="description">Description</param>
    /// <param name="words">Argument aliases</param>
    public CommandLineArgumentSwitch(string description, string[]? words) : base(description: description, words: words, defaultValue: null)
    {
        Value = false;
    }

    /// <summary>
    /// Set value of argument
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Unused command line arguments</returns>
    public override string[] SetValue(string[] args)
    {
        Value = true;
        string[] returnArgs = new string[args.Length - 1];
        Array.Copy(sourceArray: args, sourceIndex: 1, destinationArray: returnArgs, destinationIndex: 0, length: returnArgs.Length);
        return returnArgs;
    }
}