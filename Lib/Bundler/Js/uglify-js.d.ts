interface IDictionary<T> {
    has(key: string): boolean;
    set(key: string, val: T): IDictionary<T>;
    add(key: string, val: T): IDictionary<T>;
    get(key: string): T;
    del(key: string): IDictionary<T>;
    each(f: (val: T, key: string) => void): void;
    size(): number;
    map<U>(f: (val: T, key: string) => U): U[];
}

interface IOutputStream {
    toString(): string;
}

interface IAstToken {
    type: string;
    file: string;
    value: string | number | RegExp;
    line: number;
    col: number;
    pos: number;
    endpos: number;
    nlb: boolean;
    comments_before: IAstToken[];
}

interface ISymbolDef {
    name: string;
    orig: IAstSymbolDeclaration[];
    scope: IAstScope;
    references: IAstSymbolRef[];
    global: boolean;
    undeclared: boolean;
    constant: boolean;
    mangled_name?: string;
}

interface ISymbolDefNew {
    new(scope: IAstScope, index: number, orig: IAstSymbolDeclaration): ISymbolDef;
}
declare const SymbolDef: ISymbolDefNew;

/// Base class of all AST nodes
interface IAstNode {
    /// The first token of this node
    start?: IAstToken;
    /// The last token of this node
    end?: IAstToken;

    clone?(): IAstNode;
    walk?(walker: IWalker): void;
    transform?(transformer: ITransformer): IAstNode;
    TYPE?: string;
    CTOR?: { PROPS: string[] };
    print_to_string?(options?: IOutputStreamOptions): string;
}

interface IAST_Node {
    new(props?: IAstNode): IAstNode;
}
/// Base class of all AST nodes
declare const AST_Node: IAST_Node;

/// Base class of all statements
interface IAstStatement extends IAstNode {
}

interface IAST_Statement {
    new(props?: IAstStatement): IAstStatement;
}
/// Base class of all statements
declare const AST_Statement: IAST_Statement;

/// Represents a debugger statement
interface IAstDebugger extends IAstStatement {
}

interface IAST_Debugger {
    new(props?: IAstDebugger): IAstDebugger;
}
/// Represents a debugger statement
declare const AST_Debugger: IAST_Debugger;

/// Represents a directive, like "use strict";
interface IAstDirective extends IAstStatement {
    /// The value of this directive as a plain string (it's not an AST_String!)
    value?: string;
    /// The scope that this directive affects (After Scope)
    scope?: IAstScope;
    /// the original quote character
    quote?: string;
}

interface IAST_Directive {
    new(props?: IAstDirective): IAstDirective;
}
/// Represents a directive, like "use strict";
declare const AST_Directive: IAST_Directive;

/// A statement consisting of an expression, i.e. a = 1 + 2
interface IAstSimpleStatement extends IAstStatement {
    /// an expression node (should not be instanceof AST_Statement)
    body?: IAstNode;
}

interface IAST_SimpleStatement {
    new(props?: IAstSimpleStatement): IAstSimpleStatement;
}
/// A statement consisting of an expression, i.e. a = 1 + 2
declare const AST_SimpleStatement: IAST_SimpleStatement;

/// A body of statements (usually bracketed)
interface IAstBlock extends IAstStatement {
    /// an array of statements
    body?: IAstStatement[];
}

interface IAST_Block {
    new(props?: IAstBlock): IAstBlock;
}
/// A body of statements (usually bracketed)
declare const AST_Block: IAST_Block;

/// A block statement
interface IAstBlockStatement extends IAstBlock {
}

interface IAST_BlockStatement {
    new(props?: IAstBlockStatement): IAstBlockStatement;
}
/// A block statement
declare const AST_BlockStatement: IAST_BlockStatement;

