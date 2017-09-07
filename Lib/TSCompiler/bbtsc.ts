/// <reference path="../node_modules/typescript/lib/typescriptServices.d.ts" />

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
    reportTypeScriptDiagFile(isError: boolean, code: number, text: string,
        fileName: string, startLine: number, startCharacter: number, endLine: number, endCharacter: number): void;
    // resolvedName:string|isExternalLibraryImport:boolean|extension:string(Ts,Tsx,Dts,Js,Jsx)
    resolveModuleName(name: string, containingFile: string): string;
    resolvePathStringLiteral(sourcePath: string, text: string): string;
}

let parseCache: { [fileName: string]: [number, ts.SourceFile] } = {};

function createCompilerHost(setParentNodes?: boolean): ts.CompilerHost {
    function getCanonicalFileName(fileName: string): string {
        return fileName;
    }

    function getSourceFile(fileName: string, languageVersion: ts.ScriptTarget, onError?: (message: string) => void): ts.SourceFile {
        let version = bb.getChangeId(fileName);
        if (version == undefined) {
            if (onError) {
                onError("Read Error in " + fileName);
            }
            throw new Error("Cannot getSourceFile " + fileName);
        }
        let cache = parseCache[fileName];
        if (cache && version == cache[0])
            return cache[1];
        let text = bb.readFile(fileName, true);
        if (text == undefined) {
            if (onError) {
                onError("Read Error in " + fileName);
            }
            throw new Error("Cannot getSourceFile " + fileName);
        }
        let res = ts.createSourceFile(fileName, text, languageVersion, setParentNodes);
        parseCache[fileName] = [version, res];
        return res;
    }

    function writeFile(fileName: string, data: string, _writeByteOrderMark: boolean, onError?: (message: string) => void) {
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
        fileExists: (name) => bb.fileExists(name),
        readFile: (fileName) => bb.readFile(fileName, false),
        trace: (text) => bb.trace(text),
        directoryExists: (dir) => bb.dirExists(dir),
        getEnvironmentVariable: (name: string) => { bb.trace("Getting ENV " + name); return ""; },
        getDirectories: (name: string) => bb.getDirectories(name).split("|"),
        realpath: (path) => bb.realPath(path),
        resolveModuleNames(moduleNames: string[], containingFile: string): ts.ResolvedModuleFull[] {
            return moduleNames.map((n) => {
                let r = bb.resolveModuleName(n, containingFile).split("|");
                if (r.length < 3) return null as any;
                let res: ts.ResolvedModuleFull = { resolvedFileName: r[0], isExternalLibraryImport: r[1] == "true", extension: (ts.Extension as any)[r[2]] }
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
}

function reportDiagnostic(diagnostic: ts.Diagnostic) {
    if (diagnostic.file) {
        var locStart = diagnostic.file.getLineAndCharacterOfPosition(diagnostic.start!);
        var locEnd = diagnostic.file.getLineAndCharacterOfPosition(diagnostic.start! + diagnostic.length!);
        bb.reportTypeScriptDiagFile(diagnostic.category === ts.DiagnosticCategory.Error, diagnostic.code, ts.flattenDiagnosticMessageText(diagnostic.messageText, '\n'),
            diagnostic.file.fileName, locStart.line, locStart.character, locEnd.line, locEnd.character);
    } else {
        bb.reportTypeScriptDiag(diagnostic.category === ts.DiagnosticCategory.Error, diagnostic.code, ts.flattenDiagnosticMessageText(diagnostic.messageText, '\n'));
    }
}

function reportDiagnostics(diagnostics: ts.Diagnostic[]) {
    for (var i = 0; i < diagnostics.length; i++) {
        reportDiagnostic(diagnostics[i]);
    }
}

function bbCompileProgram(): void {
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
}

const sourceInfos: { [name: string]: SourceInfo } = Object.create(null);

function bbGatherSourceInfo(): void {
    let typeChecker = program.getTypeChecker();
    let sourceFiles = program.getSourceFiles();
    const resolvePathStringLiteral = (nn: ts.StringLiteral) => bb.resolvePathStringLiteral(nn.getSourceFile().fileName, nn.text);
    for (let i = 0; i < sourceFiles.length; i++) {
        let sourceFile = sourceFiles[i];
        if (sourceFile.isDeclarationFile)
            continue;
        sourceInfos[sourceFile.fileName] = gatherSourceInfo(sourceFile, typeChecker, resolvePathStringLiteral);
    }
}

function bbEmitProgram(): boolean {
    const res = program.emit();
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

function evalNode(n: ts.Node, tc: ts.TypeChecker, resolveStringLiteral?: (sl: ts.StringLiteral) => string): any {
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
        case ts.SyntaxKind.TrueKeyword: return true;
        case ts.SyntaxKind.FalseKeyword: return false;
        case ts.SyntaxKind.NullKeyword: return null;
        case ts.SyntaxKind.PrefixUnaryExpression: {
            let nn = <ts.PrefixUnaryExpression>n;
            let operand = evalNode(nn.operand, tc, resolveStringLiteral);
            if (operand !== undefined) {
                let op = null;
                switch (nn.operator) {
                    case ts.SyntaxKind.PlusToken: op = "+"; break;
                    case ts.SyntaxKind.MinusToken: op = "-"; break;
                    case ts.SyntaxKind.TildeToken: op = "~"; break;
                    case ts.SyntaxKind.ExclamationToken: op = "!"; break;
                    default: return undefined;
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
                    default: return undefined;
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
            if (((s.flags & ts.SymbolFlags.Alias) !== 0) && n.kind === ts.SyntaxKind.PropertyAccessExpression) {
                if (s.declarations == null || s.declarations.length !== 1)
                    return undefined;
                let decl = <ts.ImportSpecifier>s.declarations[0];
                return evalNode(decl, tc, resolveStringLiteral);
            } else if (((s.flags & ts.SymbolFlags.Alias) !== 0) && n.kind === ts.SyntaxKind.Identifier) {
                if (s.declarations == null || s.declarations.length !== 1)
                    return undefined;
                let decl = <ts.ImportSpecifier>s.declarations[0];
                if (decl.kind !== ts.SyntaxKind.ImportSpecifier)
                    return undefined;
                if (decl.parent && decl.parent.parent && decl.parent.parent.parent && decl.parent.parent.parent.kind === ts.SyntaxKind.ImportDeclaration) {
                    let impdecl = <ts.ImportDeclaration>decl.parent.parent.parent;
                    let s2 = tc.getSymbolAtLocation(impdecl.moduleSpecifier);
                    if (s2 && s2.exports!.get(decl.propertyName!.escapedText)) {
                        let s3 = s2.exports!.get(decl.propertyName!.escapedText);
                        if (s3 == null)
                            return undefined;
                        let exportAssign = <ts.ExportAssignment>s3.declarations![0];
                        return evalNode(exportAssign, tc, resolveStringLiteral);
                    }
                }
            } else if (((s.flags & ts.SymbolFlags.Property) !== 0) && n.kind === ts.SyntaxKind.PropertyAccessExpression) {
                let obj = evalNode((<ts.PropertyAccessExpression>n).expression, tc, resolveStringLiteral);
                if (typeof obj !== "object")
                    return undefined;
                let name = (<ts.PropertyAccessExpression>n).name.text;
                return obj[name];
            } else if (s.flags & ts.SymbolFlags.Variable) {
                if (s.valueDeclaration!.parent!.flags & ts.NodeFlags.Const) {
                    return evalNode((<ts.VariableDeclaration>s.valueDeclaration).initializer!, tc, resolveStringLiteral);
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
                if (prop.kind === ts.SyntaxKind.PropertyAssignment && (prop.name.kind === ts.SyntaxKind.Identifier || prop.name.kind === ts.SyntaxKind.StringLiteral)) {
                    let name = prop.name.kind === ts.SyntaxKind.Identifier ? (<ts.Identifier>prop.name).text : (<ts.StringLiteral>prop.name).text;
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
    sourceDeps: [string, string][];
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
    return text === sourceInfo.bobrilNamespace + '.' + name || text === sourceInfo.bobrilImports[name];
}

function isBobrilG11NFunction(name: string, callExpression: ts.CallExpression, sourceInfo: SourceInfo): boolean {
    let text = callExpression.expression.getText();
    return text === sourceInfo.bobrilG11NNamespace + '.' + name || text === sourceInfo.bobrilG11NImports[name];
}

function extractBindings(bindings: ts.NamespaceImport | ts.NamedImports, ns: string | undefined, ims: { [name: string]: string }): string | undefined {
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

function gatherSourceInfo(source: ts.SourceFile, tc: ts.TypeChecker, resolvePathStringLiteral: (sl: ts.StringLiteral) => string): SourceInfo {
    let result: SourceInfo = {
        sourceFile: source,
        sourceDeps: [],
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
                if (/bobriln?\/index\.ts/i.test(fn)) {
                    result.bobrilNamespace = extractBindings(bindings, result.bobrilNamespace, result.bobrilImports);
                } else if (/bobril-g11n\/index\.ts/i.test(fn)) {
                    result.bobrilG11NNamespace = extractBindings(bindings, result.bobrilG11NNamespace, result.bobrilG11NImports);
                }
            }
            result.sourceDeps.push([moduleSymbol.name, fn]);
        }
        else if (n.kind === ts.SyntaxKind.ExportDeclaration) {
            let ed = <ts.ExportDeclaration>n;
            if (ed.moduleSpecifier) {
                let moduleSymbol = tc.getSymbolAtLocation(ed.moduleSpecifier);
                if (moduleSymbol == null) return;
                result.sourceDeps.push([moduleSymbol.name, moduleSymbol.valueDeclaration!.getSourceFile().fileName]);
            }
        }
        else if (n.kind === ts.SyntaxKind.CallExpression) {
            let ce = <ts.CallExpression>n;
            if (isBobrilFunction('asset', ce, result)) {
                result.assets.push({ callExpression: ce, name: evalNode(ce.arguments[0], tc, resolvePathStringLiteral) });
            } else if (isBobrilFunction('sprite', ce, result)) {
                let si: SpriteInfo = { callExpression: ce };
                for (let i = 0; i < ce.arguments.length; i++) {
                    let res = evalNode(ce.arguments[i], tc, i === 0 ? resolvePathStringLiteral : undefined); // first argument is path
                    if (res !== undefined) switch (i) {
                        case 0:
                            if (typeof res === 'string') si.name = res;
                            break;
                        case 1:
                            if (typeof res === 'string') si.color = res;
                            break;
                        case 2:
                            if (typeof res === 'number') si.width = res;
                            break;
                        case 3:
                            if (typeof res === 'number') si.height = res;
                            break;
                        case 4:
                            if (typeof res === 'number') si.x = res;
                            break;
                        case 5:
                            if (typeof res === 'number') si.y = res;
                            break;
                        default: throw new Error('b.sprite cannot have more than 6 parameters');
                    }
                }
                result.sprites.push(si);
            } else if (isBobrilFunction('styleDef', ce, result) || isBobrilFunction('styleDefEx', ce, result)) {
                let item: StyleDefInfo = { callExpression: ce, isEx: isBobrilFunction('styleDefEx', ce, result), userNamed: false };
                if (ce.arguments.length == 3 + (item.isEx ? 1 : 0)) {
                    item.name = evalNode(ce.arguments[ce.arguments.length - 1], tc);
                    item.userNamed = true;
                } else {
                    if (ce.parent!.kind === ts.SyntaxKind.VariableDeclaration) {
                        let vd = <ts.VariableDeclaration>ce.parent;
                        item.name = (<ts.Identifier>vd.name).text;
                    } else if (ce.parent!.kind === ts.SyntaxKind.BinaryExpression) {
                        let be = <ts.BinaryExpression>ce.parent;
                        if (be.operatorToken != null && be.left != null && be.operatorToken.kind === ts.SyntaxKind.FirstAssignment && be.left.kind === ts.SyntaxKind.Identifier) {
                            item.name = (<ts.Identifier>be.left).text;
                        }
                    }
                }
                result.styleDefs.push(item);
            } else if (isBobrilG11NFunction('t', ce, result)) {
                let item: TranslationMessage = { callExpression: ce, message: "", withParams: false, knownParams: undefined, hint: undefined, justFormat: false };
                item.message = evalNode(ce.arguments[0], tc);
                if (ce.arguments.length >= 2) {
                    item.withParams = true;
                    let params = evalNode(ce.arguments[1], tc);
                    item.knownParams = params != undefined && typeof params === "object" ? Object.keys(params) : [];
                }
                if (ce.arguments.length >= 3) {
                    item.hint = evalNode(ce.arguments[2], tc);
                }
                result.trs.push(item);
            } else if (isBobrilG11NFunction('f', ce, result)) {
                let item: TranslationMessage = { callExpression: ce, message: "", withParams: false, knownParams: undefined, hint: undefined, justFormat: true };
                item.message = evalNode(ce.arguments[0], tc);
                if (ce.arguments.length >= 2) {
                    item.withParams = true;
                    let params = evalNode(ce.arguments[1], tc);
                    item.knownParams = params !== undefined && typeof params === "object" ? Object.keys(params) : [];
                }
                result.trs.push(item);
            }
        }
        ts.forEachChild(n, visit);
    }
    visit(source);
    return result;
}
