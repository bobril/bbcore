using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Njsast.Ast;

namespace Njsast.Reader
{
    public sealed partial class Parser
    {
        // Convert existing expression atom to assignable pattern
        // if possible.
        [return: NotNullIfNotNull("node")]
        AstNode? ToAssignable(AstNode? node, bool isBinding = false)
        {
            if (Options.EcmaVersion >= 6 && node != null)
            {
                switch (node)
                {
                    case AstSymbol identifierNode:
                        if (_inAsync && identifierNode.Name == "await")
                            Raise(node.Start, "Can not use 'await' as identifier inside an async function");
                        break;

                    case AstPropAccess _:
                        if (!isBinding) break;
                        goto default;

                    case AstDestructuring _:
                        break;

                    case AstObject objectExpression:
                        var newProperties = new StructList<AstNode>();
                        newProperties.Reserve(objectExpression.Properties.Count);
                        for (var i = 0; i < newProperties.Count; i++)
                            newProperties[(uint) i] = ToAssignable(objectExpression.Properties[(uint) i], isBinding)!;
                        node = new AstDestructuring(SourceFile, node.Start, node.End, ref newProperties, false);
                        break;

                    case AstArray arrayExpression:
                        ToAssignableList(ref arrayExpression.Elements, isBinding);
                        node = new AstDestructuring(SourceFile, node.Start, node.End, ref arrayExpression.Elements, true);
                        break;

                    case AstExpansion spreadElement:
                        spreadElement.Expression = ToAssignable(spreadElement.Expression, isBinding)!;
                        if (spreadElement.Expression is AstDefaultAssign)
                        {
                            Raise(spreadElement.Expression.Start, "Rest elements cannot have a default value");
                        }

                        break;

                    case AstAssign assignmentExpression:
                        if (assignmentExpression.Operator != Operator.Assignment)
                        {
                            Raise(assignmentExpression.Left.End,
                                "Only '=' operator can be used for specifying default value.");
                        }

                        var left = ToAssignable(assignmentExpression.Left, isBinding)!;
                        var right = assignmentExpression.Right;
                        node = new AstDefaultAssign(SourceFile, node.Start, node.End, left, right);
                        goto AssignmentPatternNode;

                    case AstDefaultAssign _:
                        AssignmentPatternNode:
                        break;

                    default:
                        Raise(node.Start, "Assigning to rvalue");
                        break;
                }
            }

            return node;
        }

        [return: NotNullIfNotNull("property")]
        AstObjectProperty? ToAssignable(AstObjectProperty? property, bool isBinding = false)
        {
            if (property == null)
                return null;

            if (!(property is AstObjectKeyVal))
                Raise(property.Start, "Object pattern can't contain getter or setter");

            property.Value = ToAssignable(property.Value, isBinding)!;
            return property;
        }

        // Convert list of expression atoms to binding list.
        void ToAssignableList(ref StructList<AstNode> expressionList, bool isBinding)
        {
            for (var i = 0; i < expressionList.Count; i++)
            {
                var element = expressionList[(uint) i];
                if (element != null) expressionList[(uint) i] = ToAssignable(element, isBinding)!;
            }

            if (expressionList.Count != 0)
            {
                var last = expressionList.Last;
                if (Options.EcmaVersion == 6 && isBinding && last is AstExpansion restElementNode &&
                    !(restElementNode.Expression is AstSymbol))
                {
                    Raise(restElementNode.Expression.Start, "Unexpected token");
                }
            }
        }

        // Parses spread element.
        AstExpansion ParseSpread(DestructuringErrors? refDestructuringErrors)
        {
            var startLoc = Start;
            Next();
            var argument = ParseMaybeAssign(Start, false, refDestructuringErrors);
            return new AstExpansion(SourceFile, startLoc, _lastTokEnd, argument);
        }

        AstNode ParseRestBinding()
        {
            var startLoc = Start;
            Next();

            // RestElement inside of a function parameter must be an identifier
            if (Options.EcmaVersion == 6 && Type != TokenType.Name)
            {
                Raise(Start, "Unexpected token");
            }

            var argument = ParseBindingAtom();
            return new AstExpansion(SourceFile, startLoc, _lastTokEnd, argument);
        }

