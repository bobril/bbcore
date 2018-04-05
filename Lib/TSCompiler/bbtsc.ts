/// <reference path="../node_modules/typescript/lib/typescriptServices.d.ts" />

// expose internal useful TS functions
declare namespace ts {
    function getNodeId(node: Node): number;
}

declare var bbCurrentDirectory: string;
declare var bbDefaultLibLocation: string;

declare const bb: IBB;

interface IBB {
    getChangeId(fileName: string): number | undefined;
    readFile(fileName: string, sourceCode: boolean): string;
    writeFile(fileName: string, data: string): boolean;
    dirExists(directoryPath: string): boolean;
    fileExists(fileName: string): boolean;
    getDirectories(directoryPath: string): string;
    realPath(path: string): string;
    trace(text: string): void;
    reportTypeScriptDiag(isError: boolean, code: number, text: string): void;
    reportTypeScriptDiagFile(
        isError: boolean,
        code: number,
        text: string,
        fileName: string,
        startLine: number,
        startCharacter: number,
        endLine: number,
        endCharacter: number
    ): void;
    // resolvedName:string|isExternalLibraryImport:boolean|extension:string(Ts,Tsx,Dts,Js,Jsx)
    resolveModuleName(name: string, containingFile: string): string;
    resolvePathStringLiteral(sourcePath: string, text: string): string;
    reportSourceInfo(fileName: string, info: string): void;
    getModifications(fileName: string): string;
}

let parseCache: { [fileName: string]: [number, ts.SourceFile] } = {};

function createCompilerHost(setParentNodes?: boolean): ts.CompilerHost {
    function getCanonicalFileName(fileName: string): string {
        return fileName;
    }

    function getSourceFile(
        fileName: string,
        languageVersion: ts.ScriptTarget,
        onError?: (message: string) => void
    ): ts.SourceFile {
        let version = bb.getChangeId(fileName);
        if (version == undefined) {
            if (onError) {
                onError("Read Error in " + fileName);
            }
            throw new Error("Cannot getSourceFile " + fileName);
        }
        let cache = parseCache[fileName];
        if (cache && version == cache[0]) return cache[1];
        let text = bb.readFile(fileName, true);
        if (text == undefined) {
            if (onError) {
                onError("Read Error in " + fileName);
            }
            throw new Error("Cannot getSourceFile " + fileName);
        }
        let res = ts.createSourceFile(fileName, text, languageVersion, setParentNodes);
        if (fileName.endsWith(".d.ts")) parseCache[fileName] = [version, res];
        return res;
    }

    function writeFile(
        fileName: string,
        data: string,
        _writeByteOrderMark: boolean,
        onError?: (message: string) => void
    ) {
        if (!bb.writeFile(fileName, data)) {
            if (onError) {
                onError("Write failed " + fileName);
            }
        }
    }

    return {
        getSourceFile,
        getDefaultLibLocation: () => bbDefaultLibLocation,
        getDefaultLibFileName: options => bbDefaultLibLocation + "/" + ts.getDefaultLibFileName(options),
        writeFile,
        getCurrentDirectory: () => bbCurrentDirectory,
        useCaseSensitiveFileNames: () => true,
        getCanonicalFileName,
        getNewLine: () => "\n",
        fileExists: name => bb.fileExists(name),
        readFile: fileName => bb.readFile(fileName, false),
        trace: text => bb.trace(text),
        directoryExists: dir => bb.dirExists(dir),
        getEnvironmentVariable: (name: string) => {
            bb.trace("Getting ENV " + name);
            return "";
        },
        getDirectories: (name: string) => bb.getDirectories(name).split("|"),
        realpath: path => bb.realPath(path),
        resolveModuleNames(moduleNames: string[], containingFile: string): ts.ResolvedModuleFull[] {
            return moduleNames.map(n => {
                let r = bb.resolveModuleName(n, containingFile).split("|");
                if (r.length < 3) return null as any;
                let res: ts.ResolvedModuleFull = {
                    resolvedFileName: r[0],
                    isExternalLibraryImport: r[1] == "true",
                    extension: (ts.Extension as any)[r[2]]
                };
                return res;
            });
        }
    };
}

