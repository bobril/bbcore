using Njsast.Ast;

namespace Njsast
{
    public abstract class TreeWalker
    {
        StructList<AstNode> _stack = new StructList<AstNode>();
        bool _stopDescending;

        protected void StopDescending()
        {
            _stopDescending = true;
        }

        protected AstNode Parent()
        {
            if (_stack.Count <= 1)
                return null;
            return _stack[_stack.Count - 2];
        }

        protected AstNode Parent(int generation)
        {
            if (_stack.Count <= 1 + generation)
                return null;
            return _stack[_stack.Count - 2 - (uint) generation];
        }

        protected T FindParent<T>() where T : AstNode
        {
            uint i = _stack.Count - 2;
            while (i < _stack.Count)
            {
                var p = _stack[i];
                if (p is T node)
                    return node;
                i--;
            }

            return null;
        }

        protected void Descend()
        {
            var top = _stack[_stack.Count - 1];
            top.Visit(this);
        }

        protected void DescendOnce()
        {
            Descend(); StopDescending();
        }

        protected abstract void Visit(AstNode node);

        public void Walk(AstNode start)
        {
            if (start == null) return;
            _stack.Add(start);
            var backupStopDescending = _stopDescending;
            try
            {
                _stopDescending = false;
                Visit(start);
                if (!_stopDescending)
                    Descend();
            }
            finally
            {
                _stopDescending = backupStopDescending;
                _stack.Pop();
            }
        }

        internal void WalkList<T>(in StructList<T> list) where T : AstNode
        {
            for (uint i = 0; i < list.Count; i++)
            {
                Walk(list[i]);
            }
        }
    }
}
