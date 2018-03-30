"use strict";
/// <reference path="../node_modules/typescript/lib/typescriptServices.d.ts" />
var __read = (this && this.__read) || function (o, n) {
    var m = typeof Symbol === "function" && o[Symbol.iterator];
    if (!m) return o;
    var i = m.call(o), r, ar = [], e;
    try {
        while ((n === void 0 || n-- > 0) && !(r = i.next()).done) ar.push(r.value);
    }
    catch (error) { e = { error: error }; }
    finally {
        try {
            if (r && !r.done && (m = i["return"])) m.call(i);
        }
        finally { if (e) throw e.error; }
    }
    return ar;
};
var __spread = (this && this.__spread) || function () {
    for (var ar = [], i = 0; i < arguments.length; i++) ar = ar.concat(__read(arguments[i]));
    return ar;
};
var parseCache = {};
function createCompilerHost(setParentNodes) {
    function getCanonicalFileName(fileName) {
        return fileName;
    }
    function getSourceFile(fileName, languageVersion, onError) {
        var version = bb.getChangeId(fileName);
        if (version == undefined) {
            if (onError) {
                onError("Read Error in " + fileName);
            }
            throw new Error("Cannot getSourceFile " + fileName);
        }
        var cache = parseCache[fileName];
        if (cache && version == cache[0])
            return cache[1];
        var text = bb.readFile(fileName, true);
        if (text == undefined) {
            if (onError) {
                onError("Read Error in " + fileName);
            }
            throw new Error("Cannot getSourceFile " + fileName);
        }
        var res = ts.createSourceFile(fileName, text, languageVersion, setParentNodes);
        if (fileName.endsWith(".d.ts"))
            parseCache[fileName] = [version, res];
        return res;
    }
    function writeFile(fileName, data, _writeByteOrderMark, onError) {
        if (!bb.writeFile(fileName, data)) {
            if (onError) {
                onError("Write failed " + fileName);
            }
        }
    }
    return {
        getSourceFile: getSourceFile,
        getDefaultLibLocation: function () { return bbDefaultLibLocation; },
        getDefaultLibFileName: function (options) { return bbDefaultLibLocation + "/" + ts.getDefaultLibFileName(options); },
        writeFile: writeFile,
        getCurrentDirectory: function () { return bbCurrentDirectory; },
        useCaseSensitiveFileNames: function () { return true; },
        getCanonicalFileName: getCanonicalFileName,
        getNewLine: function () { return "\n"; },
        fileExists: function (name) { return bb.fileExists(name); },
        readFile: function (fileName) { return bb.readFile(fileName, false); },
        trace: function (text) { return bb.trace(text); },
        directoryExists: function (dir) { return bb.dirExists(dir); },
        getEnvironmentVariable: function (name) {
            bb.trace("Getting ENV " + name);
            return "";
        },
        getDirectories: function (name) { return bb.getDirectories(name).split("|"); },
        realpath: function (path) { return bb.realPath(path); },
        resolveModuleNames: function (moduleNames, containingFile) {
            return moduleNames.map(function (n) {
                var r = bb.resolveModuleName(n, containingFile).split("|");
                if (r.length < 3)
                    return null;
                var res = {
                    resolvedFileName: r[0],
                    isExternalLibraryImport: r[1] == "true",
                    extension: ts.Extension[r[2]]
                };
                return res;
            });
        }
    };
}
var compilerOptions = ts.getDefaultCompilerOptions();
function bbInitDefaultCompilerOptions() {
    compilerOptions = ts.getDefaultCompilerOptions();
}
function bbSetCurrentCompilerOptions(json) {
    compilerOptions = JSON.parse(json);
}
function bbMergeCurrentCompilerOptions(json) {
    Object.assign(compilerOptions, JSON.parse(json));
}
function bbGetCurrentCompilerOptions() {
    return JSON.stringify(compilerOptions);
}
var program;
var typeChecker;
function addLibPrefixPostfix(names) {
    for (var i = 0; i < names.length; i++) {
        if (names[i].startsWith("lib."))
            continue;
        names[i] = "lib." + names[i] + ".d.ts";
    }
}
function bbStartTSPerformance() {
    ts.performance.enable();
}
function bbCreateProgram(rootNames) {
    if (compilerOptions.lib != null)
        addLibPrefixPostfix(compilerOptions.lib);
    //bb.trace(JSON.stringify(compilerOptions));
    program = ts.createProgram(rootNames.split("|"), compilerOptions, createCompilerHost());
    typeChecker = program.getTypeChecker();
}
var wasError = false;
function reportDiagnostic(diagnostic) {
    if (diagnostic.category === ts.DiagnosticCategory.Error)
        wasError = true;
    if (diagnostic.file) {
        var locStart = diagnostic.file.getLineAndCharacterOfPosition(diagnostic.start);
        var locEnd = diagnostic.file.getLineAndCharacterOfPosition(diagnostic.start + diagnostic.length);
        bb.reportTypeScriptDiagFile(diagnostic.category === ts.DiagnosticCategory.Error, diagnostic.code, ts.flattenDiagnosticMessageText(diagnostic.messageText, "\n"), diagnostic.file.fileName, locStart.line, locStart.character, locEnd.line, locEnd.character);
    }
    else {
        bb.reportTypeScriptDiag(diagnostic.category === ts.DiagnosticCategory.Error, diagnostic.code, ts.flattenDiagnosticMessageText(diagnostic.messageText, "\n"));
    }
}
function reportDiagnostics(diagnostics) {
    for (var i = 0; i < diagnostics.length; i++) {
        reportDiagnostic(diagnostics[i]);
    }
}
function bbCompileProgram() {
    var diagnostics = program.getSyntacticDiagnostics();
    reportDiagnostics(diagnostics);
    if (diagnostics.length === 0) {
        var diagnostics_1 = program.getGlobalDiagnostics();
        reportDiagnostics(diagnostics_1);
        if (diagnostics_1.length === 0) {
            var diagnostics_2 = program.getSemanticDiagnostics();
            reportDiagnostics(diagnostics_2);
        }
    }
    return program.getCommonSourceDirectory();
}
var sourceInfos = Object.create(null);
function bbGatherSourceInfo() {
    var sourceFiles = program.getSourceFiles();
    var resolvePathStringLiteral = function (nn) {
        return bb.resolvePathStringLiteral(nn.getSourceFile().fileName, nn.text);
    };
    for (var i = 0; i < sourceFiles.length; i++) {
        var sourceFile = sourceFiles[i];
        if (sourceFile.isDeclarationFile)
            continue;
        var sourceInfo = gatherSourceInfo(sourceFile, typeChecker, resolvePathStringLiteral);
        sourceInfos[sourceFile.fileName] = sourceInfo;
        bb.reportSourceInfo(sourceFile.fileName, JSON.stringify(toJsonableSourceInfo(sourceInfo)));
    }
}
function toJsonableSourceInfo(sourceInfo) {
    return {
        assets: sourceInfo.assets.map(function (a) { return ({
            nodeId: ts.getNodeId(a.callExpression),
            name: a.name
        }); }),
        sprites: sourceInfo.sprites.map(function (s) { return ({
            nodeId: ts.getNodeId(s.callExpression),
            name: s.name,
            color: s.color,
            hasColor: s.hasColor,
            width: s.width,
            height: s.height,
            x: s.x,
            y: s.y
        }); }),
        translations: sourceInfo.trs.map(function (t) { return ({
            nodeId: ts.getNodeId(t.callExpression),
            message: typeof t.message === "string" ? t.message : undefined,
            hint: t.hint,
            justFormat: t.justFormat,
            withParams: t.withParams,
            knownParams: t.knownParams
        }); }),
        styleDefs: sourceInfo.styleDefs.map(function (s) { return ({
            nodeId: ts.getNodeId(s.callExpression),
            name: s.name,
            userNamed: s.userNamed,
            isEx: s.isEx
        }); })
    };
}
function bbEmitProgram() {
    var res = program.emit(undefined, undefined, undefined, undefined, transformers);
    reportDiagnostics(res.diagnostics);
    return !res.emitSkipped;
}
function bbFinishTSPerformance() {
    var res = {};
    ts.performance.forEachMeasure(function (k, d) {
        res[k] = d;
    });
    ts.performance.disable();
    return JSON.stringify(res);
}
var transformers = {
    before: [
        function (context) { return function (node) {
            var modifications = JSON.parse(bb.getModifications(node.getSourceFile().fileName));
            if (modifications == null)
                return node;
            function visitor(node) {
                node = ts.visitEachChild(node, visitor, context);
                var id = node.id;
                if (typeof id === "number") {
                    var modification = modifications[id];
                    if (Array.isArray(modification)) {
                        while (modification.length > 0) {
                            var callEx = node;
                            switch (modification[0]) {
                                case 0:// change first parameter to constant in modification[1]
                                    node = ts.setTextRange(ts.createCall(callEx.expression, undefined, __spread([
                                        ts.createLiteral(modification[1])
                                    ], callEx.arguments.slice(1))), callEx);
                                    modification = modification.slice(2);
                                    break;
                                case 1:// set argument count to modification[1]
                                    node = ts.setTextRange(ts.createCall(callEx.expression, undefined, sliceAndPad(callEx.arguments, 0, modification[1])), callEx);
                                    modification = modification.slice(2);
                                    break;
                                case 2:// set parameter with index modification[1] to modification[2] and set argument count to modification[3]
                                    node = ts.setTextRange(ts.createCall(callEx.expression, undefined, __spread(sliceAndPad(callEx.arguments, 0, modification[1]), [
                                        ts.createLiteral(modification[2])
                                    ], sliceAndPad(callEx.arguments, modification[1] + 1, modification[3]))), callEx);
                                    modification = modification.slice(4);
                                    break;
                                case 3:// set parameter with index modification[1] to modification[2] plus original content and set argument count to modification[3]
                                    node = ts.setTextRange(ts.createCall(callEx.expression, undefined, __spread(sliceAndPad(callEx.arguments, 0, modification[1]), [
                                        ts.createAdd(ts.createLiteral(modification[2]), callEx.arguments[modification[1]])
                                    ], sliceAndPad(callEx.arguments, modification[1] + 1, modification[3]))), callEx);
                                    modification = modification.slice(4);
                                    break;
                                case 4:// set function name to modification[1]
                                    node = ts.setTextRange(ts.createCall(ts.createPropertyAccess(callEx.expression.expression, modification[1]), undefined, callEx.arguments), callEx);
                                    modification = modification.slice(2);
                                    break;
                                case 5:// remove first parameter
                                    node = ts.setTextRange(ts.createCall(callEx.expression, undefined, callEx.arguments.slice(1)), callEx);
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
        }; }
    ]
};
function sliceAndPad(args, start, end) {
    var res = args.slice(start, end);
    while (res.length < end - start) {
        res.push(ts.createVoidZero());
    }
    return res;
}
var evalSourceCache = new Map();
function evalTrueSourceByExportName(dtsFileName, varName, resolveStringLiteral) {
    if (!dtsFileName.endsWith(".d.ts"))
        return undefined;
    dtsFileName = dtsFileName.substr(0, dtsFileName.length - 4);
    var changeId;
    var ext = ["ts", "tsx", "jsx", "js"].find(function (ext) { return (changeId = bb.getChangeId(dtsFileName + ext)) != null; });
    if (ext === undefined)
        return undefined;
    var sourceName = dtsFileName + ext;
    var sc = evalSourceCache.get(sourceName);
    if (sc !== undefined) {
        if (sc.changeId !== changeId)
            sc = undefined;
    }
    if (sc === undefined) {
        var program_1 = ts.createProgram([sourceName], compilerOptions, createCompilerHost());
        var typeChecker_1 = program_1.getTypeChecker();
        var diagnostics = program_1.getSyntacticDiagnostics();
        wasError = false;
        reportDiagnostics(diagnostics);
        if (diagnostics.length === 0) {
            var diagnostics_3 = program_1.getGlobalDiagnostics();
            reportDiagnostics(diagnostics_3);
            if (diagnostics_3.length === 0) {
                var diagnostics_4 = program_1.getSemanticDiagnostics();
                reportDiagnostics(diagnostics_4);
            }
        }
        if (wasError) {
            bb.trace("Should not happen but compiling " + sourceName + " we got errors export name:" + varName);
            return undefined;
        }
        sc = {
            changeId: changeId,
            program: program_1,
            typeChecker: typeChecker_1
        };
        evalSourceCache.set(sourceName, sc);
    }
    var program = sc.program;
    var typeChecker = sc.typeChecker;
    var sourceAst = program.getSourceFile(sourceName);
    var s = typeChecker.tryGetMemberInModuleExports(varName, typeChecker.getSymbolAtLocation(sourceAst));
    if (s == null)
        return undefined;
    if (s.flags & ts.SymbolFlags.Variable) {
        var varDecl = s.valueDeclaration;
        if ((varDecl.parent.flags & ts.NodeFlags.Const) !== 0) {
            if (varDecl.initializer != null) {
                return evalNode(varDecl.initializer, typeChecker, resolveStringLiteral);
            }
            else {
                bb.trace("initializer null in evalTrueSourceByExportName " + dtsFileName + " " + varName);
            }
        }
    }
    return undefined;
}
function traceNode(n) {
    bb.trace(n.getSourceFile().fileName + " " + ts.SyntaxKind[n.kind]);
}
function evalNode(n, tc, resolveStringLiteral) {
    //traceNode(n);
    switch (n.kind) {
        case ts.SyntaxKind.StringLiteral: {
            var nn = n;
            if (resolveStringLiteral) {
                return resolveStringLiteral(nn);
            }
            return nn.text;
        }
        case ts.SyntaxKind.NumericLiteral: {
            var nn = n;
            return parseFloat(nn.text);
        }
        case ts.SyntaxKind.TrueKeyword:
            return true;
        case ts.SyntaxKind.FalseKeyword:
            return false;
        case ts.SyntaxKind.NullKeyword:
            return null;
        case ts.SyntaxKind.PrefixUnaryExpression: {
            var nn = n;
            var operand = evalNode(nn.operand, tc, resolveStringLiteral);
            if (operand !== undefined) {
                var op = null;
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
            var nn = n;
            var left = evalNode(nn.left, tc, resolveStringLiteral);
            var right = evalNode(nn.right, tc);
            if (left !== undefined && right !== undefined) {
                var op = null;
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
            var nn = n;
            var cond = evalNode(nn.condition, tc);
            if (cond === undefined)
                return undefined;
            var e = cond ? nn.whenTrue : nn.whenFalse;
            return evalNode(e, tc, resolveStringLiteral);
        }
        case ts.SyntaxKind.ExportAssignment: {
            var nn = n;
            return evalNode(nn.expression, tc, resolveStringLiteral);
        }
        case ts.SyntaxKind.Identifier:
        case ts.SyntaxKind.PropertyAccessExpression: {
            var s = tc.getSymbolAtLocation(n);
            if (s == null)
                return undefined;
            if ((s.flags & ts.SymbolFlags.Alias) !== 0 && n.kind === ts.SyntaxKind.PropertyAccessExpression) {
                if (s.declarations == null || s.declarations.length !== 1)
                    return undefined;
                var decl = s.declarations[0];
                return evalNode(decl, tc, resolveStringLiteral);
            }
            else if ((s.flags & ts.SymbolFlags.Alias) !== 0 && n.kind === ts.SyntaxKind.Identifier) {
                if (s.declarations == null || s.declarations.length !== 1)
                    return undefined;
                var decl = s.declarations[0];
                if (decl.kind !== ts.SyntaxKind.ImportSpecifier)
                    return undefined;
                if (decl.parent &&
                    decl.parent.parent &&
                    decl.parent.parent.parent &&
                    decl.parent.parent.parent.kind === ts.SyntaxKind.ImportDeclaration) {
                    var impdecl = decl.parent.parent.parent;
                    var s2 = tc.getSymbolAtLocation(impdecl.moduleSpecifier);
                    if (s2 && s2.exports.get(decl.propertyName.escapedText)) {
                        var s3 = s2.exports.get(decl.propertyName.escapedText);
                        if (s3 == null)
                            return undefined;
                        var exportAssign = s3.declarations[0];
                        return evalNode(exportAssign, tc, resolveStringLiteral);
                    }
                }
            }
            else if ((s.flags & ts.SymbolFlags.Property) !== 0 && n.kind === ts.SyntaxKind.PropertyAccessExpression) {
                var obj = evalNode(n.expression, tc, resolveStringLiteral);
                if (typeof obj !== "object")
                    return undefined;
                var name = n.name.text;
                return obj[name];
            }
            else if (s.flags & ts.SymbolFlags.Variable) {
                var varDecl = s.valueDeclaration;
                if ((varDecl.parent.flags & ts.NodeFlags.Const) !== 0) {
                    if (varDecl.initializer != null) {
                        return evalNode(varDecl.initializer, tc, resolveStringLiteral);
                    }
                    else {
                        var dtsFileName = varDecl.getSourceFile().fileName;
                        var varName = varDecl.name.text;
                        return evalTrueSourceByExportName(dtsFileName, varName, resolveStringLiteral);
                    }
                }
            }
            return undefined;
        }
        case ts.SyntaxKind.TypeAssertionExpression: {
            var nn = n;
            return evalNode(nn.expression, tc, resolveStringLiteral);
        }
        case ts.SyntaxKind.ObjectLiteralExpression: {
            var ole = n;
            var res = {};
            for (var i = 0; i < ole.properties.length; i++) {
                var prop = ole.properties[i];
                if (prop.kind === ts.SyntaxKind.PropertyAssignment &&
                    (prop.name.kind === ts.SyntaxKind.Identifier || prop.name.kind === ts.SyntaxKind.StringLiteral)) {
                    var name = prop.name.kind === ts.SyntaxKind.Identifier
                        ? prop.name.text
                        : prop.name.text;
                    res[name] = evalNode(prop.initializer, tc, resolveStringLiteral);
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
function isBobrilFunction(name, callExpression, sourceInfo) {
    var text = callExpression.expression.getText();
    return text === sourceInfo.bobrilNamespace + "." + name || text === sourceInfo.bobrilImports[name];
}
function isBobrilG11NFunction(name, callExpression, sourceInfo) {
    var text = callExpression.expression.getText();
    return text === sourceInfo.bobrilG11NNamespace + "." + name || text === sourceInfo.bobrilG11NImports[name];
}
function extractBindings(bindings, ns, ims) {
    if (bindings.kind === ts.SyntaxKind.NamedImports) {
        var namedBindings = bindings;
        for (var i = 0; i < namedBindings.elements.length; i++) {
            var binding = namedBindings.elements[i];
            ims[(binding.propertyName || binding.name).text] = binding.name.text;
        }
    }
    else if (ns == null && bindings.kind === ts.SyntaxKind.NamespaceImport) {
        return bindings.name.text;
    }
    return ns;
}
function gatherSourceInfo(source, tc, resolvePathStringLiteral) {
    var result = {
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
    function visit(n) {
        if (n.kind === ts.SyntaxKind.ImportDeclaration) {
            var id = n;
            var moduleSymbol = tc.getSymbolAtLocation(id.moduleSpecifier);
            if (moduleSymbol == null)
                return;
            var fn = moduleSymbol.valueDeclaration.getSourceFile().fileName;
            if (id.importClause) {
                var bindings = id.importClause.namedBindings;
                if (/bobriln?\/index\.(?:d\.)?ts$/i.test(fn)) {
                    result.bobrilNamespace = extractBindings(bindings, result.bobrilNamespace, result.bobrilImports);
                }
                else if (/bobril-g11n\/index\.(?:d\.)?ts$/i.test(fn)) {
                    result.bobrilG11NNamespace = extractBindings(bindings, result.bobrilG11NNamespace, result.bobrilG11NImports);
                }
            }
        }
        else if (n.kind === ts.SyntaxKind.CallExpression) {
            var ce = n;
            if (isBobrilFunction("asset", ce, result)) {
                var res = evalNode(ce.arguments[0], tc, resolvePathStringLiteral);
                result.assets.push({
                    callExpression: ce,
                    name: res
                });
                if (res === undefined) {
                    reportErrorInTSNode(ce, -5, "First parameter of b.asset must be resolved as constant string");
                }
            }
            else if (isBobrilFunction("sprite", ce, result)) {
                var si = { callExpression: ce };
                si.hasColor = ce.arguments.length >= 2;
                for (var i = 0; i < ce.arguments.length; i++) {
                    var res = evalNode(ce.arguments[i], tc, i === 0 ? resolvePathStringLiteral : undefined); // first argument is path
                    if (res !== undefined)
                        switch (i) {
                            case 0:
                                if (typeof res === "string")
                                    si.name = res;
                                break;
                            case 1:
                                if (typeof res === "string")
                                    si.color = res;
                                break;
                            case 2:
                                if (typeof res === "number")
                                    si.width = res;
                                break;
                            case 3:
                                if (typeof res === "number")
                                    si.height = res;
                                break;
                            case 4:
                                if (typeof res === "number")
                                    si.x = res;
                                break;
                            case 5:
                                if (typeof res === "number")
                                    si.y = res;
                                break;
                            default:
                                reportErrorInTSNode(ce, -6, "b.sprite cannot have more than 6 parameters");
                        }
                }
                result.sprites.push(si);
            }
            else if (isBobrilG11NFunction("t", ce, result)) {
                var item = {
                    callExpression: ce,
                    message: "",
                    withParams: false,
                    knownParams: undefined,
                    hint: undefined,
                    justFormat: false
                };
                item.message = evalNode(ce.arguments[0], tc);
                if (ce.arguments.length >= 2) {
                    item.withParams = true;
                    var params = evalNode(ce.arguments[1], tc);
                    item.knownParams = params != undefined && typeof params === "object" ? Object.keys(params) : [];
                }
                if (ce.arguments.length >= 3) {
                    item.hint = evalNode(ce.arguments[2], tc);
                }
                result.trs.push(item);
            }
            else if (isBobrilG11NFunction("f", ce, result)) {
                var item = {
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
                    var params = evalNode(ce.arguments[1], tc);
                    item.knownParams = params !== undefined && typeof params === "object" ? Object.keys(params) : [];
                }
                result.trs.push(item);
            }
            else {
                var isStyleDef = isBobrilFunction("styleDef", ce, result);
                if (isStyleDef || isBobrilFunction("styleDefEx", ce, result)) {
                    var item = {
                        callExpression: ce,
                        isEx: !isStyleDef,
                        userNamed: false
                    };
                    if (ce.arguments.length == 3 + (item.isEx ? 1 : 0)) {
                        item.name = evalNode(ce.arguments[ce.arguments.length - 1], tc);
                        item.userNamed = true;
                    }
                    else {
                        if (ce.parent.kind === ts.SyntaxKind.VariableDeclaration) {
                            var vd = ce.parent;
                            item.name = vd.name.text;
                        }
                        else if (ce.parent.kind === ts.SyntaxKind.BinaryExpression) {
                            var be = ce.parent;
                            if (be.operatorToken != null &&
                                be.left != null &&
                                be.operatorToken.kind === ts.SyntaxKind.FirstAssignment &&
                                be.left.kind === ts.SyntaxKind.Identifier) {
                                item.name = be.left.text;
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
function reportErrorInTSNode(node, code, message) {
    var sf = node.getSourceFile();
    var locStart = sf.getLineAndCharacterOfPosition(node.getStart());
    var locEnd = sf.getLineAndCharacterOfPosition(node.getEnd());
    bb.reportTypeScriptDiagFile(true, code, message, sf.fileName, locStart.line, locStart.character, locEnd.line, locEnd.character);
}
