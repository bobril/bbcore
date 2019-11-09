using Njsast.Ast;

namespace Njsast
{
    public abstract class TreeWalker : TreeWalkerBase
    {
        bool _stopDescending;

        protected void StopDescending()
        {
            _stopDescending = true;
        }

        protected void Descend()
        {
            var top = Stack.Last;
            top.Visit(this);
        }

        protected void DescendOnce()
        {
            Descend();
            StopDescending();
        }

        protected abstract void Visit(AstNode node);

        public void Walk(AstNode? start)
        {
            if (start == null) return;
            Stack.Add(start);
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
                Stack.Pop();
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