/// Base class for all statements introducing a lexical scope
interface IAstScope extends IAstBlock {
    /// an array of directives declared in this scope (After Scope)
    directives?: string[];
    /// a map of name -> SymbolDef for all variables/functions defined in this scope (After Scope)
    variables?: IDictionary<ISymbolDef>;
    /// like `variables`, but only lists function declarations (After Scope)
    functions?: IDictionary<ISymbolDef>;
    /// tells whether this scope uses the `with` statement (After Scope)
    uses_with?: boolean;
    /// tells whether this scope contains a direct call to the global `eval` (After Scope)
    uses_eval?: boolean;
    /// link to the parent scope (After Scope)
    parent_scope?: IAstScope;
    /// a list of all symbol definitions that are accessed from this scope or any subscopes (After Scope)
    enclosed?: ISymbolDef[];
    /// current index for mangling variables (used internally by the mangler) (After Scope)
    cname?: number;

    def_variable(symb: IAstSymbolRef): ISymbolDef;
}

interface IAST_Scope {
    new(props?: IAstScope): IAstScope;
}
/// Base class for all statements introducing a lexical scope
declare const AST_Scope: IAST_Scope;

/// The toplevel scope
interface IAstToplevel extends IAstScope {
    /// a map of name -> SymbolDef for all undeclared names (After Scope)
    globals?: IDictionary<ISymbolDef>;

    figure_out_scope?(): void;
    compute_char_frequency?(): void;
    mangle_names?(): void;
    print?(os: IOutputStream): void;
}

interface IAST_Toplevel {
    new(props?: IAstToplevel): IAstToplevel;
}
/// The toplevel scope
declare const AST_Toplevel: IAST_Toplevel;

/// Base class for functions
interface IAstLambda extends IAstScope {
    /// the name of this function
    name?: IAstSymbolDeclaration;
    /// array of function arguments
    argnames?: IAstSymbolFunarg[];
    /// tells whether this function accesses the arguments array (After Scope)
    uses_arguments?: boolean;
}

interface IAST_Lambda {
    new(props?: IAstLambda): IAstLambda;
}
/// Base class for functions
declare const AST_Lambda: IAST_Lambda;

/// A setter/getter function.  The `name` property is always null.
interface IAstAccessor extends IAstLambda {
}

interface IAST_Accessor {
    new(props?: IAstAccessor): IAstAccessor;
}
/// A setter/getter function.  The `name` property is always null.
declare const AST_Accessor: IAST_Accessor;

/// A function expression
interface IAstFunction extends IAstLambda {
}

interface IAST_Function {
    new(props?: IAstFunction): IAstFunction;
}
/// A function expression
declare const AST_Function: IAST_Function;

/// A function definition
interface IAstDefun extends IAstLambda {
}

interface IAST_Defun {
    new(props?: IAstDefun): IAstDefun;
}
/// A function definition
declare const AST_Defun: IAST_Defun;

/// A `switch` statement
interface IAstSwitch extends IAstBlock {
    /// the `switch` �discriminant�
    expression?: IAstNode;
}

interface IAST_Switch {
    new(props?: IAstSwitch): IAstSwitch;
}
/// A `switch` statement
declare const AST_Switch: IAST_Switch;

/// Base class for `switch` branches
interface IAstSwitchBranch extends IAstBlock {
}

interface IAST_SwitchBranch {
    new(props?: IAstSwitchBranch): IAstSwitchBranch;
}
/// Base class for `switch` branches
declare const AST_SwitchBranch: IAST_SwitchBranch;

/// A `default` switch branch
interface IAstDefault extends IAstSwitchBranch {
}

interface IAST_Default {
    new(props?: IAstDefault): IAstDefault;
}
/// A `default` switch branch
declare const AST_Default: IAST_Default;

/// A `case` switch branch
interface IAstCase extends IAstSwitchBranch {
    /// the `case` expression
    expression?: IAstNode;
}

interface IAST_Case {
    new(props?: IAstCase): IAstCase;
}
/// A `case` switch branch
declare const AST_Case: IAST_Case;

/// A `try` statement
interface IAstTry extends IAstBlock {
    /// the catch block, or null if not present
    bcatch?: IAstCatch;
    /// the finally block, or null if not present
    bfinally?: IAstFinally;
}

