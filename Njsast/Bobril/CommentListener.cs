using Njsast.Ast;
using Njsast.Reader;
using System;
using System.Collections.Generic;

namespace Njsast.Bobril
{
    public class CommentListener : TreeWalker
    {
        readonly HashSet<string> _pureFunctionNames = new HashSet<string>();
        StructList<Position> _classStartsAfterPositions = new StructList<Position>();

        public void OnComment(bool block, string content, SourceLocation sourceLocation)
        {
            var c = content.AsSpan().Trim();
            if (!block && c.StartsWith("PureFuncs:", StringComparison.Ordinal))
            {
                c = c.Slice(10);
                while (c.Length > 0)
                {
                    var pos = c.IndexOf(',');
                    if (pos < 0) pos = c.Length;
                    var functionName = c.Slice(0, pos).Trim();
                    if (functionName.Length > 0)
                    {
                        _pureFunctionNames.Add(functionName.ToString());
                    }

                    if (c.Length == pos) break;
                    c = c.Slice(pos + 1);
                }
            }
            else if (block && c.IndexOf("@class") >= 0)
            {
                _classStartsAfterPositions.Add(sourceLocation.End);
            }
        }

        protected override void Visit(AstNode node)
        {
            if (node is AstLambda func)
            {
                if (func.Name != null)
                {
                    if (_pureFunctionNames.Contains(func.Name.Name))
                    {
                        func.Pure = true;
                        return;
                    }
                }
                for (var i = 0u; i < _classStartsAfterPositions.Count; i++)
                {
                    var pos = _classStartsAfterPositions[i];
                    if (pos.Line == func.Start.Line && ((uint)(func.Start.Column - pos.Column)) < 10)
                    {
                        func.Pure = true;
                        return;
                    }
                }
            }
        }
    }
}