let compilerOptions = ts.getDefaultCompilerOptions();

function bbInitDefaultCompilerOptions() {
    compilerOptions = ts.getDefaultCompilerOptions();
}

function bbSetCurrentCompilerOptions(json: string) {
    compilerOptions = JSON.parse(json);
}

function bbMergeCurrentCompilerOptions(json: string) {
    Object.assign(compilerOptions, JSON.parse(json));
}

function bbGetCurrentCompilerOptions(): string {
    return JSON.stringify(compilerOptions);
}

let program: ts.Program;
let typeChecker: ts.TypeChecker;

function addLibPrefixPostfix(names: string[]) {
    for (var i = 0; i < names.length; i++) {
        if (names[i].startsWith("lib.")) continue;
        names[i] = "lib." + names[i] + ".d.ts";
    }
}

function bbStartTSPerformance() {
    (ts as any).performance.enable();
}

function bbCreateProgram(rootNames: string) {
    if (compilerOptions.lib != null) addLibPrefixPostfix(compilerOptions.lib);
    //bb.trace(JSON.stringify(compilerOptions));
    program = ts.createProgram(rootNames.split("|"), compilerOptions, createCompilerHost());
    typeChecker = program.getTypeChecker();
}

let wasError = false;

function reportDiagnostic(diagnostic: ts.Diagnostic) {
    if (diagnostic.category === ts.DiagnosticCategory.Error) wasError = true;
    if (diagnostic.file) {
        var locStart = diagnostic.file.getLineAndCharacterOfPosition(diagnostic.start!);
        var locEnd = diagnostic.file.getLineAndCharacterOfPosition(diagnostic.start! + diagnostic.length!);
        bb.reportTypeScriptDiagFile(
            diagnostic.category === ts.DiagnosticCategory.Error,
            diagnostic.code,
            ts.flattenDiagnosticMessageText(diagnostic.messageText, "\n"),
            diagnostic.file.fileName,
            locStart.line,
            locStart.character,
            locEnd.line,
            locEnd.character
        );
    } else {
        bb.reportTypeScriptDiag(
            diagnostic.category === ts.DiagnosticCategory.Error,
            diagnostic.code,
            ts.flattenDiagnosticMessageText(diagnostic.messageText, "\n")
        );
    }
}

function reportDiagnostics(diagnostics: ReadonlyArray<ts.Diagnostic>) {
    for (var i = 0; i < diagnostics.length; i++) {
        reportDiagnostic(diagnostics[i]);
    }
}

function bbCompileProgram(): string {
    let diagnostics = program.getSyntacticDiagnostics();
    reportDiagnostics(diagnostics);
    if (diagnostics.length === 0) {
        let diagnostics = program.getGlobalDiagnostics();
        reportDiagnostics(diagnostics);
        if (diagnostics.length === 0) {
            let diagnostics = program.getSemanticDiagnostics();
            reportDiagnostics(diagnostics);
        }
    }
    return (<any>program).getCommonSourceDirectory();
}

const sourceInfos: { [name: string]: SourceInfo } = Object.create(null);

function bbGatherSourceInfo(): void {
    let sourceFiles = program.getSourceFiles();
    const resolvePathStringLiteral = (nn: ts.StringLiteral) =>
        bb.resolvePathStringLiteral(nn.getSourceFile().fileName, nn.text);
    for (let i = 0; i < sourceFiles.length; i++) {
        let sourceFile = sourceFiles[i];
        if (sourceFile.isDeclarationFile) continue;
        let sourceInfo = gatherSourceInfo(sourceFile, typeChecker, resolvePathStringLiteral);
        sourceInfos[sourceFile.fileName] = sourceInfo;
        bb.reportSourceInfo(sourceFile.fileName, JSON.stringify(toJsonableSourceInfo(sourceInfo)));
    }
}

