using System;
using System.Collections.Generic;

namespace Njsast.Reader
{
    public sealed partial class Parser
    {
        public static List<TokContext> InitialContext()
        {
            return new List<TokContext>
            {
                TokContext.BStat
            };
        }

        bool BraceIsBlock(TokenType prevType)
        {
            var parent = CurContext();
            if (parent == TokContext.FExpr || parent == TokContext.FStat)
                return true;
            if (prevType == TokenType.Colon && (parent == TokContext.BStat || parent == TokContext.BExpr))
                return !parent.IsExpression;

            // The check for `tt.name && exprAllowed` detects whether we are
            // after a `yield` or `of` construct. See the `updateContext` for
            // `tt.name`.
            if (prevType == TokenType.Return || prevType == TokenType.Name && _exprAllowed)
                return LineBreak.IsMatch(_input.Substring(_lastTokEnd.Index, Start.Index - _lastTokEnd.Index));
            if (prevType == TokenType.Else || prevType == TokenType.Semi || prevType == TokenType.Eof || prevType == TokenType.ParenR || prevType == TokenType.Arrow)
                return true;
            if (prevType == TokenType.BraceL)
                return parent == TokContext.BStat;
            if (prevType == TokenType.Var || prevType == TokenType.Name)
                return false;
            return !_exprAllowed;
        }

        bool InGeneratorContext()
        {
            for (var i = _context.Count - 1; i >= 1; i--)
            {
                if (_context[i].Token == "function")
                    return _context[i].Generator;
            }
            return false;
        }

        void UpdateContext(TokenType prevType)
        {
            Action<Parser, TokenType>? update;
            if (TokenInformation.Types[Type].Keyword != null && prevType == TokenType.Dot)
                _exprAllowed = false;
            else if ((update = TokenInformation.Types[Type].UpdateContext) != null)
                update(this, prevType);
            else
                _exprAllowed = TokenInformation.Types[Type].BeforeExpression;
        }

        internal static void ParenBraceRUpdateContext(Parser parser, TokenType _)
        {
            if (parser._context.Count == 1)
            {
                parser._exprAllowed = true;
                return;
            }
            var @out = parser._context.Pop();
            if (@out == TokContext.BStat && parser.CurContext().Token == "function")
            {
                @out = parser._context.Pop();
            }
            parser._exprAllowed = !@out.IsExpression;
        }

        internal static void BraceLUpdateContext(Parser parser, TokenType prevType)
        {
            parser._context.Add(parser.BraceIsBlock(prevType) ? TokContext.BStat : TokContext.BExpr);
            parser._exprAllowed = true;
        }

        internal static void DollarBraceLUpdateContext(Parser parser, TokenType prevType)
        {
            parser._context.Add(TokContext.BTmpl);
            parser._exprAllowed = true;
        }

        internal static void ParenLUpdateContext(Parser parser, TokenType prevType)
        {
            var statementParens = prevType == TokenType.If || prevType == TokenType.For || prevType == TokenType.With || prevType == TokenType.While;
            parser._context.Add(statementParens ? TokContext.PStat : TokContext.PExpr);
            parser._exprAllowed = true;
        }

        internal static void IncDecUpdateContext(Parser parser, TokenType prevType)
        {
            // tokExprAllowed stays unchanged
        }

        internal static void FunctionClassUpdateContext(Parser parser, TokenType prevType)
        {
            if (TokenInformation.Types[prevType].BeforeExpression && prevType != TokenType.Semi && prevType != TokenType.Else &&
                !((prevType == TokenType.Colon || prevType == TokenType.BraceL) && parser.CurContext() == TokContext.BStat))
                parser._context.Add(TokContext.FExpr);
            else
                parser._context.Add(TokContext.FStat);
            parser._exprAllowed = false;
        }

        internal static void BackQuoteUpdateContext(Parser parser, TokenType prevType)
        {
            if (parser.CurContext() == TokContext.QTmpl)
                parser._context.Pop();
            else
                parser._context.Add(TokContext.QTmpl);
            parser._exprAllowed = false;
        }

        internal static void StarUpdateContext(Parser parser, TokenType prevType)
        {
            if (prevType == TokenType.Function)
            {
                var index = parser._context.Count - 1;
                if (parser._context[index] == TokContext.FExpr)
                    parser._context[index] = TokContext.FExprGen;
                else
                    parser._context[index] = TokContext.FGen;
            }
            parser._exprAllowed = true;
        }

        internal static void NameUpdateContext(Parser parser, TokenType prevType)
        {
            var allowed = false;
            if (parser.Options.EcmaVersion >= 6)
            {
                if ("of".Equals(parser.Value) && !parser._exprAllowed ||
                    "yield".Equals(parser.Value) && parser.InGeneratorContext())
                    allowed = true;
            }
            parser._exprAllowed = allowed;
        }
    }
}
