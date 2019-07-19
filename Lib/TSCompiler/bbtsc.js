"use strict";
/// <reference path="../node_modules/typescript/lib/typescriptServices.d.ts" />
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
var FileWatcher = /** @class */ (function () {
    function FileWatcher(path, callback) {
        this.path = path;
        this.callback = callback;
        this.changeId = bb.getChangeId(path);
        this.closed = false;
    }
    FileWatcher.prototype.check = function () {
        if (this.closed)
            return;
        var newChangeId = bb.getChangeId(this.path);
        var oldChangeId = this.changeId;
        if (newChangeId !== oldChangeId) {
            this.changeId = newChangeId;
            this.callback(this.path, newChangeId === undefined
                ? ts.FileWatcherEventKind.Deleted
                : oldChangeId === undefined
                    ? ts.FileWatcherEventKind.Created
                    : ts.FileWatcherEventKind.Changed);
        }
    };
    FileWatcher.prototype.close = function () {
        if (this.closed)
            return;
        this.closed = true;
        watchDirMap.delete(this.path);
    };
    return FileWatcher;
}());
var DirWatcher = /** @class */ (function () {
    function DirWatcher(path, callback, recursive) {
        this.path = path;
        this.callback = callback;
        this.recursive = recursive;
        this.changeId = bb.getChangeId(path);
        this.closed = false;
    }
    DirWatcher.prototype.check = function () {
        if (this.closed)
            return;
        var newChangeId = bb.getChangeId(this.path);
        var oldChangeId = this.changeId;
        if (newChangeId !== oldChangeId) {
            this.changeId = newChangeId;
            this.callback(this.path);
        }
    };
    DirWatcher.prototype.close = function () {
        if (this.closed)
            return;
        this.closed = true;
        watchDirMap.delete(this.path);
    };
    return DirWatcher;
}());
var watchFileMap = new Map();
var watchDirMap = new Map();
var launchBuild;
var mySys = {
    args: [],
    newLine: "\n",
    useCaseSensitiveFileNames: true,
    createDirectory: function () { },
    write: function (s) {
        bb.trace(s);
    },
    writeOutputIsTTY: function () {
        return false;
    },
    setTimeout: function () {
        var args = [];
        for (var _i = 0; _i < arguments.length; _i++) {
            args[_i] = arguments[_i];
        }
        launchBuild = args[0];
        return 1;
    },
    clearTimeout: function () { },
    writeFile: function (path, _data, _writeOrderMark) {
        bb.trace("should not be called writeFile: " + path);
    },
    readFile: function (path, _encoding) {
        return bb.readFile(path);
    },
    fileExists: function (path) {
        return bb.fileExists(path);
    },
    directoryExists: function (path) {
        return bb.dirExists(path);
    },
    getExecutingFilePath: function () {
        return bbCurrentDirectory;
    },
    getCurrentDirectory: function () {
        return bbCurrentDirectory;
    },
    exit: function (exitCode) {
        bb.trace("should not be called exit: " + exitCode);
    },
    resolvePath: function (path) {
        bb.trace("resolvePath:" + path);
        return path;
    },
    getDirectories: function (path) {
        return bb.getDirectories(path).split("|");
    },
    realpath: function (path) {
        return bb.realPath(path);
    },
    readDirectory: function (path, _extensions, _exclude, _include, _depth) {
        bb.trace("should not be called readDirectory: " + path);
        return [];
    },
    watchFile: function (path, callback, _pollingInterval) {
        var res = watchFileMap.get(path);
        if (res !== undefined) {
            res.close();
        }
        res = new FileWatcher(path, callback);
        watchFileMap.set(path, res);
        return res;
    },
    watchDirectory: function (path, callback, recursive) {
        var res = watchDirMap.get(path);
        if (res !== undefined) {
            res.close();
        }
        res = new DirWatcher(path, callback, recursive || false);
        watchDirMap.set(path, res);
        return res;
    }
};
function reportWatchStatusChanged(diagnostic) {
    bb.reportTypeScriptDiag(diagnostic.category === ts.DiagnosticCategory.Error, diagnostic.code, ts.flattenDiagnosticMessageText(diagnostic.messageText, "\n"));
}
var watchProgram;
function bbCreateWatchProgram(fileNames) {
    fixCompilerOptions();
    compilerOptions.noEmit = true;
    var host = ts.createWatchCompilerHost(fileNames.split("|"), compilerOptions, mySys, ts.createSemanticDiagnosticsBuilderProgram, reportDiagnostic, reportWatchStatusChanged);
    host.getDefaultLibLocation = function () { return bbDefaultLibLocation; };
    host.getDefaultLibFileName = function (options) { return bbDefaultLibLocation + "/" + ts.getDefaultLibFileName(options); };
    host.trace = function (s) {
        bb.trace(s);
    };
    watchProgram = ts.createWatchProgram(host);
}
function bbUpdateSourceList(fileNames) {
    watchProgram.updateRootFileNames(fileNames.split("|"));
}
function bbTriggerUpdate() {
    watchFileMap.forEach(function (w) { return w.check(); });
    watchDirMap.forEach(function (w) { return w.check(); });
    var launch = launchBuild;
    launchBuild = undefined;
    if (launch !== undefined)
        launch();
}