interface IAST_Try {
    new(props?: IAstTry): IAstTry;
}
/// A `try` statement
declare const AST_Try: IAST_Try;

/// A `catch` node; only makes sense as part of a `try` statement
interface IAstCatch extends IAstBlock {
    /// symbol for the exception
    argname?: IAstSymbolCatch;
}

interface IAST_Catch {
    new(props?: IAstCatch): IAstCatch;
}
/// A `catch` node; only makes sense as part of a `try` statement
declare const AST_Catch: IAST_Catch;

/// A `finally` node; only makes sense as part of a `try` statement
interface IAstFinally extends IAstBlock {
}

interface IAST_Finally {
    new(props?: IAstFinally): IAstFinally;
}
/// A `finally` node; only makes sense as part of a `try` statement
declare const AST_Finally: IAST_Finally;

/// The empty statement (empty block or simply a semicolon)
interface IAstEmptyStatement extends IAstStatement {
}

interface IAST_EmptyStatement {
    new(props?: IAstEmptyStatement): IAstEmptyStatement;
}
/// The empty statement (empty block or simply a semicolon)
declare const AST_EmptyStatement: IAST_EmptyStatement;

/// Base class for all statements that contain one nested body: `For`, `ForIn`, `Do`, `While`, `With`
interface IAstStatementWithBody extends IAstStatement {
    /// the body; this should always be present, even if it's an AST_EmptyStatement
    body?: IAstStatement;
}

interface IAST_StatementWithBody {
    new(props?: IAstStatementWithBody): IAstStatementWithBody;
}
/// Base class for all statements that contain one nested body: `For`, `ForIn`, `Do`, `While`, `With`
declare const AST_StatementWithBody: IAST_StatementWithBody;

/// Statement with a label
interface IAstLabeledStatement extends IAstStatementWithBody {
    /// a label definition
    label?: IAstLabel;
}

interface IAST_LabeledStatement {
    new(props?: IAstLabeledStatement): IAstLabeledStatement;
}
/// Statement with a label
declare const AST_LabeledStatement: IAST_LabeledStatement;

/// Internal class.  All loops inherit from it.
interface IAstIterationStatement extends IAstStatementWithBody {
}

interface IAST_IterationStatement {
    new(props?: IAstIterationStatement): IAstIterationStatement;
}
/// Internal class.  All loops inherit from it.
declare const AST_IterationStatement: IAST_IterationStatement;

/// Base class for do/while statements
interface IAstDWLoop extends IAstIterationStatement {
    /// the loop condition.  Should not be instanceof AST_Statement
    condition?: IAstNode;
}

interface IAST_DWLoop {
    new(props?: IAstDWLoop): IAstDWLoop;
}
/// Base class for do/while statements
declare const AST_DWLoop: IAST_DWLoop;

/// A `do` statement
interface IAstDo extends IAstDWLoop {
}

interface IAST_Do {
    new(props?: IAstDo): IAstDo;
}
/// A `do` statement
declare const AST_Do: IAST_Do;

/// A `while` statement
interface IAstWhile extends IAstDWLoop {
}

interface IAST_While {
    new(props?: IAstWhile): IAstWhile;
}
/// A `while` statement
declare const AST_While: IAST_While;

/// A `for` statement
interface IAstFor extends IAstIterationStatement {
    /// the `for` initialization code, or null if empty
    init?: IAstNode;
    /// the `for` termination clause, or null if empty
    condition?: IAstNode;
    /// the `for` update clause, or null if empty
    step?: IAstNode;
}

interface IAST_For {
    new(props?: IAstFor): IAstFor;
}
/// A `for` statement
declare const AST_For: IAST_For;

/// A `for ... in` statement
interface IAstForIn extends IAstIterationStatement {
    /// the `for/in` initialization code
    init?: IAstNode;
    /// the loop variable, only if `init` is AST_Var
    name?: IAstSymbolRef;
    /// the object that we're looping through
    object?: IAstNode;
}

