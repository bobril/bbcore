using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Njsast.Ast;

namespace Njsast
{
    public abstract class TreeWalkerBase
    {
        protected StructList<AstNode> Stack = new StructList<AstNode>();

        protected AstNode? Parent()
        {
            if (Stack.Count <= 1)
                return null;
            return Stack[Stack.Count - 2];
        }

        protected AstNode? Parent(int generation)
        {
            if (Stack.Count <= 1 + generation)
                return null;
            return Stack[Stack.Count - 2 - (uint) generation];
        }

        protected struct Enumerator : IEnumerator<AstNode>, IEnumerable<AstNode>
        {
            int _position;
            readonly AstNode[] _stack;

            public Enumerator(int start, AstNode[] stack)
            {
                _position = start;
                _stack = stack;
            }

            public bool MoveNext()
            {
                Debug.Assert(_position >= 0);
                _position--;
                return _position >= 0;
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }

            public AstNode Current => _stack[_position];

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public Enumerator GetEnumerator()
            {
                return this;
            }

            IEnumerator<AstNode> IEnumerable<AstNode>.GetEnumerator()
            {
                return this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this;
            }
        }

        protected Enumerator Parents()
        {
            return new Enumerator((int) Stack.Count - 1, Stack.UnsafeBackingArray);
        }

        protected T FindParent<T>() where T : AstNode?
        {
            var i = Stack.Count - 2;
            while (i < Stack.Count)
            {
                var p = Stack[i];
                if (p is T node)
                    return node;
                i--;
            }

            return null!;
        }

        protected AstNode? FindParent<T1, T2>() where T1 : AstNode? where T2 : AstNode?
        {
            var i = Stack.Count - 2;
            while (i < Stack.Count)
            {
                var p = Stack[i];
                if (p is T1 || p is T2)
                    return p;
                i--;
            }

            return null;
        }
    }
}
