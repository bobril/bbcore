using Njsast.AstDump;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

/// An `export` statement
public class AstExport : AstStatement
{
    /// [AstDefun|AstDefinitions|AstDefClass?] An exported definition
    public AstNode? ExportedDefinition;

    public AstNode? ExportedValue;

    /// Whether this is the default exported value of this module
    public bool IsDefault;

    /// List of exported names
    public StructRefList<AstNameMapping> ExportedNames;

    /// Name of the file to load exports from
    public AstString? ModuleName;

    public AstObject? Attributes;

    public string AttributeKeyword = "with";

    public AstExport(string? source, Position startPos, Position endPos, AstString? moduleName, AstNode? declaration,
        ref StructList<AstNameMapping> specifiers, AstObject? attributes = null, string attributeKeyword = "with") : base(source, startPos, endPos)
    {
        ModuleName = moduleName;
        Attributes = attributes;
        AttributeKeyword = attributeKeyword;
        if (declaration is AstDefun or AstDefinitions or AstDefClass)
        {
            ExportedDefinition = declaration;
        }
        else
        {
            ExportedValue = declaration;
        }

        ExportedNames.TransferFrom(ref specifiers);
    }

    public AstExport(string? source, Position startPos, Position endPos, AstNode declaration, bool isDefault) : base(
        source, startPos, endPos)
    {
        if (declaration is AstDefun or AstDefinitions or AstDefClass)
        {
            ExportedDefinition = declaration;
        }
        else
        {
            ExportedValue = declaration;
        }

        IsDefault = isDefault;
    }

    public AstExport(ref StructList<AstNameMapping> exportMappings)
    {
        ExportedNames.TransferFrom(ref exportMappings);
    }

    public override void Visit(TreeWalker w)
    {
        base.Visit(w);
        w.Walk(ModuleName);
        w.Walk(Attributes);
        w.Walk(ExportedDefinition);
        w.Walk(ExportedValue);
        w.WalkList(ExportedNames);
    }

    public override void Transform(TreeTransformer tt)
    {
        base.Transform(tt);
        if (ModuleName != null)
            ModuleName = (AstString)tt.Transform(ModuleName);
        if (Attributes != null)
            Attributes = (AstObject)tt.Transform(Attributes);
        if (ExportedDefinition != null)
            ExportedDefinition = tt.Transform(ExportedDefinition);
        if (ExportedValue != null)
            ExportedValue = tt.Transform(ExportedValue);
        tt.TransformList(ref ExportedNames);
    }

    public override void DumpScalars(IAstDumpWriter writer)
    {
        base.DumpScalars(writer);
        writer.PrintProp("IsDefault", IsDefault);
    }

    public override AstNode ShallowClone()
    {
        var res = new AstExport(Source, Start, End, ExportedDefinition ?? ExportedValue!, IsDefault);
        res.ModuleName = ModuleName;
        res.Attributes = Attributes;
        res.AttributeKeyword = AttributeKeyword;
        res.ExportedNames.AddRange(ExportedNames.AsReadOnlySpan());
        return res;
    }

    public override void CodeGen(OutputContext output)
    {
        output.Print("export");
        output.Space();
        if (IsDefault)
        {
            output.Print("default");
            output.Space();
        }

        var emptySpecifierList = ExportedNames.Count == 0 && ExportedValue == null && ExportedDefinition == null;
        if (ExportedNames.Count > 0 || emptySpecifierList)
        {
            if (ExportedNames.Count == 1 && ExportedNames[0].Name.Name == "*")
            {
                ExportedNames[0].Print(output);
            }
            else
            {
                output.Print("{");
                for (var i = 0u; i < ExportedNames.Count; i++)
                {
                    if (i > 0) output.Comma();
                    else output.Space();
                    ExportedNames[i].Print(output);
                }

                output.Space();
                output.Print("}");
            }
        }
        else if (ExportedValue != null)
        {
            ExportedValue.Print(output);
        }
        else if (ExportedDefinition != null)
        {
            ExportedDefinition.Print(output);
            if (ExportedDefinition is AstDefinitions) return;
        }

        if (ModuleName != null)
        {
            output.Space();
            output.Print("from");
            output.Space();
            ModuleName.Print(output);
            if (Attributes != null)
            {
                output.Space();
                output.Print(AttributeKeyword);
                output.Space();
                Attributes.Print(output);
            }
        }

        if (ExportedValue != null
            && !(ExportedValue is AstDefun ||
                 ExportedValue is AstFunction ||
                 ExportedValue is AstClass)
            || ModuleName != null
            || ExportedNames.Count > 0
            || emptySpecifierList
           )
        {
            output.Semicolon();
        }
    }
}