interface IAST_ForIn {
    new(props?: IAstForIn): IAstForIn;
}
/// A `for ... in` statement
declare const AST_ForIn: IAST_ForIn;

/// A `with` statement
interface IAstWith extends IAstStatementWithBody {
    /// the `with` expression
    expression?: IAstNode;
}

interface IAST_With {
    new(props?: IAstWith): IAstWith;
}
/// A `with` statement
declare const AST_With: IAST_With;

/// A `if` statement
interface IAstIf extends IAstStatementWithBody {
    /// the `if` condition
    condition?: IAstNode;
    /// the `else` part, or null if not present
    alternative?: IAstStatement;
}

interface IAST_If {
    new(props?: IAstIf): IAstIf;
}
/// A `if` statement
declare const AST_If: IAST_If;

/// Base class for �jumps� (for now that's `return`, `throw`, `break` and `continue`)
interface IAstJump extends IAstStatement {
}

interface IAST_Jump {
    new(props?: IAstJump): IAstJump;
}
/// Base class for �jumps� (for now that's `return`, `throw`, `break` and `continue`)
declare const AST_Jump: IAST_Jump;

/// Base class for �exits� (`return` and `throw`)
interface IAstExit extends IAstJump {
    /// the value returned or thrown by this statement; could be null for AST_Return
    value?: IAstNode;
}

interface IAST_Exit {
    new(props?: IAstExit): IAstExit;
}
/// Base class for �exits� (`return` and `throw`)
declare const AST_Exit: IAST_Exit;

/// A `return` statement
interface IAstReturn extends IAstExit {
}

interface IAST_Return {
    new(props?: IAstReturn): IAstReturn;
}
/// A `return` statement
declare const AST_Return: IAST_Return;

/// A `throw` statement
interface IAstThrow extends IAstExit {
}

interface IAST_Throw {
    new(props?: IAstThrow): IAstThrow;
}
/// A `throw` statement
declare const AST_Throw: IAST_Throw;

/// Base class for loop control statements (`break` and `continue`)
interface IAstLoopControl extends IAstJump {
    /// the label, or null if none
    label?: IAstLabelRef;
}

interface IAST_LoopControl {
    new(props?: IAstLoopControl): IAstLoopControl;
}
/// Base class for loop control statements (`break` and `continue`)
declare const AST_LoopControl: IAST_LoopControl;

/// A `break` statement
interface IAstBreak extends IAstLoopControl {
}

interface IAST_Break {
    new(props?: IAstBreak): IAstBreak;
}
/// A `break` statement
declare const AST_Break: IAST_Break;

/// A `continue` statement
interface IAstContinue extends IAstLoopControl {
}

interface IAST_Continue {
    new(props?: IAstContinue): IAstContinue;
}
/// A `continue` statement
declare const AST_Continue: IAST_Continue;

/// Base class for `var` or `const` nodes (variable declarations/initializations)
interface IAstDefinitions extends IAstStatement {
    /// array of variable definitions
    definitions?: IAstVarDef[];
}

interface IAST_Definitions {
    new(props?: IAstDefinitions): IAstDefinitions;
}
/// Base class for `var` or `const` nodes (variable declarations/initializations)
declare const AST_Definitions: IAST_Definitions;

/// A `var` statement
interface IAstVar extends IAstDefinitions {
}

interface IAST_Var {
    new(props?: IAstVar): IAstVar;
}
/// A `var` statement
declare const AST_Var: IAST_Var;

/// A `const` statement
interface IAstConst extends IAstDefinitions {
}

interface IAST_Const {
    new(props?: IAstConst): IAstConst;
}
/// A `const` statement
declare const AST_Const: IAST_Const;

/// A variable declaration; only appears in a AST_Definitions node
interface IAstVarDef extends IAstNode {
    /// name of the variable
    name?: IAstSymbolVar | IAstSymbolConst;
    /// initializer, or null of there's no initializer
    value?: IAstNode;
}

interface IAST_VarDef {
    new(props?: IAstVarDef): IAstVarDef;
}
/// A variable declaration; only appears in a AST_Definitions node
declare const AST_VarDef: IAST_VarDef;

