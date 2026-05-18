using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Njsast.Reader;
using Njsast.SourceMap;

namespace Njsast.Css;

public sealed class CssParserOptions
{
    public string? SourceFile { get; set; }
}

public sealed class CssOutputOptions
{
    public bool Beautify { get; set; }
    public int IndentLevel { get; set; } = 4;
    public int IndentStart { get; set; }
    public bool PreserveComments { get; set; } = true;
}

public sealed class CssParseException : Exception
{
    public CssParseException(string message, Position position) : base($"{message} ({position.ToShortString()})")
    {
        Position = position;
    }

    public Position Position { get; }
}

public abstract class CssNode
{
    protected CssNode(string? source, Position start, Position end)
    {
        Source = source;
        Start = start;
        End = end;
    }

    public string? Source { get; set; }
    public Position Start { get; set; }
    public Position End { get; set; }
    public abstract void Print(CssOutputContext output);

    public virtual IEnumerable<CssNode> Children()
    {
        yield break;
    }
}

public sealed class CssStylesheet : CssNode
{
    public CssStylesheet(string? source, Position start, Position end) : base(source, start, end)
    {
    }

    public List<CssNode> Nodes { get; } = new();

    public static CssStylesheet Concat(IEnumerable<CssStylesheet> stylesheets)
    {
        var result = new CssStylesheet(null, new(), new());
        foreach (var stylesheet in stylesheets)
        {
            result.Nodes.AddRange(stylesheet.Nodes);
            if (result.Source == null) result.Source = stylesheet.Source;
            if (result.Start.Index == 0 && stylesheet.Start.Index != 0) result.Start = stylesheet.Start;
            result.End = stylesheet.End;
        }

        return result;
    }

    public override IEnumerable<CssNode> Children() => Nodes;

    public override void Print(CssOutputContext output)
    {
        output.PrintNodeList(Nodes, topLevel: true);
    }

    public string PrintToString(CssOutputOptions? options = null)
    {
        var output = new CssOutputContext(options);
        Print(output);
        return output.ToString();
    }

    public void PrintToBuilder(SourceMapBuilder builder, CssOutputOptions? options = null)
    {
        var output = new CssOutputContext(options, builder);
        Print(output);
        builder.AddMapping(null, 0, 0, false);
    }
}

public sealed class CssRule : CssNode
{
    public CssRule(string? source, Position start, Position end, string selector) : base(source, start, end)
    {
        Selector = selector;
    }

    public string Selector { get; set; }
    public List<CssNode> Nodes { get; } = new();
    public override IEnumerable<CssNode> Children() => Nodes;

    public override void Print(CssOutputContext output)
    {
        output.PrintRaw(Selector.Trim());
        output.PrintBlock(Nodes);
    }
}

public sealed class CssAtRule : CssNode
{
    public CssAtRule(string? source, Position start, Position end, string name, string parameters) : base(source, start, end)
    {
        Name = name;
        Parameters = parameters;
    }

    public string Name { get; set; }
    public string Parameters { get; set; }
    public List<CssNode>? Nodes { get; set; }
    public override IEnumerable<CssNode> Children() => Nodes ?? Enumerable.Empty<CssNode>();

    public override void Print(CssOutputContext output)
    {
        output.PrintRaw("@");
        output.PrintRaw(Name);
        if (!string.IsNullOrWhiteSpace(Parameters))
        {
            output.PrintRaw(" ");
            output.PrintRaw(Parameters.Trim());
        }

        if (Nodes == null)
        {
            output.Semicolon();
            return;
        }

        output.PrintBlock(Nodes);
    }
}

public sealed class CssDeclaration : CssNode
{
    public CssDeclaration(string? source, Position start, Position end, string property, string value, bool important) : base(source, start, end)
    {
        Property = property;
        Value = value;
        Important = important;
    }

    public string Property { get; set; }
    public string Value { get; set; }
    public bool Important { get; set; }

    public override void Print(CssOutputContext output)
    {
        output.PrintRaw(Property.Trim());
        output.Colon();
        output.PrintRaw(Value.Trim());
        if (Important)
        {
            output.Space();
            output.PrintRaw("!important");
        }
        output.Semicolon();
    }
}

public sealed class CssComment : CssNode
{
    public CssComment(string? source, Position start, Position end, string text) : base(source, start, end)
    {
        Text = text;
    }

    public string Text { get; set; }

    public override void Print(CssOutputContext output)
    {
        if (!output.Options.PreserveComments) return;
        output.PrintRaw("/*");
        output.PrintRaw(Text);
        output.PrintRaw("*/");
    }
}

public sealed class CssOutputContext
{
    readonly SourceMapBuilder? _builder;
    readonly StringBuilder _content = new();
    int _indent;
    bool _lineStart = true;

    public CssOutputContext(CssOutputOptions? options = null, SourceMapBuilder? builder = null)
    {
        Options = options ?? new CssOutputOptions();
        _builder = builder;
        _indent = Options.IndentStart;
    }

    public CssOutputOptions Options { get; }

    public override string ToString() => _content.ToString();

    public void AddMapping(CssNode node)
    {
        _builder?.AddMapping(node.Source, node.Start.Line, node.Start.Column, true);
    }

    public void PrintRaw(string text)
    {
        if (text.Length == 0) return;
        if (Options.Beautify && _lineStart)
        {
            var spaces = new string(' ', _indent);
            _content.Append(spaces);
            _builder?.AddTextWithMapping(spaces);
            _lineStart = false;
        }
        _content.Append(text);
        _builder?.AddTextWithMapping(text);
    }

    public void Space()
    {
        if (Options.Beautify) PrintRaw(" ");
    }

    public void Colon()
    {
        PrintRaw(Options.Beautify ? ": " : ":");
    }

    public void Semicolon()
    {
        PrintRaw(";");
    }

    public void NewLine()
    {
        if (!Options.Beautify) return;
        PrintRaw("\n");
        _lineStart = true;
    }

    public void PrintBlock(List<CssNode> nodes)
    {
        if (Options.Beautify)
        {
            PrintRaw(" {");
            NewLine();
            _indent += Options.IndentLevel;
            PrintNodeList(nodes, topLevel: false);
            _indent -= Options.IndentLevel;
            PrintRaw("}");
        }
        else
        {
            PrintRaw("{");
            PrintNodeList(nodes, topLevel: false);
            PrintRaw("}");
        }
    }

    public void PrintNodeList(List<CssNode> nodes, bool topLevel)
    {
        foreach (var node in nodes)
        {
            if (node is CssComment && !Options.PreserveComments) continue;
            AddMapping(node);
            node.Print(this);
            if (Options.Beautify) NewLine();
        }
    }
}
