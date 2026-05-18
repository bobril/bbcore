using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Njsast.Css;

public static class CssMinifier
{
    static readonly Regex ZeroUnitRegex =
        new(@"(?<![\w.-])([+-]?)0+(?:\.0+)?(?:px|em|rem|ex|ch|vw|vh|vmin|vmax|cm|mm|q|in|pt|pc|deg|grad|rad|turn|s|ms|Hz|kHz)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex DecimalRegex =
        new(@"(?<![\w.-])([+-]?)(\d+)\.(\d+)([a-z%]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex LeadingZeroRegex =
        new(@"(?<![\w.-])([+-]?)0\.(\d+)([a-z%]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex WholeNumberLeadingZeroRegex =
        new(@"(?<![\w.-])([+-]?)0+([1-9]\d*)([a-z%]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex HexColorRegex =
        new(@"#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})\b", RegexOptions.Compiled);

    static readonly Dictionary<string, string> ColorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = "#000",
        ["white"] = "#fff",
        ["red"] = "red",
        ["blue"] = "blue",
        ["gray"] = "gray",
        ["grey"] = "grey",
        ["green"] = "green",
        ["transparent"] = "#0000"
    };

    public static void Minify(CssStylesheet stylesheet)
    {
        MinifyList(stylesheet.Nodes);
    }

    static void MinifyList(List<CssNode> nodes)
    {
        for (var i = nodes.Count - 1; i >= 0; i--)
        {
            switch (nodes[i])
            {
                case CssComment:
                    nodes.RemoveAt(i);
                    break;
                case CssDeclaration declaration:
                    declaration.Property = declaration.Property.Trim();
                    if (!declaration.Property.StartsWith("--", StringComparison.Ordinal))
                        declaration.Value = MinifyValue(declaration.Value);
                    else
                        declaration.Value = declaration.Value.Trim();
                    break;
                case CssRule rule:
                    rule.Selector = MinifySelector(rule.Selector);
                    MinifyList(rule.Nodes);
                    if (rule.Nodes.Count == 0)
                        nodes.RemoveAt(i);
                    break;
                case CssAtRule atRule:
                    atRule.Parameters = MinifyAtRuleParameters(atRule.Name, atRule.Parameters);
                    if (atRule.Nodes != null)
                    {
                        MinifyList(atRule.Nodes);
                        if (atRule.Nodes.Count == 0 && !KeepEmptyAtRule(atRule.Name))
                            nodes.RemoveAt(i);
                    }
                    break;
            }
        }
    }

    static bool KeepEmptyAtRule(string name)
    {
        return name.Equals("font-face", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("keyframes", StringComparison.OrdinalIgnoreCase);
    }

    static string MinifyAtRuleParameters(string name, string parameters)
    {
        parameters = CollapseWhitespace(parameters.Trim());
        if (name.EndsWith("keyframes", StringComparison.OrdinalIgnoreCase))
            return parameters;
        return MinifyValue(parameters);
    }

    static string MinifySelector(string selector)
    {
        return TrimPunctuationWhitespace(CollapseWhitespace(selector.Trim()));
    }

    static string MinifyValue(string value)
    {
        value = TrimPunctuationWhitespace(CollapseWhitespace(value.Trim()));
        value = DecimalRegex.Replace(value, match =>
        {
            var sign = match.Groups[1].Value;
            var integer = match.Groups[2].Value.TrimStart('0');
            if (integer.Length == 0) integer = "0";
            var fraction = match.Groups[3].Value.TrimEnd('0');
            var unit = match.Groups[4].Value;
            return fraction.Length == 0 ? sign + integer + unit : sign + integer + "." + fraction + unit;
        });
        value = WholeNumberLeadingZeroRegex.Replace(value, "$1$2$3");
        value = ZeroUnitRegex.Replace(value, "0");
        value = LeadingZeroRegex.Replace(value, "$1.$2$3");
        value = HexColorRegex.Replace(value, ShortenHexColor);
        value = ReplaceColorNames(value);
        return value;
    }

    static string ReplaceColorNames(string value)
    {
        var result = new StringBuilder(value.Length);
        var i = 0;
        while (i < value.Length)
        {
            if (IsIdentStart(value[i]))
            {
                var start = i;
                i++;
                while (i < value.Length && IsIdent(value[i])) i++;
                var word = value[start..i];
                if (ColorNames.TryGetValue(word, out var replacement) && replacement.Length < word.Length)
                    result.Append(replacement);
                else
                    result.Append(word);
                continue;
            }

            result.Append(value[i]);
            i++;
        }

        return result.ToString();
    }

    static string ShortenHexColor(Match match)
    {
        var hex = match.Groups[1].Value.ToLowerInvariant();
        if (hex.Length == 6 && hex[0] == hex[1] && hex[2] == hex[3] && hex[4] == hex[5])
            return "#" + hex[0] + hex[2] + hex[4];
        if (hex.Length == 8 && hex[0] == hex[1] && hex[2] == hex[3] && hex[4] == hex[5] && hex[6] == hex[7])
            return "#" + hex[0] + hex[2] + hex[4] + hex[6];
        return "#" + hex;
    }

    static string CollapseWhitespace(string text)
    {
        var result = new StringBuilder(text.Length);
        var inWhitespace = false;
        var quote = '\0';
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quote != '\0')
            {
                result.Append(ch);
                if (ch == '\\' && i + 1 < text.Length)
                {
                    result.Append(text[++i]);
                }
                else if (ch == quote)
                {
                    quote = '\0';
                }
                continue;
            }

            if (ch is '"' or '\'')
            {
                FlushWhitespace(result, ref inWhitespace);
                quote = ch;
                result.Append(ch);
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                inWhitespace = true;
                continue;
            }

            FlushWhitespace(result, ref inWhitespace);
            result.Append(ch);
        }

        return result.ToString();
    }

    static void FlushWhitespace(StringBuilder result, ref bool inWhitespace)
    {
        if (!inWhitespace) return;
        if (result.Length > 0)
            result.Append(' ');
        inWhitespace = false;
    }

    static string TrimPunctuationWhitespace(string text)
    {
        var result = new StringBuilder(text.Length);
        var quote = '\0';
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quote != '\0')
            {
                result.Append(ch);
                if (ch == '\\' && i + 1 < text.Length)
                {
                    result.Append(text[++i]);
                }
                else if (ch == quote)
                {
                    quote = '\0';
                }
                continue;
            }

            if (ch is '"' or '\'')
            {
                quote = ch;
                result.Append(ch);
                continue;
            }

            if (ch == ' ' && (NextIsPunctuation(text, i + 1) || PreviousIsPunctuation(result)))
                continue;

            if (IsPunctuation(ch) && result.Length > 0 && result[^1] == ' ')
                result.Length--;

            result.Append(ch);
        }

        return result.ToString();
    }

    static bool PreviousIsPunctuation(StringBuilder result)
    {
        return result.Length > 0 && IsPunctuation(result[^1]);
    }

    static bool NextIsPunctuation(string text, int start)
    {
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == ' ') continue;
            return IsPunctuation(text[i]);
        }

        return false;
    }

    static bool IsPunctuation(char ch)
    {
        return ch is ':' or ';' or ',' or '>' or '+' or '~' or '(' or ')' or '[' or ']' or '{' or '}';
    }

    static bool IsIdentStart(char ch)
    {
        return char.IsLetter(ch) || ch == '_' || ch == '-';
    }

    static bool IsIdent(char ch)
    {
        return IsIdentStart(ch) || char.IsDigit(ch);
    }
}