/// A function call expression
interface IAstCall extends IAstNode {
    /// expression to invoke as function
    expression?: IAstNode;
    /// array of arguments
    args?: IAstNode[];
}

interface IAST_Call {
    new(props?: IAstCall): IAstCall;
}
/// A function call expression
declare const AST_Call: IAST_Call;

/// An object instantiation.  Derives from a function call since it has exactly the same properties
interface IAstNew extends IAstCall {
}

interface IAST_New {
    new(props?: IAstNew): IAstNew;
}
/// An object instantiation.  Derives from a function call since it has exactly the same properties
declare const AST_New: IAST_New;

/// A sequence expression (two comma-separated expressions)
interface IAstSeq extends IAstNode {
    /// first element in sequence
    car?: IAstNode;
    /// second element in sequence
    cdr?: IAstNode;
}

interface IAST_Seq {
    new(props?: IAstSeq): IAstSeq;
}
/// A sequence expression (two comma-separated expressions)
declare const AST_Seq: IAST_Seq;

/// Base class for property access expressions, i.e. `a.foo` or `a["foo"]`
interface IAstPropAccess extends IAstNode {
    /// the �container� expression
    expression?: IAstNode;
    /// the property to access.  For AST_Dot this is always a plain string, while for AST_Sub it's an arbitrary AST_Node
    property?: IAstNode | string;
}

interface IAST_PropAccess {
    new(props?: IAstPropAccess): IAstPropAccess;
}
/// Base class for property access expressions, i.e. `a.foo` or `a["foo"]`
declare const AST_PropAccess: IAST_PropAccess;

/// A dotted property access expression
interface IAstDot extends IAstPropAccess {
}

interface IAST_Dot {
    new(props?: IAstDot): IAstDot;
}
/// A dotted property access expression
declare const AST_Dot: IAST_Dot;

/// Index-style property access, i.e. `a["foo"]`
interface IAstSub extends IAstPropAccess {
}

interface IAST_Sub {
    new(props?: IAstSub): IAstSub;
}
/// Index-style property access, i.e. `a["foo"]`
declare const AST_Sub: IAST_Sub;

/// Base class for unary expressions
interface IAstUnary extends IAstNode {
    /// the operator
    operator?: string;
    /// expression that this unary operator applies to
    expression?: IAstNode;
}

interface IAST_Unary {
    new(props?: IAstUnary): IAstUnary;
}
/// Base class for unary expressions
declare const AST_Unary: IAST_Unary;

/// Unary prefix expression, i.e. `typeof i` or `++i`
interface IAstUnaryPrefix extends IAstUnary {
}

interface IAST_UnaryPrefix {
    new(props?: IAstUnaryPrefix): IAstUnaryPrefix;
}
/// Unary prefix expression, i.e. `typeof i` or `++i`
declare const AST_UnaryPrefix: IAST_UnaryPrefix;

/// Unary postfix expression, i.e. `i++`
interface IAstUnaryPostfix extends IAstUnary {
}

interface IAST_UnaryPostfix {
    new(props?: IAstUnaryPostfix): IAstUnaryPostfix;
}
/// Unary postfix expression, i.e. `i++`
declare const AST_UnaryPostfix: IAST_UnaryPostfix;

/// Binary expression, i.e. `a + b`
interface IAstBinary extends IAstNode {
    /// left-hand side expression
    left?: IAstNode;
    /// the operator
    operator?: string;
    /// right-hand side expression
    right?: IAstNode;
}

interface IAST_Binary {
    new(props?: IAstBinary): IAstBinary;
}
/// Binary expression, i.e. `a + b`
declare const AST_Binary: IAST_Binary;

/// An assignment expression � `a = b + 5`
interface IAstAssign extends IAstBinary {
}

interface IAST_Assign {
    new(props?: IAstAssign): IAstAssign;
}
/// An assignment expression � `a = b + 5`
declare const AST_Assign: IAST_Assign;

