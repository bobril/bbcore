using System;
using System.Collections.Generic;
using Njsast.Ast;
using Njsast.Reader;

namespace Njsast.Scope
{
    public class FindBackReferencesAndEvalTreeWalker : TreeWalker
    {
        readonly ScopeOptions _options;
        readonly AstToplevel _astToplevel;

        public FindBackReferencesAndEvalTreeWalker(ScopeOptions options, AstToplevel astToplevel)
        {
            _options = options;
            _astToplevel = astToplevel;
            _astToplevel.Globals = new Dictionary<string, SymbolDef>();
        }

        protected override void Visit(AstNode node)
        {
            if (node is AstLoopControl loopControl && loopControl.Label != null)
            {
                loopControl.Label.Thedef!.References.Add(loopControl);
                StopDescending();
                return;
            }

            if (node is AstSymbol astSymbol)
            {
                var usage = SymbolUsage.Unknown;
                if (node is AstLabel)
                {
                }
                else if (node is AstSymbolFunarg || node is AstSymbolDefun || node is AstSymbolLambda ||
                         node is AstSymbolCatch || node is AstSymbolMethod)
                {
                    usage |= SymbolUsage.Write;
                }
                else
                    switch (Parent())
                    {
                        case AstVarDef astVarDef:
                            if (astVarDef.Name == node && astVarDef.Value != null)
                            {
                                usage |= SymbolUsage.Write;
                                var def = astSymbol.Thedef;
                                if (def!.Orig[0] == astSymbol && def.References.Count == 0 && Parent(2) == def.Scope)
                                {
                                    def.VarInit = astVarDef.Value;
                                }
                                else
                                {
                                    def!.References.Add(astSymbol);
                                }
                            }

                            if (astVarDef.Value == node)
                                usage |= SymbolUsage.Read;

                            // This handle case for(var init in object) where node structure is AstForIn > AstVar > AstVarDef
                            if (Parent(2) is AstForIn forIn && forIn.Init == Parent(1))
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
                        case AstBinary _:
                            usage |= SymbolUsage.Read;
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
                                usage |= SymbolUsage.Write;
                            }
                            else
                            {
                                usage |= SymbolUsage.Read;
                            }

                            break;
                        case AstPropAccess _:
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
                            usage |= SymbolUsage.Read;
                            break;
                        case AstConciseMethod conciseMethod:
                            if (conciseMethod.Key == astSymbol)
                            {
                                usage |= SymbolUsage.Write;
                            }

                            break;
                        case { } parent:
                            throw new NotImplementedException("Symbol Usage Detection parent " + parent.GetType().Name);
                    }

                astSymbol.Usage = usage;
            }

            if (node is AstSymbolRef astSymbolRef)
            {
                var name = astSymbolRef.Name;
                if (name == "eval" && Parent() is AstCall)
                {
                    for (var s = astSymbolRef.Scope; s != null && !s.UsesEval; s = s.ParentScope)
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
                if (astSymbolRef.Scope != null && astSymbolRef.Scope.IsBlockScope && !(sym.Orig[0] is AstSymbolBlockDeclaration))
                {
                    astSymbolRef.Scope = astSymbolRef.Scope.DefunScope();
                }

                StopDescending();
                return;
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
    }
}