function toJsonableSourceInfo(sourceInfo: SourceInfo) {
    return {
        assets: sourceInfo.assets.map(a => ({
            nodeId: ts.getNodeId(a.callExpression),
            name: a.name
        })),
        sprites: sourceInfo.sprites.map(s => ({
            nodeId: ts.getNodeId(s.callExpression),
            name: s.name,
            color: s.color,
            hasColor: s.hasColor,
            width: s.width,
            height: s.height,
            x: s.x,
            y: s.y
        })),
        translations: sourceInfo.trs.map(t => ({
            nodeId: ts.getNodeId(t.callExpression),
            message: typeof t.message === "string" ? t.message : undefined,
            hint: t.hint,
            justFormat: t.justFormat,
            withParams: t.withParams,
            knownParams: t.knownParams
        })),
        styleDefs: sourceInfo.styleDefs.map(s => ({
            nodeId: ts.getNodeId(s.callExpression),
            name: s.name,
            userNamed: s.userNamed,
            isEx: s.isEx
        }))
    };
}

function bbEmitProgram(): boolean {
    const res = program.emit(undefined, undefined, undefined, undefined, transformers);
    reportDiagnostics(res.diagnostics);
    return !res.emitSkipped;
}

function bbFinishTSPerformance(): string {
    var res: { [name: string]: number } = {};
    (ts as any).performance.forEachMeasure((k: string, d: number) => {
        res[k] = d;
    });
    (ts as any).performance.disable();
    return JSON.stringify(res);
}

const transformers: ts.CustomTransformers = {
    before: [
        context => (node: ts.SourceFile): ts.SourceFile => {
            let modifications: { [nodeId: number]: any[] } = JSON.parse(
                bb.getModifications(node.getSourceFile().fileName)
            );
            if (modifications == null) return node;
            function visitor(node: ts.Node): ts.Node {
                node = ts.visitEachChild(node, visitor, context);
                let id = (node as any).id;
                if (typeof id === "number") {
                    let modification = modifications[id];
                    if (Array.isArray(modification)) {
                        while (modification.length > 0) {
                            let callEx = node as ts.CallExpression;
                            switch (modification[0] as number) {
                                case 0: // change first parameter to constant in modification[1]
                                    node = ts.setTextRange(
                                        ts.createCall(callEx.expression, undefined, [
                                            ts.createLiteral(modification[1] as string | number | boolean),
                                            ...callEx.arguments.slice(1)
                                        ]),
                                        callEx
                                    );
                                    modification = modification.slice(2);
                                    break;
                                case 1: // set argument count to modification[1]
                                    node = ts.setTextRange(
                                        ts.createCall(
                                            callEx.expression,
                                            undefined,
                                            sliceAndPad(callEx.arguments, 0, modification[1] as number)
                                        ),
                                        callEx
                                    );
                                    modification = modification.slice(2);
                                    break;
                                case 2: // set parameter with index modification[1] to modification[2] and set argument count to modification[3]
                                    node = ts.setTextRange(
                                        ts.createCall(callEx.expression, undefined, [
                                            ...sliceAndPad(callEx.arguments, 0, modification[1] as number),
                                            ts.createLiteral(modification[2] as string | number | boolean),
                                            ...sliceAndPad(
                                                callEx.arguments,
                                                (modification[1] as number) + 1,
                                                modification[3] as number
                                            )
                                        ]),
                                        callEx
                                    );
                                    modification = modification.slice(4);
                                    break;
                                case 3: // set parameter with index modification[1] to modification[2] plus original content and set argument count to modification[3]
                                    node = ts.setTextRange(
                                        ts.createCall(callEx.expression, undefined, [
                                            ...sliceAndPad(callEx.arguments, 0, modification[1] as number),
                                            ts.createAdd(
                                                ts.createLiteral(modification[2] as string | number | boolean),
                                                callEx.arguments[modification[1] as number]
                                            ),
                                            ...sliceAndPad(
                                                callEx.arguments,
                                                (modification[1] as number) + 1,
                                                modification[3] as number
                                            )
                                        ]),
                                        callEx
                                    );
                                    modification = modification.slice(4);
                                    break;
                                case 4: // set function name to modification[1]
                                    node = ts.setTextRange(
                                        ts.createCall(
                                            ts.createPropertyAccess(
                                                (<ts.PropertyAccessExpression>callEx.expression).expression,
                                                <string>modification[1]
                                            ),
                                            undefined,
                                            callEx.arguments
                                        ),
                                        callEx
                                    );
                                    modification = modification.slice(2);
                                    break;
                                case 5: // remove first parameter
                                    node = ts.setTextRange(
                                        ts.createCall(callEx.expression, undefined, callEx.arguments.slice(1)),
                                        callEx
                                    );
                                    modification = modification.slice(1);
                                    break;
                                default:
                                    throw new Error("Unknown modification type " + modification[0] + " for " + id);
                            }
                        }
                    }
                }
                //bb.trace(ts.SyntaxKind[node.kind]);
                return node;
            }
            return ts.visitEachChild(node, visitor, context);
        }
    ]
};

