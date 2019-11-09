using System;
using Njsast.Ast;

namespace Njsast.Utils
{
    public sealed class SemanticError : Exception
    {
        public SemanticError(string message, AstNode node) :
            base(message)
        {
            Node = node;
        }

        public AstNode Node { get; }
    }
}