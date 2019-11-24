using System;
using Njsast.Ast;
using Njsast.Output;

namespace Njsast
{
    public abstract class TreeTransformer : TreeWalkerBase
    {
        class AstSpreadStructList<T> : AstNode where T : AstNode
        {
            public readonly StructList<T> NodeList = new StructList<T>();

            public AstSpreadStructList(ref StructList<T> nodeList)
            {
                NodeList.TransferFrom(ref nodeList);
            }

            public override void Visit(TreeWalker w)
            {
                throw new InvalidOperationException();
            }

            public override void Transform(TreeTransformer tt)
            {
                throw new InvalidOperationException();
            }

            public override AstNode ShallowClone()
            {
                throw new InvalidOperationException();
            }

            public override void CodeGen(OutputContext output)
            {
                throw new InvalidOperationException();
            }
        }

        class AstRemoveMe : AstNode
        {
            public AstRemoveMe()
            {
            }

            public override void Visit(TreeWalker w)
            {
                throw new InvalidOperationException();
            }

            public override void Transform(TreeTransformer tt)
            {
                throw new InvalidOperationException();
            }

            public override AstNode ShallowClone()
            {
                return Remove;
            }

            public override void CodeGen(OutputContext output)
            {
                throw new InvalidOperationException();
            }
        }

        public static readonly AstNode Remove = new AstRemoveMe();

        protected static AstNode SpreadStructList(AstBlock block)
        {
            return new AstSpreadStructList<AstNode>(ref block.Body);
        }

        protected static AstNode SpreadStructList(ref StructList<AstNode> statements)
        {
            return new AstSpreadStructList<AstNode>(ref statements);
        }

        protected void Descend()
        {
            var top = Stack.Last;
            top.Transform(this);
        }

        public bool Modified { get; set; }
        /// Before descend if returns non null, descending will be skipped and result directly returned from Transform
        protected abstract AstNode? Before(AstNode node, bool inList);

        /// After descend if returns non null it will be returned from Transform
        protected abstract AstNode? After(AstNode node, bool inList);

        public AstNode Transform(AstNode start, bool inList = false)
        {
            Stack.Add(start);
            try
            {
                var x = Before(start, inList);
                if (x != null)
                {
                    if (x != start) Modified = true;
                    return x;
                }
                start.Transform(this);
                x = After(start, inList);
                if (x == null) return start;
                if (x != start) Modified = true;
                return x;
            }
            finally
            {
                Stack.Pop();
            }
        }

        internal void TransformList<T>(ref StructList<T> list) where T : AstNode
        {
            for (var i = 0; i < list.Count; i++)
            {
                var originalNode = list[i];
                var item = Transform(originalNode, true);
                if (item == Remove)
                {
                    list.RemoveAt(i);
                    Modified = true;
                    i--;
                }
                else if (item is AstSpreadStructList<T> spreadList)
                {
                    list.ReplaceItemAt(i, spreadList.NodeList);
                    Modified = true;
                }
                else
                {
                    if (originalNode != item)
                        Modified = true;
                    list[i] = (T)item;
                }
            }
        }
    }
}