function sliceAndPad(args: ts.NodeArray<ts.Expression>, start: number, end: number) {
    var res = args.slice(start, end);
    while (res.length < end - start) {
        res.push(ts.createVoidZero());
    }
    return res;
}

interface SourceCache {
    changeId: number;
    program: ts.Program;
    typeChecker: ts.TypeChecker;
}

let evalSourceCache = new Map<string, SourceCache>();

function evalTrueSourceByExportName(
    dtsFileName: string,
    varName: string,
    resolveStringLiteral?: (sl: ts.StringLiteral) => string
): any {
    if (!dtsFileName.endsWith(".d.ts")) return undefined;
    dtsFileName = dtsFileName.substr(0, dtsFileName.length - 4);
    let changeId: number | undefined;
    let ext = ["ts", "tsx", "jsx", "js"].find(ext => (changeId = bb.getChangeId(dtsFileName + ext)) != null);
    if (ext === undefined) return undefined;
    let sourceName = dtsFileName + ext;
    var sc = evalSourceCache.get(sourceName);
    if (sc !== undefined) {
        if (sc.changeId !== changeId) sc = undefined;
    }
    if (sc === undefined) {
        let program = ts.createProgram([sourceName], compilerOptions, createCompilerHost());
        let typeChecker = program.getTypeChecker();
        let diagnostics = program.getSyntacticDiagnostics();
        wasError = false;
        reportDiagnostics(diagnostics);
        if (diagnostics.length === 0) {
            let diagnostics = program.getGlobalDiagnostics();
            reportDiagnostics(diagnostics);
            if (diagnostics.length === 0) {
                let diagnostics = program.getSemanticDiagnostics();
                reportDiagnostics(diagnostics);
            }
        }
        if (wasError) {
            bb.trace("Should not happen but compiling " + sourceName + " we got errors export name:" + varName);
            return undefined;
        }
        sc = {
            changeId: changeId!,
            program,
            typeChecker
        };
        evalSourceCache.set(sourceName, sc);
    }
    let program = sc.program;
    let typeChecker = sc.typeChecker;
    let sourceAst = program.getSourceFile(sourceName);
    let s = typeChecker.tryGetMemberInModuleExports(varName, typeChecker.getSymbolAtLocation(sourceAst)!);
    if (s == null) return undefined;
    if (s.flags & ts.SymbolFlags.Variable) {
        let varDecl = <ts.VariableDeclaration>s.valueDeclaration;
        if ((varDecl.parent!.flags & ts.NodeFlags.Const) !== 0) {
            if (varDecl.initializer != null) {
                return evalNode(varDecl.initializer, typeChecker, resolveStringLiteral);
            } else {
                bb.trace("initializer null in evalTrueSourceByExportName " + dtsFileName + " " + varName);
            }
        }
    }
    return undefined;
}

function traceNode(n: ts.Node) {
    bb.trace(n.getSourceFile().fileName + " " + (<any>ts).SyntaxKind[n.kind]);
}

