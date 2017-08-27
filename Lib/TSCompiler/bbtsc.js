"use strict";
/// <reference path="../node_modules/typescript/lib/typescriptServices.d.ts" />
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
        getEnvironmentVariable: function (name) { bb.trace("Getting ENV " + name); return ""; },
        getDirectories: function (name) { return bb.getDirectories(name).split("|"); },
        realpath: function (path) { return bb.realPath(path); },
        resolveModuleNames: function (moduleNames, containingFile) {
            return moduleNames.map(function (n) {
                var r = bb.resolveModuleName(n, containingFile).split("|");
                if (r.length < 3)
                    return null;
                var res = { resolvedFileName: r[0], isExternalLibraryImport: r[1] == "true", extension: ts.Extension[r[2]] };
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
}
function reportDiagnostic(diagnostic) {
    if (diagnostic.file) {
        var locStart = diagnostic.file.getLineAndCharacterOfPosition(diagnostic.start);
        var locEnd = diagnostic.file.getLineAndCharacterOfPosition(diagnostic.start + diagnostic.length);
        bb.reportTypeScriptDiagFile(diagnostic.category === ts.DiagnosticCategory.Error, diagnostic.code, ts.flattenDiagnosticMessageText(diagnostic.messageText, '\n'), diagnostic.file.fileName, locStart.line, locStart.character, locEnd.line, locEnd.character);
    }
    else {
        bb.reportTypeScriptDiag(diagnostic.category === ts.DiagnosticCategory.Error, diagnostic.code, ts.flattenDiagnosticMessageText(diagnostic.messageText, '\n'));
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
}
function bbEmitProgram() {
    var res = program.emit();
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
