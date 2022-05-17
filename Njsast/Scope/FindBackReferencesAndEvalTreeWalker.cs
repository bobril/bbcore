using System;
using System.Collections.Generic;
using Newtonsoft.Json.Serialization;
using Njsast.Ast;
using Njsast.Reader;

namespace Njsast.Scope;

public class FindBackReferencesAndEvalTreeWalker : TreeWalker
{
    readonly ScopeOptions _options;
    readonly AstToplevel _astToplevel;

    public FindBackReferencesAndEvalTreeWalker(ScopeOptions options, AstToplevel astToplevel)
    {
        _options = options;
        _astToplevel = astToplevel;
        _astToplevel.Globals = new();
    }

    protected override void Visit(AstNode node)
    {
        if (node is AstLoopControl { Label: { } } loopControl)
        {
            loopControl.Label.Thedef!.References.Add(loopControl);
            StopDescending();
            return;
        }

        if (node is AstSymbolRef astSymbolRef)
        {
            var name = astSymbolRef.Name;
            if (name == "eval" && Parent() is AstCall)
            {
                for (var s = astSymbolRef.Scope; s is { UsesEval: false }; s = s.ParentScope)
                {
                    s.UsesEval = true;
                }
            }


            var sym = astSymbolRef.Scope?.FindVariable(name);
            if (sym == null || Parent() is AstNameMapping && (Parent(1) as AstImport)?.ModuleName != null)
            {
                sym = _astToplevel.DefGlobal(astSymbolRef);
            }
            else if (sym.Scope is AstLambda astLambda && name == "arguments")
            {
                astLambda.UsesArguments = true;
            }

            astSymbolRef.Thedef = sym;
            astSymbolRef.Reference(_options);
            if (astSymbolRef.Scope is { IsBlockScope: true } && sym.Orig[0] is not AstSymbolBlockDeclaration)
            {
                astSymbolRef.Scope = astSymbolRef.Scope.DefunScope();
            }
        }

        if (node is AstSymbol astSymbol)
        {
            var usage = SymbolUsage.Unknown;
            if (node is AstSymbolExport export)
            {
                if (export.Name != "*")
                    usage |= SymbolUsage.Read;
            }
            else if (node is AstLabel or AstSymbolProperty or AstSymbolMethod or AstSymbolCatch or AstSymbolFunarg or AstSymbolDefClass or AstSymbolExportForeign or AstSymbolImportForeign or AstSymbolImport)
            {
            }
            else
            {
                DetectSymbolUsage(node, 0, ref usage, astSymbol);
            }

            astSymbol.Usage = usage;
        }

        // ensure mangling works if catch reuses a scope variable
        if (node is AstSymbolCatch astSymbolCatch)
        {
            var def = astSymbolCatch.Thedef!.Redefined();
            if (def != null)
            {
                for (var s = astSymbolCatch.Scope; s != null; s = s.ParentScope)
                {
                    s.Enclosed.AddUnique(def);
                    if (s == def.Scope) break;
                }
            }
        }
    }