function evalNode(n: ts.Node, tc: ts.TypeChecker, resolveStringLiteral?: (sl: ts.StringLiteral) => string): any {
    //traceNode(n);
    switch (n.kind) {
        case ts.SyntaxKind.StringLiteral: {
            let nn = <ts.StringLiteral>n;
            if (resolveStringLiteral) {
                return resolveStringLiteral(nn);
            }
            return nn.text;
        }
        case ts.SyntaxKind.NumericLiteral: {
            let nn = <ts.LiteralExpression>n;
            return parseFloat(nn.text);
        }
        case ts.SyntaxKind.TrueKeyword:
            return true;
        case ts.SyntaxKind.FalseKeyword:
            return false;
        case ts.SyntaxKind.NullKeyword:
            return null;
        case ts.SyntaxKind.PrefixUnaryExpression: {
            let nn = <ts.PrefixUnaryExpression>n;
            let operand = evalNode(nn.operand, tc, resolveStringLiteral);
            if (operand !== undefined) {
                let op = null;
                switch (nn.operator) {
                    case ts.SyntaxKind.PlusToken:
                        op = "+";
                        break;
                    case ts.SyntaxKind.MinusToken:
                        op = "-";
                        break;
                    case ts.SyntaxKind.TildeToken:
                        op = "~";
                        break;
                    case ts.SyntaxKind.ExclamationToken:
                        op = "!";
                        break;
                    default:
                        return undefined;
                }
                var f = new Function("a", "return " + op + "a");
                return f(operand);
            }
            return undefined;
        }
        case ts.SyntaxKind.BinaryExpression: {
            let nn = <ts.BinaryExpression>n;
            let left = evalNode(nn.left, tc, resolveStringLiteral);
            let right = evalNode(nn.right, tc);
            if (left !== undefined && right !== undefined) {
                let op = null;
                switch (nn.operatorToken.kind) {
                    case ts.SyntaxKind.BarBarToken:
                    case ts.SyntaxKind.AmpersandAmpersandToken:
                    case ts.SyntaxKind.BarToken:
                    case ts.SyntaxKind.CaretToken:
                    case ts.SyntaxKind.AmpersandToken:
                    case ts.SyntaxKind.EqualsEqualsToken:
                    case ts.SyntaxKind.ExclamationEqualsToken:
                    case ts.SyntaxKind.EqualsEqualsEqualsToken:
                    case ts.SyntaxKind.ExclamationEqualsEqualsToken:
                    case ts.SyntaxKind.LessThanToken:
                    case ts.SyntaxKind.GreaterThanToken:
                    case ts.SyntaxKind.LessThanEqualsToken:
                    case ts.SyntaxKind.GreaterThanEqualsToken:
                    case ts.SyntaxKind.InstanceOfKeyword:
                    case ts.SyntaxKind.InKeyword:
                    case ts.SyntaxKind.LessThanLessThanToken:
                    case ts.SyntaxKind.GreaterThanGreaterThanToken:
                    case ts.SyntaxKind.GreaterThanGreaterThanGreaterThanToken:
                    case ts.SyntaxKind.PlusToken:
                    case ts.SyntaxKind.MinusToken:
                    case ts.SyntaxKind.AsteriskToken:
                    case ts.SyntaxKind.SlashToken:
                    case ts.SyntaxKind.PercentToken:
                        op = nn.operatorToken.getText();
                        break;
                    default:
                        return undefined;
                }
                var f = new Function("a", "b", "return a " + op + " b");
                return f(left, right);
            }
            return undefined;
        }
        case ts.SyntaxKind.ConditionalExpression: {
            let nn = <ts.ConditionalExpression>n;
            var cond = evalNode(nn.condition, tc);
            if (cond === undefined) return undefined;
            let e = cond ? nn.whenTrue : nn.whenFalse;
            return evalNode(e, tc, resolveStringLiteral);
        }
        case ts.SyntaxKind.ExportAssignment: {
            let nn = <ts.ExportAssignment>n;
            return evalNode(nn.expression, tc, resolveStringLiteral);
        }
        case ts.SyntaxKind.Identifier:
        case ts.SyntaxKind.PropertyAccessExpression: {
            let s = tc.getSymbolAtLocation(n);
            if (s == null) return undefined;
            if ((s.flags & ts.SymbolFlags.Alias) !== 0 && n.kind === ts.SyntaxKind.PropertyAccessExpression) {
                if (s.declarations == null || s.declarations.length !== 1) return undefined;
                let decl = <ts.ImportSpecifier>s.declarations[0];
                return evalNode(decl, tc, resolveStringLiteral);
            } else if ((s.flags & ts.SymbolFlags.Alias) !== 0 && n.kind === ts.SyntaxKind.Identifier) {
                if (s.declarations == null || s.declarations.length !== 1) return undefined;
                let decl = <ts.ImportSpecifier>s.declarations[0];
                if (decl.kind !== ts.SyntaxKind.ImportSpecifier) return undefined;
                if (
                    decl.parent &&
                    decl.parent.parent &&
                    decl.parent.parent.parent &&
                    decl.parent.parent.parent.kind === ts.SyntaxKind.ImportDeclaration
                ) {
                    let impdecl = <ts.ImportDeclaration>decl.parent.parent.parent;
                    let s2 = tc.getSymbolAtLocation(impdecl.moduleSpecifier);
                    if (s2 && s2.exports!.get(decl.propertyName!.escapedText)) {
                        let s3 = s2.exports!.get(decl.propertyName!.escapedText);
                        if (s3 == null) return undefined;
                        let exportAssign = <ts.ExportAssignment>s3.declarations![0];
                        return evalNode(exportAssign, tc, resolveStringLiteral);
                    }
                }
            } else if ((s.flags & ts.SymbolFlags.Property) !== 0 && n.kind === ts.SyntaxKind.PropertyAccessExpression) {
                let obj = evalNode((<ts.PropertyAccessExpression>n).expression, tc, resolveStringLiteral);
                if (typeof obj !== "object") return undefined;
                let name = (<ts.PropertyAccessExpression>n).name.text;
                return obj[name];
            } else if (s.flags & ts.SymbolFlags.Variable) {
                let varDecl = <ts.VariableDeclaration>s.valueDeclaration;
                if ((varDecl.parent!.flags & ts.NodeFlags.Const) !== 0) {
                    if (varDecl.initializer != null) {
                        return evalNode(varDecl.initializer, tc, resolveStringLiteral);
                    } else {
                        let dtsFileName = varDecl.getSourceFile().fileName;
                        let varName = (<ts.Identifier>varDecl.name).text;
                        return evalTrueSourceByExportName(dtsFileName, varName, resolveStringLiteral);
                    }
                }
            }
            return undefined;
        }
        case ts.SyntaxKind.TypeAssertionExpression: {
            let nn = <ts.TypeAssertion>n;
            return evalNode(nn.expression, tc, resolveStringLiteral);
        }
        case ts.SyntaxKind.ObjectLiteralExpression: {
            let ole = <ts.ObjectLiteralExpression>n;
            let res: { [name: string]: any } = {};
            for (let i = 0; i < ole.properties.length; i++) {
                let prop = ole.properties[i];
                if (
                    prop.kind === ts.SyntaxKind.PropertyAssignment &&
                    (prop.name.kind === ts.SyntaxKind.Identifier || prop.name.kind === ts.SyntaxKind.StringLiteral)
                ) {
                    let name =
                        prop.name.kind === ts.SyntaxKind.Identifier
                            ? (<ts.Identifier>prop.name).text
                            : (<ts.StringLiteral>prop.name).text;
                    res[name] = evalNode((<ts.PropertyAssignment>prop).initializer, tc, resolveStringLiteral);
                }
            }
            return res;
        }
        default: {
            //console.log((<any>ts).SyntaxKind[n.kind]);
            return undefined;
        }
    }
}

