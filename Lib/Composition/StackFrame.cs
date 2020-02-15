using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

namespace Lib.Composition
{
    public class StackFrame
    {
        public string FunctionName;
        public List<string> Args;
        public string FileName;
        public int LineNumber;
        public int ColumnNumber;
        public override string ToString()
        {
            var functionName = FunctionName ?? "{anonymous}";
            var args = $"({string.Join(",", Args ?? new List<string>())})";
            var fileName = FileName != null ? ("@" + FileName) : "";
            var lineNumber = LineNumber != 0 ? (":" + LineNumber.ToString()) : "";
            var columnNumber = ColumnNumber != 0 ? (":" + ColumnNumber.ToString()) : "";
            return functionName + args + fileName + lineNumber + columnNumber;
        }

        static Regex CHROME_IE_STACK_REGEXP = new Regex("^\\s*at .*(\\S+\\:\\d+|\\(native\\))", RegexOptions.ECMAScript | RegexOptions.Multiline);
        static Regex SAFARI_NATIVE_CODE_REGEXP = new Regex("^(eval@)?(\\[native code\\])?$", RegexOptions.ECMAScript);
        static Regex ParseLocationRegexPar = new Regex("^\\((.+?)(?:\\:(\\d+))?(?:\\:(\\d+))?\\)\\D*$", RegexOptions.ECMAScript);
        static Regex ParseLocationRegex = new Regex("^(.+?)(?:\\:(\\d+))?(?:\\:(\\d+))?$", RegexOptions.ECMAScript);

        public static List<StackFrame> Parse(string stack)
        {
            if (CHROME_IE_STACK_REGEXP.IsMatch(stack))
            {
                return ParseV8OrIE(stack);
            }
            else
            {
                return ParseFFOrSafari(stack);
            }
        }

        static List<string> ExtractLocation(string urlLike)
        {
            // Fail-fast but return locations like "(native)"
            if (urlLike.IndexOf(':') < 0)
            {
                return new List<string> { urlLike };
            }
            var parts = ParseLocationRegexPar.Match(urlLike.Replace("()", ""));

            if (parts.Success)
            {
                var gg = (IReadOnlyList<Group>)parts.Groups;
                return new List<string> { gg.ElementAtOrDefault(1)?.Value, gg.ElementAtOrDefault(2)?.Value, gg.ElementAtOrDefault(3)?.Value };
            }
            parts = ParseLocationRegex.Match(urlLike.Replace("()", ""));
            var g = (IReadOnlyList<Group>)parts.Groups;
            return new List<string> { g.ElementAtOrDefault(1)?.Value, g.ElementAtOrDefault(2)?.Value, g.ElementAtOrDefault(3)?.Value };
        }

        static List<StackFrame> ParseV8OrIE(string stack)
        {
            var filtered = stack.Split('\n').Where(line => CHROME_IE_STACK_REGEXP.IsMatch(line));

            return filtered.Select(line =>
            {
                if (line.IndexOf("(eval ", StringComparison.Ordinal) > -1)
                {
                    // Throw away eval information
                    line = new Regex("(\\(eval at [^\\()]*)|(\\)\\,.*$)", RegexOptions.ECMAScript).Replace(line.Replace("eval code", "eval"), "");
                }
                var tokens = new Regex("\\s+").Split(line.TrimStart()).Skip(1).ToList();
                var locationParts = ExtractLocation(tokens.Last());
                tokens.RemoveAt(tokens.Count - 1);
                var functionName = string.Join(' ', tokens);
                var fileName = (locationParts[0] == "eval" || locationParts[0] == "<anonymous>") ? null : locationParts[0];
                return new StackFrame
                {
                    FunctionName = functionName,
                    Args = new List<string>(),
                    FileName = fileName,
                    LineNumber = int.Parse(locationParts.ElementAtOrDefault(1) ?? "0"),
                    ColumnNumber = int.Parse(locationParts.ElementAtOrDefault(2) ?? "0")
                };
            }).ToList();
        }

        static List<StackFrame> ParseFFOrSafari(string stack)
        {
            var filtered = stack.Split('\n').Where(line =>
            {
                return !SAFARI_NATIVE_CODE_REGEXP.IsMatch(line);
            });

            return filtered.Select(line =>
            {
                // Throw away eval information
                if (line.IndexOf(" > eval") >= 0)
                {
                    line = new Regex(" line (\\d+)(?: > eval line \\d+)* > eval\\:\\d+\\:\\d+", RegexOptions.ECMAScript).Replace(line, ":$1");
                }

                if (line.IndexOf('@') == -1 && line.IndexOf(':') == -1)
                {
                    // Safari eval frames only have function names and nothing else
                    return new StackFrame() { FunctionName = line, Args = new List<string>() };
                }

                var tokens = line.Split('@');
                var locationParts = ExtractLocation(tokens.Last());
                var functionName = string.Join('@', tokens.SkipLast(1));
                return new StackFrame
                {
                    FunctionName = functionName,
                    Args = new List<string>(),
                    FileName = locationParts.ElementAtOrDefault(0),
                    LineNumber = int.Parse(locationParts.ElementAtOrDefault(1) ?? "0"),
                    ColumnNumber = int.Parse(locationParts.ElementAtOrDefault(2) ?? "0")
                };
            }).ToList();
        }

    }
}
