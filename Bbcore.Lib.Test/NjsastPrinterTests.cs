using Njsast.Ast;

namespace Bbcore.Lib.Test;

public class NjsastPrinterTests
{
    [Fact]
    public void ObjectStringKeyHashIsQuoted()
    {
        var obj = new AstObject();
        obj.Properties.Add(new AstObjectKeyVal(new AstString("@"), new AstNumber(60)));
        obj.Properties.Add(new AstObjectKeyVal(new AstString("#"), new AstNumber(63)));
        obj.Properties.Add(new AstObjectKeyVal(new AstString(""), new AstNumber(62)));

        Assert.Equal("{\"@\":60,\"#\":63,\"\":62}", obj.PrintToString());
    }
}