interface SourceInfo {
    sourceFile: ts.SourceFile;
    // module name, module main file
    bobrilNamespace: string | undefined;
    bobrilImports: { [name: string]: string };
    bobrilG11NNamespace: string | undefined;
    bobrilG11NImports: { [name: string]: string };
    styleDefs: StyleDefInfo[];
    sprites: SpriteInfo[];
    trs: TranslationMessage[];
    assets: AssetInfo[];
}

interface AssetInfo {
    callExpression: ts.CallExpression;
    name?: string;
}

interface StyleDefInfo {
    callExpression: ts.CallExpression;
    isEx: boolean;
    name?: string;
    userNamed: boolean;
}

interface SpriteInfo {
    callExpression: ts.CallExpression;
    name?: string;
    color?: string;
    hasColor?: boolean;
    x?: number;
    y?: number;
    width?: number;
    height?: number;
}

interface TranslationMessage {
    callExpression: ts.CallExpression;
    message: string | number | undefined;
    withParams: boolean;
    knownParams?: string[];
    hint?: string;
    justFormat: boolean;
}

function isBobrilFunction(name: string, callExpression: ts.CallExpression, sourceInfo: SourceInfo): boolean {
    let text = callExpression.expression.getText();
    return text === sourceInfo.bobrilNamespace + "." + name || text === sourceInfo.bobrilImports[name];
}

