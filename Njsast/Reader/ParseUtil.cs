using Njsast.Ast;

namespace Njsast.Reader
{
    public sealed partial class Parser
    {
        // Predicate that tests whether the next token is of the given
        // type, and if yes, consumes it as a side effect.
        bool Eat(TokenType type)
        {
            if (Type == type)
            {
                Next();
                return true;
            }
            return false;
        }

        // Tests whether parsed token is a contextual keyword.
        bool IsContextual(string name)
        {
            return Type == TokenType.Name && name.Equals(Value);
        }

        // Consumes contextual keyword if possible.
        bool EatContextual(string name)
        {
            return name.Equals(Value) && Eat(TokenType.Name);
        }

        // Asserts that following token is given contextual keyword.
        void ExpectContextual(string name)
        {
            if (!EatContextual(name))
            {
                Raise(Start, "Unexpected token");
            }
        }

        // Test whether a semicolon can be inserted at the current position.
        bool CanInsertSemicolon()
        {
            return Type == TokenType.Eof ||
                   Type == TokenType.BraceR ||
                   LineBreak.IsMatch(_input.Substring(_lastTokEnd.Index, Start.Index - _lastTokEnd.Index));
        }

        bool InsertSemicolon()
        {
            if (CanInsertSemicolon())
            {
                return true;
            }
            return false;
        }

        // Consume a semicolon, or, failing that, see if we are allowed to
        // pretend that there is a semicolon at this position.
        void Semicolon()
        {
            if (!Eat(TokenType.Semi) && !InsertSemicolon())
            {
                Raise(Start, "Unexpected token");
            }
        }

        bool AfterTrailingComma(TokenType tokType, bool notNext = false)
        {
            if (Type == tokType)
            {
                if (!notNext)
                    Next();
                return true;
            }
            return false;
        }

        // Expect a token of a given type. If found, consume it, otherwise,
        // raise an unexpected token error.
        void Expect(TokenType type)
        {
            if (!Eat(type))
            {
                Raise(Start, "Unexpected token");
            }
        }

        public sealed class DestructuringErrors
        {
            public Position ShorthandAssign;
            public Position TrailingComma;
            public Position ParenthesizedAssign;
            public Position ParenthesizedBind;

            public void Reset()
            {
                ShorthandAssign = default;
                TrailingComma = default;
                ParenthesizedAssign = default;
                ParenthesizedBind = default;
            }
        }

        static void CheckPatternErrors(DestructuringErrors? refDestructuringErrors, bool isAssign)
        {
            if (refDestructuringErrors == null) return;
            if (refDestructuringErrors.TrailingComma.Line > 0)
            {
                RaiseRecoverable(refDestructuringErrors.TrailingComma, "Comma is not permitted after the rest element");
            }
            var parens = isAssign ? refDestructuringErrors.ParenthesizedAssign : refDestructuringErrors.ParenthesizedBind;
            if (parens.Line > 0) RaiseRecoverable(parens, "Parenthesized pattern");
        }

        static bool CheckExpressionErrors(DestructuringErrors? refDestructuringErrors, bool andThrow = false)
        {
            var pos = refDestructuringErrors?.ShorthandAssign ?? default;
            if (!andThrow) return pos.Line > 0;
            if (pos.Line > 0) Raise(pos, "Shorthand property assignments are valid only in destructuring patterns");
            return false;
        }

        void CheckYieldAwaitInDefaultParams()
        {
            if (_yieldPos.Line > 0 && (_awaitPos.Line == 0 || _yieldPos.Index < _awaitPos.Index))
                Raise(_yieldPos, "Yield expression cannot be a default value");
            if (_awaitPos.Line > 0)
                Raise(_awaitPos, "Await expression cannot be a default value");
        }

        static bool IsSimpleAssignTarget(AstNode expr)
        {
            return expr is AstSymbol || expr is AstPropAccess;
        }
    }
}