/// Conditional expression using the ternary operator, i.e. `a ? b : c`
interface IAstConditional extends IAstNode {
    /// 
    condition?: IAstNode;
    /// 
    consequent?: IAstNode;
    /// 
    alternative?: IAstNode;
}

interface IAST_Conditional {
    new(props?: IAstConditional): IAstConditional;
}
/// Conditional expression using the ternary operator, i.e. `a ? b : c`
declare const AST_Conditional: IAST_Conditional;

/// An array literal
interface IAstArray extends IAstNode {
    /// array of elements
    elements?: IAstNode[];
}

interface IAST_Array {
    new(props?: IAstArray): IAstArray;
}
/// An array literal
declare const AST_Array: IAST_Array;

/// An object literal
interface IAstObject extends IAstNode {
    /// array of properties
    properties?: IAstObjectProperty[];
}

interface IAST_Object {
    new(props?: IAstObject): IAstObject;
}
/// An object literal
declare const AST_Object: IAST_Object;

/// Base class for literal object properties
interface IAstObjectProperty extends IAstNode {
    /// the property name converted to a string for ObjectKeyVal.  For setters and getters this is an arbitrary AST_Node.
    key?: string;
    /// property value.  For setters and getters this is an AST_Function.
    value?: IAstNode;
}

interface IAST_ObjectProperty {
    new(props?: IAstObjectProperty): IAstObjectProperty;
}
/// Base class for literal object properties
declare const AST_ObjectProperty: IAST_ObjectProperty;

/// A key: value object property
interface IAstObjectKeyVal extends IAstObjectProperty {
    /// the original quote character
    quote?: string;
}

interface IAST_ObjectKeyVal {
    new(props?: IAstObjectKeyVal): IAstObjectKeyVal;
}
/// A key: value object property
declare const AST_ObjectKeyVal: IAST_ObjectKeyVal;

/// An object setter property
interface IAstObjectSetter extends IAstObjectProperty {
}

interface IAST_ObjectSetter {
    new(props?: IAstObjectSetter): IAstObjectSetter;
}
/// An object setter property
declare const AST_ObjectSetter: IAST_ObjectSetter;

/// An object getter property
interface IAstObjectGetter extends IAstObjectProperty {
}

interface IAST_ObjectGetter {
    new(props?: IAstObjectGetter): IAstObjectGetter;
}
/// An object getter property
declare const AST_ObjectGetter: IAST_ObjectGetter;

/// Base class for all symbols
interface IAstSymbol extends IAstNode {
    /// the current scope (not necessarily the definition scope) (After Scope)
    scope?: IAstScope;
    /// name of this symbol
    name?: string;
    /// the definition of this symbol (After Scope)
    thedef?: ISymbolDef;
}

interface IAST_Symbol {
    new(props?: IAstSymbol): IAstSymbol;
}
/// Base class for all symbols
declare const AST_Symbol: IAST_Symbol;

/// The name of a property accessor (setter/getter function)
interface IAstSymbolAccessor extends IAstSymbol {
}

interface IAST_SymbolAccessor {
    new(props?: IAstSymbolAccessor): IAstSymbolAccessor;
}
/// The name of a property accessor (setter/getter function)
declare const AST_SymbolAccessor: IAST_SymbolAccessor;

/// A declaration symbol (symbol in var/const, function name or argument, symbol in catch)
interface IAstSymbolDeclaration extends IAstSymbol {
    /// array of initializers for this declaration. (After Scope)
    init?: IAstNode[];
}

interface IAST_SymbolDeclaration {
    new(props?: IAstSymbolDeclaration): IAstSymbolDeclaration;
}
/// A declaration symbol (symbol in var/const, function name or argument, symbol in catch)
declare const AST_SymbolDeclaration: IAST_SymbolDeclaration;

/// Symbol defining a variable
interface IAstSymbolVar extends IAstSymbolDeclaration {
}

interface IAST_SymbolVar {
    new(props?: IAstSymbolVar): IAstSymbolVar;
}
/// Symbol defining a variable
declare const AST_SymbolVar: IAST_SymbolVar;