    void DetectSymbolUsage(AstNode node, int deepness, ref SymbolUsage usage, AstSymbol astSymbol)
    {
        var parent = Parent(deepness);
        switch (parent)
        {
            case AstVarDef astVarDef:
                if (astVarDef.Name == node)
                {
                    ((AstSymbolDeclaration)astSymbol).Init = astVarDef;

                    if (astVarDef.Value != null)
                    {
                        usage |= SymbolUsage.Write;
                        var def = astSymbol.Thedef!;
                        if (def.Orig[0] == astSymbol && def.References.Count == 0 &&
                            Parent(2 + deepness) == def.Scope)
                        {
                            def.VarInit = astVarDef.Value;
                        }
                        else
                        {
                            def.References.Add(astSymbol);
                        }
                    }
                }

                if (astVarDef.Value == node)
                    usage |= SymbolUsage.Read;

                // This handle case for(var init in object) where node structure is AstForIn > AstVar > AstVarDef
                if (Parent(2 + deepness) is AstForIn forIn && forIn.Init == Parent(1 + deepness))
                {
                    astSymbol.Thedef!.References.Add(astSymbol);
                    usage |= SymbolUsage.Write;
                }

                break;
            case AstAssign astAssign:
                if (astAssign.Left == node)
                {
                    usage |= SymbolUsage.Write;
                    if (astAssign.Operator != Operator.Assignment)
                        usage |= SymbolUsage.Read;
                }

                if (astAssign.Right == node)
                {
                    usage |= SymbolUsage.Read;
                }

                break;
            case AstDefaultAssign astDefaultAssign:
                if (astDefaultAssign.Left == node)
                {
                    usage |= SymbolUsage.Write;
                }

                if (astDefaultAssign.Right == node)
                {
                    usage |= SymbolUsage.Read;
                }

                break;
            case AstUnary astUnary:
                switch (astUnary.Operator)
                {
                    case Operator.IncrementPostfix:
                    case Operator.DecrementPostfix:
                    case Operator.Increment:
                    case Operator.Decrement:
                        usage |= SymbolUsage.ReadWrite;
                        break;
                    default:
                        usage |= SymbolUsage.Read;
                        break;
                }

                break;
            case AstForIn astForIn:
                if (astForIn.Init == node)
                {
                    usage |= SymbolUsage.Write;
                }

                if (astForIn.Object == node)
                {
                    usage |= SymbolUsage.Read;
                }

                break;
            case AstObjectKeyVal objectKeyVal:
                if (objectKeyVal.Key == astSymbol)
                {
                    usage |= SymbolUsage.Read;
                }
                else
                {
                    DetectSymbolUsage(parent, deepness + 1, ref usage, astSymbol);
                }

                break;
            case AstPropAccess propAccess:
                usage |= SymbolUsage.Read;
                if (propAccess.Expression == astSymbol)
                {
                    if (!IsPropAccessReadOnly(astSymbol)) usage |= SymbolUsage.PropWrite;
                }

                break;

            case AstBinary _:
            case AstCall _:
            case AstSimpleStatement _:
            case AstReturn _:
            case AstIf _:
            case AstSwitch _:
            case AstSequence _:
            case AstConditional _:
            case AstThrow _:
            case AstWith _:
            case AstArray _:
            case AstWhile _:
            case AstDo _:
            case AstCase _:
            case AstFor _:
            case AstPrefixedTemplateString _:
            case AstTemplateString _:
            case AstYield _:
            case AstObject _:
            case AstAwait _:
            case AstObjectGetter _:
            case AstObjectSetter _:
            case AstClass _: // extends
                usage |= SymbolUsage.Read;
                break;
            case AstExpansion _:
            case AstDestructuring _:
                DetectSymbolUsage(parent, deepness + 1, ref usage, astSymbol);
                break;

            case AstArrow _:
                usage |= SymbolUsage.Read;
                break;

            case AstLambda _:
                usage |= SymbolUsage.Write;
                break;
            case AstConciseMethod conciseMethod:
                if (conciseMethod.Key == astSymbol)
                {
                    usage |= SymbolUsage.Write;
                }

                break;
            case { }:
                throw new NotImplementedException("Symbol Usage Detection parent " + parent.GetType().Name);
        }
    }

    bool IsPropAccessReadOnly(AstNode? cur)
    {
        AstNode? par;
        var idx = -1;
        do
        {
            par = Parent(++idx);
            if (!(par is AstPropAccess propAccess)) break;
            if (propAccess.Property == cur)
                return true;
            if (propAccess.Expression == cur && cur.IsSymbolDef()?.VarInit?.IsRequireCall() != null)
            {
                return true;
            }

            cur = par;
        } while (true);

        switch (par)
        {
            case AstAssign astAssign:
                return astAssign.Right == cur;
            case AstCase _:
            case AstSimpleStatement _:
            case AstBinary _:
            case AstConditional _:
            case AstIf _:
            case AstFor _:
            case AstForIn _:
            case AstDwLoop _:
                return true;
            case AstUnary astUnary:
                switch (astUnary.Operator)
                {
                    case Operator.IncrementPostfix:
                    case Operator.DecrementPostfix:
                    case Operator.Increment:
                    case Operator.Decrement:
                        return false;
                }

                return true;
            case AstCall astCall:
            {
                if (Parent(idx - 1) == astCall.Expression)
                    return true;
                if (astCall.IsDefinePropertyExportsEsModule())
                    return true;
                if (astCall.Expression.IsSymbolDef()?.Init is AstLambda { Pure: true })
                    return true;
                return false;
            }
        }

        return false;
    }
}
