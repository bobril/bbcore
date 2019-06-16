using System;
using System.Collections.Generic;
using Njsast.Ast;

namespace Njsast.Scope
{
    public class SetupScopeChainingAndHandleDefinitionsTreeWalker : TreeWalker
    {
        readonly ScopeOptions _options;
        AstScope _currentScope;
        AstScope _defun;
        AstDestructuring _inDestructuring;
        Dictionary<string, AstLabel> _labels;

        public SetupScopeChainingAndHandleDefinitionsTreeWalker(ScopeOptions options, AstToplevel astToplevel)
        {
            _options = options;
            _currentScope = astToplevel.ParentScope = null;
            _defun = null;
        }

        protected override void Visit(AstNode node)
        {
            if (node is IMayBeBlockScope blockScope && blockScope.IsBlockScope)
            {
                var saveScope = _currentScope;
                blockScope.BlockScope = _currentScope = new AstScope(node);
                _currentScope.InitScopeVars(saveScope);
                if (!(node is AstScope))
                {
                    _currentScope.UsesWith = saveScope.UsesWith;
                    _currentScope.UsesEval = saveScope.UsesEval;
                    _currentScope.HasUseStrictDirective = saveScope.HasUseStrictDirective;
                }

                DescendOnce();
                _currentScope = saveScope;
                return;
            }

            if (node is AstDestructuring destructuring)
            {
                _inDestructuring = destructuring; // These don't nest
                DescendOnce();
                _inDestructuring = null;
                return;
            }

            if (node is AstScope astScope)
            {
                astScope.InitScopeVars(_currentScope);
                var saveScope = _currentScope;
                var saveDefun = _defun;
                var saveLabels = _labels;
                _labels = new Dictionary<string, AstLabel>();
                _defun = _currentScope = astScope;
                DescendOnce();
                _defun = saveDefun;
                _currentScope = saveScope;
                _labels = saveLabels;
                return;
            }

            if (node is AstLabeledStatement labeledStatement)
            {
                var l = labeledStatement.Label;
                if (!_labels.TryAdd(l.Name, l))
                {
                    throw new Exception($"Label {l.Name} defined twice");
                }

                DescendOnce();
                _labels.Remove(l.Name);
                return;
            }

            if (node is AstWith)
            {
                for (var s = _currentScope; s != null; s = s.ParentScope)
                {
                    s.UsesWith = true;
                }

                return;
            }

            if (node is AstSymbol astSymbol)
            {
                astSymbol.Scope = _currentScope;
            }

            if (node is AstLabel astLabel)
            {
                astLabel.References = new StructList<AstLoopControl>();
            }
            else if (node is AstSymbolLambda astSymbolLambda)
            {
                _defun.DefFunction(astSymbolLambda, astSymbolLambda.Name == "arguments" ? null : _defun);
            }
            else if (node is AstSymbolDefun astSymbolDefun)
            {
                // This should be defined in the parent scope, as we encounter the
                // AstDefun node before getting to its AstSymbol.
                MarkExport((astSymbolDefun.Scope = _defun.ParentScope.Resolve()).DefFunction(astSymbolDefun, _defun),
                    1);
            }
            else if (node is AstSymbolClass)
            {
                MarkExport(_defun.DefVariable((AstSymbol) node, _defun), 1);
            }
            else if (node is AstSymbolImport)
            {
                _currentScope.DefVariable((AstSymbol) node, null);
            }
            else if (node is AstSymbolDefClass)
            {
                // This deals with the name of the class being available
                // inside the class.
                MarkExport((((AstSymbol) node).Scope = _defun.ParentScope).DefFunction((AstSymbol) node, _defun), 1);
            }
            else if (node is AstSymbolVar
                     || node is AstSymbolLet
                     || node is AstSymbolConst)
            {
                SymbolDef def;
                if (node is AstSymbolBlockDeclaration)
                {
                    def = _currentScope.DefVariable((AstSymbol) node, null);
                }
                else
                {
                    def = _defun.DefVariable((AstSymbol) node, null);
                }

                if (!def.Orig.All(sym =>
                {
                    if (sym == node) return true;
                    if (node is AstSymbolBlockDeclaration)
                    {
                        return sym is AstSymbolLambda;
                    }

                    return !(sym is AstSymbolLet || sym is AstSymbolConst);
                }))
                {
                    throw new Exception(((AstSymbol) node).Name + " redeclared");
                }

                MarkExport(def, 2);
                def.Destructuring = _inDestructuring;
                if (_defun != _currentScope)
                {
                    ((AstSymbol) node).MarkEnclosed(_options);
                    var def2 = _currentScope.FindVariable((AstSymbol) node);
                    if (((AstSymbol) node).Thedef != def2)
                    {
                        ((AstSymbol) node).Thedef = def2;
                        ((AstSymbol) node).Reference(_options);
                    }
                }
            }
            else if (node is AstSymbolCatch astSymbolCatch)
            {
                _currentScope.DefVariable(astSymbolCatch, null).Defun = _defun;
            }
            else if (node is AstLabelRef labelRef)
            {
                if (_labels.TryGetValue(labelRef.Name, out var sym))
                    labelRef.Thedef = sym;
                else
                    throw new Exception(
                        $"Undefined label {labelRef.Name} [{labelRef.Start.Line},{labelRef.Start.Column}]");
            }

            if (!(_currentScope is AstToplevel) && (node is AstExport || node is AstImport))
            {
                throw new Exception(node.PrintToString() + " statement may only appear at top level");
            }
        }

        void MarkExport(SymbolDef def, int level)
        {
            if (_inDestructuring != null)
            {
                var i = 0;
                do
                {
                    level++;
                } while (Parent(i++) != _inDestructuring);
            }

            var node = Parent(level);
            def.Export = node is AstExport;
        }
    }
}