/// Symbol naming a function argument
interface IAstSymbolFunarg extends IAstSymbolVar {
}

interface IAST_SymbolFunarg {
    new(props?: IAstSymbolFunarg): IAstSymbolFunarg;
}
/// Symbol naming a function argument
declare const AST_SymbolFunarg: IAST_SymbolFunarg;

/// A constant declaration
interface IAstSymbolConst extends IAstSymbolDeclaration {
}

interface IAST_SymbolConst {
    new(props?: IAstSymbolConst): IAstSymbolConst;
}
/// A constant declaration
declare const AST_SymbolConst: IAST_SymbolConst;

/// Symbol defining a function
interface IAstSymbolDefun extends IAstSymbolDeclaration {
}

interface IAST_SymbolDefun {
    new(props?: IAstSymbolDefun): IAstSymbolDefun;
}
/// Symbol defining a function
declare const AST_SymbolDefun: IAST_SymbolDefun;

/// Symbol naming a function expression
interface IAstSymbolLambda extends IAstSymbolDeclaration {
}

interface IAST_SymbolLambda {
    new(props?: IAstSymbolLambda): IAstSymbolLambda;
}
/// Symbol naming a function expression
declare const AST_SymbolLambda: IAST_SymbolLambda;

/// Symbol naming the exception in catch
interface IAstSymbolCatch extends IAstSymbolDeclaration {
}

interface IAST_SymbolCatch {
    new(props?: IAstSymbolCatch): IAstSymbolCatch;
}
/// Symbol naming the exception in catch
declare const AST_SymbolCatch: IAST_SymbolCatch;

/// Symbol naming a label (declaration)
interface IAstLabel extends IAstSymbol {
    /// a list of nodes referring to this label
    references?: IAstLoopControl[];
}

interface IAST_Label {
    new(props?: IAstLabel): IAstLabel;
}
/// Symbol naming a label (declaration)
declare const AST_Label: IAST_Label;

/// Reference to some symbol (not definition/declaration)
interface IAstSymbolRef extends IAstSymbol {
}

interface IAST_SymbolRef {
    new(props?: IAstSymbolRef): IAstSymbolRef;
}
/// Reference to some symbol (not definition/declaration)
declare const AST_SymbolRef: IAST_SymbolRef;

/// Reference to a label symbol
interface IAstLabelRef extends IAstSymbol {
}

interface IAST_LabelRef {
    new(props?: IAstLabelRef): IAstLabelRef;
}
/// Reference to a label symbol
declare const AST_LabelRef: IAST_LabelRef;

/// The `this` symbol
interface IAstThis extends IAstSymbol {
}

interface IAST_This {
    new(props?: IAstThis): IAstThis;
}
/// The `this` symbol
declare const AST_This: IAST_This;

/// Base class for all constants
interface IAstConstant extends IAstNode {
}

interface IAST_Constant {
    new(props?: IAstConstant): IAstConstant;
}
/// Base class for all constants
declare const AST_Constant: IAST_Constant;

/// A string literal
interface IAstString extends IAstConstant {
    /// the contents of this string
    value?: string;
    /// the original quote character
    quote?: string;
}

interface IAST_String {
    new(props?: IAstString): IAstString;
}
/// A string literal
declare const AST_String: IAST_String;

/// A number literal
interface IAstNumber extends IAstConstant {
    /// the numeric value
    value?: number;
}

interface IAST_Number {
    new(props?: IAstNumber): IAstNumber;
}
/// A number literal
declare const AST_Number: IAST_Number;

/// A regexp literal
interface IAstRegExp extends IAstConstant {
    /// the actual regexp
    value?: RegExp;
}

interface IAST_RegExp {
    new(props?: IAstRegExp): IAstRegExp;
}
/// A regexp literal
declare const AST_RegExp: IAST_RegExp;

/// Base class for atoms
interface IAstAtom extends IAstConstant {
}

interface IAST_Atom {
    new(props?: IAstAtom): IAstAtom;
}
/// Base class for atoms
declare const AST_Atom: IAST_Atom;

