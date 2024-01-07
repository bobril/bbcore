using System;
using Njsast.Output;

namespace Njsast.Ast;

public class AstSymbolExternalImport : AstNode
{
    public readonly string ImportFile;
    public readonly string[] ImportSymbolPath;

    public AstSymbolExternalImport(string importFile, string[] importSymbolPath)
    {
        ImportFile = importFile;
        ImportSymbolPath = importSymbolPath;
    }

    public override AstNode ShallowClone()
    {
        return new AstSymbolExternalImport(ImportFile, ImportSymbolPath);
    }

    public override void CodeGen(OutputContext output)
    {
        throw new InvalidOperationException("Virtual AstSymbolExternalImport");
    }
}
