using System;
using System.Collections.Generic;
using Njsast.Output;
using Njsast.Reader;

namespace Njsast.Ast;

public readonly record struct AstTypeScriptEnumMember(
    string Name,
    AstNode KeyExpression,
    AstNode ReverseNameExpression,
    string? ReferenceName,
    string? Value,
    AstNode? ValueExpression,
    bool ForceReverseMap);

public class AstTypeScriptEnum : AstStatement
{
    public readonly string Name;
    public readonly bool IsExport;
    public readonly bool IsConst;
    public readonly bool IsLocal;
    public readonly bool PreserveConstEnum;
    public readonly bool EmitDeclaration;
    public readonly List<AstTypeScriptEnumMember> Members;

    public AstTypeScriptEnum(string? source, Position startPos, Position endPos, string name, bool isExport,
        bool isConst, bool isLocal, bool preserveConstEnum, List<AstTypeScriptEnumMember> members,
        bool emitDeclaration = true) :
        base(source, startPos, endPos)
    {
        Name = name;
        IsExport = isExport;
        IsConst = isConst;
        IsLocal = isLocal;
        PreserveConstEnum = preserveConstEnum;
        EmitDeclaration = emitDeclaration;
        Members = members;
    }

    public override void Visit(TreeWalker w)
    {
        base.Visit(w);
        foreach (var member in Members)
        {
            w.Walk(member.KeyExpression);
            w.Walk(member.ReverseNameExpression);
            if (member.ValueExpression != null)
                w.Walk(member.ValueExpression);
        }
    }

    public override void Transform(TreeTransformer tt)
    {
        throw new InvalidOperationException("TypeScript enum nodes must be lowered before generic transforms");
    }

    public override AstNode ShallowClone()
    {
        return new AstTypeScriptEnum(Source, Start, End, Name, IsExport, IsConst, IsLocal, PreserveConstEnum,
            new List<AstTypeScriptEnumMember>(Members), EmitDeclaration);
    }

    public override void CodeGen(OutputContext output)
    {
        throw new InvalidOperationException("TypeScript enum nodes must be lowered before code generation");
    }
}