        // Parses lvalue (assignable) atom.
        AstNode ParseBindingAtom()
        {
            if (Options.EcmaVersion >= 6)
            {
                switch (Type)
                {
                    case TokenType.BracketL:
                        var startLoc = Start;
                        Next();

                        var elements = new StructList<AstNode>();
                        ParseBindingList(ref elements, TokenType.BracketR, true, true);
                        return new AstDestructuring(SourceFile, startLoc, _lastTokEnd, ref elements, true);

                    case TokenType.BraceL:
                        return ParseObj(true);
                }
            }

            return ParseIdent();
        }

        void ParseBindingList(ref StructList<AstNode> elts, TokenType close, bool allowEmpty, bool allowTrailingComma)
        {
            var first = true;
            while (!Eat(close))
            {
                if (first) first = false;
                else Expect(TokenType.Comma);
                if (allowEmpty && Type == TokenType.Comma)
                {
                    elts.Add(new AstHole(SourceFile, _lastTokStart, _lastTokEnd));
                }
                else if (allowTrailingComma && AfterTrailingComma(close))
                {
                    break;
                }
                else if (Type == TokenType.Ellipsis)
                {
                    var rest = ParseRestBinding();
                    elts.Add(rest);
                    if (Type == TokenType.Comma) Raise(Start, "Comma is not permitted after the rest element");
                    Expect(close);
                    break;
                }
                else
                {
                    var elem = ParseMaybeDefault(Start);
                    elts.Add(elem);
                }
            }
        }

        // Parses assignment pattern around given atom if possible.
        AstNode ParseMaybeDefault(Position startLoc, AstNode? left = null)
        {
            left ??= ParseBindingAtom();
            if (Options.EcmaVersion < 6 || !Eat(TokenType.Eq))
                return left;
            var right = ParseMaybeAssign(Start);
            return new AstDefaultAssign(SourceFile, startLoc, _lastTokEnd, left, right);
        }

        // Verify that a node is an lval — something that can be assigned
        // to.
        // bindingType can be either:
        // var indicating that the lval creates a 'var' binding
        // let indicating that the lval creates a lexical ('let' or 'const') binding
        // null indicating that the binding should be checked for illegal identifiers, but not for duplicate references
        void CheckLVal(AstNode expr, bool isBinding, VariableKind? bindingType, ISet<string>? checkClashes = null)
        {
            switch (expr)
            {
                case AstSymbol identifierNode:
                    if (_strict && _reservedWordsStrictBind.IsMatch(identifierNode.Name))
                        RaiseRecoverable(expr.Start,
                            (isBinding ? "Binding " : "Assigning to ") + identifierNode.Name + " in strict mode");
                    if (checkClashes != null)
                    {
                        if (checkClashes.Contains(identifierNode.Name))
                            RaiseRecoverable(expr.Start, "Argument name clash");
                        checkClashes.Add(identifierNode.Name);
                    }

                    if (bindingType != null && isBinding)
                    {
                        if (bindingType == VariableKind.Var && !CanDeclareVarName(identifierNode.Name) ||
                            bindingType != VariableKind.Var && !CanDeclareLexicalName(identifierNode.Name))
                        {
                            RaiseRecoverable(expr.Start,
                                $"Identifier '{identifierNode.Name}' has already been declared");
                        }

                        if (bindingType == VariableKind.Var)
                        {
                            DeclareVarName(identifierNode.Name);
                        }
                        else
                        {
                            DeclareLexicalName(identifierNode.Name);
                        }
                    }

                    break;

                case AstPropAccess _:
                    if (bindingType != null) RaiseRecoverable(expr.Start, "Binding" + " member expression");
                    break;

                case AstDestructuring objectPattern:
                    foreach (var prop in objectPattern.Names)
                    {
                        CheckLVal(prop, isBinding, bindingType, checkClashes);
                    }

                    break;

                case AstObjectProperty property:
                    // AssignmentProperty has type == "Property"
                    CheckLVal(property.Value, isBinding, bindingType, checkClashes);
                    break;


                case AstDefaultAssign assignmentPattern:
                    CheckLVal(assignmentPattern.Left, isBinding, bindingType, checkClashes);
                    break;

                case AstExpansion restElement:
                    CheckLVal(restElement.Expression, isBinding, bindingType, checkClashes);
                    break;

                default:
                    Raise(expr.Start, (bindingType != null ? "Binding" : "Assigning to") + " rvalue");
                    break;
            }
        }
    }
}
