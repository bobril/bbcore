"use strict";
/// <reference path="../node_modules/typescript/lib/typescriptServices.d.ts" />
function createCompilerHost(setParentNodes) {
    function getCanonicalFileName(fileName) {
        return fileName;
    }
    function getSourceFile(fileName, languageVersion, onError) {
        var text = bb.readFile(fileName, true);
        if (text == undefined) {
            if (onError) {
                onError("Read Error in " + fileName);
            }
            throw new Error("Cannot getSourceFile " + fileName);
        }
        var res = ts.createSourceFile(fileName, text, languageVersion, setParentNodes);
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
var optionsNeedFix = true;
function bbInitDefaultCompilerOptions() {
    compilerOptions = ts.getDefaultCompilerOptions();
    optionsNeedFix = true;
}
function bbSetCurrentCompilerOptions(json) {
    compilerOptions = JSON.parse(json);
    optionsNeedFix = true;
}
function bbMergeCurrentCompilerOptions(json) {
    Object.assign(compilerOptions, JSON.parse(json));
    optionsNeedFix = true;
}
function bbGetCurrentCompilerOptions() {
    return JSON.stringify(compilerOptions);
}
function addLibPrefixPostfix(names) {
    for (var i = 0; i < names.length; i++) {
        if (names[i].startsWith("lib."))
            continue;
        names[i] = "lib." + names[i] + ".d.ts";
    }
}
function fixCompilerOptions() {
    if (!optionsNeedFix)
        return;
    optionsNeedFix = false;
    if (compilerOptions.lib != null)
        addLibPrefixPostfix(compilerOptions.lib);
}
var lastSourceMap;
function bbTranspile(fileName, input) {
    //bb.trace(JSON.stringify(compilerOptions));
    var res = ts.transpileModule(input, { compilerOptions: compilerOptions, reportDiagnostics: true, fileName: fileName });
    if (res.diagnostics)
        reportDiagnostics(res.diagnostics);
    lastSourceMap = res.sourceMapText;
    return res.outputText;
}
function bbGetLastSourceMap() {
    var res = lastSourceMap;
    lastSourceMap = undefined;
    return res;
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