function isBobrilG11NFunction(name: string, callExpression: ts.CallExpression, sourceInfo: SourceInfo): boolean {
    let text = callExpression.expression.getText();
    return text === sourceInfo.bobrilG11NNamespace + "." + name || text === sourceInfo.bobrilG11NImports[name];
}

function extractBindings(
    bindings: ts.NamespaceImport | ts.NamedImports,
    ns: string | undefined,
    ims: { [name: string]: string }
): string | undefined {
    if (bindings.kind === ts.SyntaxKind.NamedImports) {
        let namedBindings = <ts.NamedImports>bindings;
        for (let i = 0; i < namedBindings.elements.length; i++) {
            let binding = namedBindings.elements[i];
            ims[(binding.propertyName || binding.name).text] = binding.name.text;
        }
    } else if (ns == null && bindings.kind === ts.SyntaxKind.NamespaceImport) {
        return (<ts.NamespaceImport>bindings).name.text;
    }
    return ns;
}

function gatherSourceInfo(
    source: ts.SourceFile,
    tc: ts.TypeChecker,
    resolvePathStringLiteral: (sl: ts.StringLiteral) => string
): SourceInfo {
    let result: SourceInfo = {
        sourceFile: source,
        bobrilNamespace: undefined,
        bobrilImports: Object.create(null),
        bobrilG11NNamespace: undefined,
        bobrilG11NImports: Object.create(null),
        sprites: [],
        styleDefs: [],
        trs: [],
        assets: []
    };
    function visit(n: ts.Node) {
        if (n.kind === ts.SyntaxKind.ImportDeclaration) {
            let id = <ts.ImportDeclaration>n;
            let moduleSymbol = tc.getSymbolAtLocation(id.moduleSpecifier);
            if (moduleSymbol == null) return;
            let fn = moduleSymbol.valueDeclaration!.getSourceFile().fileName;
            if (id.importClause) {
                let bindings = id.importClause.namedBindings!;
                if (/bobriln?\/index\.(?:d\.)?ts$/i.test(fn)) {
                    result.bobrilNamespace = extractBindings(bindings, result.bobrilNamespace, result.bobrilImports);
                } else if (/bobril-g11n\/index\.(?:d\.)?ts$/i.test(fn)) {
                    result.bobrilG11NNamespace = extractBindings(
                        bindings,
                        result.bobrilG11NNamespace,
                        result.bobrilG11NImports
                    );
                }
            }
        } else if (n.kind === ts.SyntaxKind.CallExpression) {
            let ce = <ts.CallExpression>n;
            if (isBobrilFunction("asset", ce, result)) {
                let res = evalNode(ce.arguments[0], tc, resolvePathStringLiteral);
                result.assets.push({
                    callExpression: ce,
                    name: res
                });
                if (res === undefined) {
                    reportErrorInTSNode(ce, -5, "First parameter of b.asset must be resolved as constant string");
                }
            } else if (isBobrilFunction("sprite", ce, result)) {
                let si: SpriteInfo = { callExpression: ce };
                si.hasColor = ce.arguments.length >= 2;
                for (let i = 0; i < ce.arguments.length; i++) {
                    let res = evalNode(ce.arguments[i], tc, i === 0 ? resolvePathStringLiteral : undefined); // first argument is path
                    if (res !== undefined)
                        switch (i) {
                            case 0:
                                if (typeof res === "string") si.name = res;
                                break;
                            case 1:
                                if (typeof res === "string") si.color = res;
                                break;
                            case 2:
                                if (typeof res === "number") si.width = res;
                                break;
                            case 3:
                                if (typeof res === "number") si.height = res;
                                break;
                            case 4:
                                if (typeof res === "number") si.x = res;
                                break;
                            case 5:
                                if (typeof res === "number") si.y = res;
                                break;
                            default:
                                reportErrorInTSNode(ce, -6, "b.sprite cannot have more than 6 parameters");
                        }
                }
                result.sprites.push(si);
            } else if (isBobrilG11NFunction("t", ce, result)) {
                let item: TranslationMessage = {
                    callExpression: ce,
                    message: "",
                    withParams: false,
                    knownParams: undefined,
                    hint: undefined,
                    justFormat: false
                };
                item.message = evalNode(ce.arguments[0], tc);
                if (ce.arguments.length >= 2) {
                    let parArg = ce.arguments[1];
                    item.withParams = parArg.kind != ts.SyntaxKind.NullKeyword && parArg.getText() != "undefined";
                    if (item.withParams) {
                        let params = evalNode(ce.arguments[1], tc);
                        item.knownParams = params != undefined && typeof params === "object" ? Object.keys(params) : [];
                    }
                }
                if (ce.arguments.length >= 3) {
                    item.hint = evalNode(ce.arguments[2], tc);
                }
                result.trs.push(item);
            } else if (isBobrilG11NFunction("f", ce, result)) {
                let item: TranslationMessage = {
                    callExpression: ce,
                    message: "",
                    withParams: false,
                    knownParams: undefined,
                    hint: undefined,
                    justFormat: true
                };
                item.message = evalNode(ce.arguments[0], tc);
                if (ce.arguments.length >= 2) {
                    item.withParams = true;
                    let params = evalNode(ce.arguments[1], tc);
                    item.knownParams = params !== undefined && typeof params === "object" ? Object.keys(params) : [];
                }
                result.trs.push(item);
            } else {
                let isStyleDef = isBobrilFunction("styleDef", ce, result);
                if (isStyleDef || isBobrilFunction("styleDefEx", ce, result)) {
                    let item: StyleDefInfo = {
                        callExpression: ce,
                        isEx: !isStyleDef,
                        userNamed: false
                    };
                    if (ce.arguments.length == 3 + (item.isEx ? 1 : 0)) {
                        item.name = evalNode(ce.arguments[ce.arguments.length - 1], tc);
                        item.userNamed = true;
                    } else {
                        if (ce.parent!.kind === ts.SyntaxKind.VariableDeclaration) {
                            let vd = <ts.VariableDeclaration>ce.parent;
                            item.name = (<ts.Identifier>vd.name).text;
                        } else if (ce.parent!.kind === ts.SyntaxKind.BinaryExpression) {
                            let be = <ts.BinaryExpression>ce.parent;
                            if (
                                be.operatorToken != null &&
                                be.left != null &&
                                be.operatorToken.kind === ts.SyntaxKind.FirstAssignment &&
                                be.left.kind === ts.SyntaxKind.Identifier
                            ) {
                                item.name = (<ts.Identifier>be.left).text;
                            }
                        }
                    }
                    result.styleDefs.push(item);
                }
            }
        }
        ts.forEachChild(n, visit);
    }
    visit(source);
    return result;
}

function reportErrorInTSNode(node: ts.Node, code: number, message: string) {
    let sf = node.getSourceFile();
    let locStart = sf.getLineAndCharacterOfPosition(node.getStart());
    let locEnd = sf.getLineAndCharacterOfPosition(node.getEnd());
    bb.reportTypeScriptDiagFile(
        true,
        code,
        message,
        sf.fileName,
        locStart.line,
        locStart.character,
        locEnd.line,
        locEnd.character
    );
}