/// The `null` atom
interface IAstNull extends IAstAtom {
}

interface IAST_Null {
    new(props?: IAstNull): IAstNull;
}
/// The `null` atom
declare const AST_Null: IAST_Null;

/// The impossible value
interface IAstNaN extends IAstAtom {
}

interface IAST_NaN {
    new(props?: IAstNaN): IAstNaN;
}
/// The impossible value
declare const AST_NaN: IAST_NaN;

/// The `undefined` value
interface IAstUndefined extends IAstAtom {
}

interface IAST_Undefined {
    new(props?: IAstUndefined): IAstUndefined;
}
/// The `undefined` value
declare const AST_Undefined: IAST_Undefined;

/// A hole in an array
interface IAstHole extends IAstAtom {
}

interface IAST_Hole {
    new(props?: IAstHole): IAstHole;
}
/// A hole in an array
declare const AST_Hole: IAST_Hole;

/// The `Infinity` value
interface IAstInfinity extends IAstAtom {
}

interface IAST_Infinity {
    new(props?: IAstInfinity): IAstInfinity;
}
/// The `Infinity` value
declare const AST_Infinity: IAST_Infinity;

/// Base class for booleans
interface IAstBoolean extends IAstAtom {
}

interface IAST_Boolean {
    new(props?: IAstBoolean): IAstBoolean;
}
/// Base class for booleans
declare const AST_Boolean: IAST_Boolean;

/// The `false` atom
interface IAstFalse extends IAstBoolean {
}

interface IAST_False {
    new(props?: IAstFalse): IAstFalse;
}
/// The `false` atom
declare const AST_False: IAST_False;

/// The `true` atom
interface IAstTrue extends IAstBoolean {
}

interface IAST_True {
    new(props?: IAstTrue): IAstTrue;
}
/// The `true` atom
declare const AST_True: IAST_True;


interface IOutputStreamOptions {
    beautify?: boolean;
}

interface ICompressorOptions {
    sequences?: boolean;
    properties?: boolean;
    dead_code?: boolean;
    drop_debugger?: boolean;
    unsafe?: boolean;
    unsafe_comps?: boolean;
    conditionals?: boolean;
    comparisons?: boolean;
    evaluate?: boolean;
    booleans?: boolean;
    loops?: boolean;
    unused?: boolean;
    hoist_funs?: boolean;
    keep_fargs?: boolean;
    keep_fnames?: boolean;
    hoist_vars?: boolean;
    if_return?: boolean;
    join_vars?: boolean;
    collapse_vars?: boolean;
    cascade?: boolean;
    side_effects?: boolean;
    pure_getters?: boolean | "strict";
    negate_iife?: boolean;
    screw_ie8?: boolean;
    drop_console?: boolean;
    warnings?: boolean;
    global_defs?: { [name: string]: any };
    pure_funcs: string[] | ((call: IAstCall) => boolean);
    passes?: number;
}

interface IParseOptions {
    filename?: string;
    strict?: boolean;
    toplevel?: IAstToplevel;
}

interface IWalker {
    parent(n?: number): IAstNode;
    stack: IAstNode[];
    find_parent(type: any): IAstNode;
    in_boolean_context(): boolean;
}

interface ITransformer extends IWalker {
}

declare function parse(code: string, options?: IParseOptions): IAstToplevel;
declare function OutputStream(options: IOutputStreamOptions): IOutputStream;

interface ITreeWalker {
    new(visitor: (node: IAstNode, descend: () => void) => boolean): IWalker;
}

declare const TreeWalker: ITreeWalker;

interface ITreeTransformer {
    new(before: (node: IAstNode, descend: (node: IAstNode, walker: IWalker) => void) => IAstNode | undefined, after: (node: IAstNode) => IAstNode | undefined): ITransformer;
}

declare const TreeTransformer: ITreeTransformer;

declare function Compressor(options: ICompressorOptions): ITransformer;

declare const base54: { reset(): void; };
