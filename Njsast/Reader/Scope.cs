using System.Collections.Generic;

namespace Njsast.Reader
{
    public sealed partial class Parser
    {
        sealed class Scope
        {
            public readonly HashSet<string> Var = new HashSet<string>();
            public readonly HashSet<string> Lexical = new HashSet<string>();
            public readonly HashSet<string> ChildVar = new HashSet<string>();
            public readonly HashSet<string> ParentLexical = new HashSet<string>();
        }

        // The functions in this module keep track of declared variables in the current scope in order to detect duplicate variable names.
        void EnterFunctionScope()
        {
            // var: a hash of var-declared names in the current lexical scope
            // lexical: a hash of lexically-declared names in the current lexical scope
            // childVar: a hash of var-declared names in all child lexical scopes of the current lexical scope (within the current function scope)
            // parentLexical: a hash of lexically-declared names in all parent lexical scopes of the current lexical scope (within the current function scope)
            _scopeStack.Push(new Scope());
        }

        void ExitFunctionScope()
        {
            _scopeStack.Pop();
        }

        void EnterLexicalScope()
        {
            var parentScope = _scopeStack.Peek();
            var childScope = new Scope();

            _scopeStack.Push(childScope);
            foreach (var str in parentScope.ParentLexical)
                childScope.ParentLexical.Add(str);
            foreach (var str in parentScope.Lexical)
                childScope.ParentLexical.Add(str);
        }

        void ExitLexicalScope()
        {
            var childScope = _scopeStack.Pop();
            var parentScope = _scopeStack.Peek();

            foreach (var str in childScope.ChildVar)
                parentScope.ChildVar.Add(str);
            foreach (var str in childScope.Var)
                parentScope.ChildVar.Add(str);
        }

        /**
         * A name can be declared with `var` if there are no variables with the same name declared with `let`/`const`
         * in the current lexical scope or any of the parent lexical scopes in this function.
         */
        bool CanDeclareVarName(string name)
        {
            var currentScope = _scopeStack.Peek();
            return !currentScope.Lexical.Contains(name) && !currentScope.ParentLexical.Contains(name);
        }

        /**
         * A name can be declared with `let`/`const` if there are no variables with the same name declared with `let`/`const`
         * in the current scope, and there are no variables with the same name declared with `var` in the current scope or in
         * any child lexical scopes in this function.
         */
        bool CanDeclareLexicalName(string name)
        {
            var currentScope = _scopeStack.Peek();
            return !currentScope.Lexical.Contains(name) && !currentScope.Var.Contains(name) && !currentScope.ChildVar.Contains(name);
        }

        void DeclareVarName(string name)
        {
            _scopeStack.Peek().Var.Add(name);
        }

        void DeclareLexicalName(string name)
        {
            _scopeStack.Peek().Lexical.Add(name);
        }
    }
}
