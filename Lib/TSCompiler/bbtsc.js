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
            if (this.callback != undefined)
                this.callback(this.path, newChangeId === undefined
                    ? ts.FileWatcherEventKind.Deleted
                    : oldChangeId === undefined
                        ? ts.FileWatcherEventKind.Created
                        : ts.FileWatcherEventKind.Changed);
            this.content = undefined;
        }
    };
    FileWatcher.prototype.getContent = function () {
        var content = this.content;
        if (content != undefined)
            return content;
        if (this.changeId == undefined) {
            return undefined;
        }
        content = bb.readFile(this.path);
        this.content = content;
        return content;
    };
    FileWatcher.prototype.close = function () {
        if (this.closed)
            return;
        bb.trace("Closed watching file " + this.path);
        this.closed = true;
        watchDirMap.delete(this.path);
    };
    return FileWatcher;
}());
var DirWatcher = /** @class */ (function () {
    function DirWatcher(path, callback) {
        this.path = path;
        this.callback = callback;
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
            bb.trace("Watcher changed dir: " + this.path);
            if (this.callback != undefined)
                this.callback(this.path);
        }
    };
    DirWatcher.prototype.close = function () {
        if (this.closed)
            return;
        bb.trace("Closed watching dir " + this.path);
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
        var res = watchFileMap.get(path);
        if (res == undefined) {
            res = new FileWatcher(path);
            watchFileMap.set(path, res);
        }
        return res.getContent();
    },
    fileExists: function (path) {
        var res = watchFileMap.get(path);
        if (res == undefined) {
            res = new FileWatcher(path);
            watchFileMap.set(path, res);
        }
        return res.changeId != undefined;
    },
    directoryExists: function (path) {
        var res = watchDirMap.get(path);
        if (res == undefined) {
            res = new DirWatcher(path);
            watchDirMap.set(path, res);
        }
        return res.changeId != undefined;
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
            if (res.callback == undefined) {
                res.callback = callback;
                return res;
            }
            res.close();
        }
        res = new FileWatcher(path, callback);
        watchFileMap.set(path, res);
        return res;
    },
    watchDirectory: function (path, callback, _recursive) {
        var res = watchDirMap.get(path);
        if (res !== undefined) {
            if (res.callback == undefined) {
                res.callback = callback;
                return res;
            }
            res.close();
        }
        res = new DirWatcher(path, callback);
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
    bb.trace("triggerUpdateStart");
    watchFileMap.forEach(function (w) { return w.check(); });
    watchDirMap.forEach(function (w) { return w.check(); });
    var launch = launchBuild;
    launchBuild = undefined;
    bb.trace("watches updated File: " + watchFileMap.size + " Dir: " + watchDirMap.size);
    if (launch !== undefined)
        launch();
    bb.trace("triggerUpdateFinish");
}
function createCompilerHost() {
    function getSourceFile(fileName, languageVersion, onError) {
        var text;
        try {
            text = bb.readFile(fileName);
        }
        catch (e) {
            if (onError) {
                onError(e.message);
            }
            text = "";
        }
        return text !== undefined ? ts.createSourceFile(fileName, text, languageVersion, false) : undefined;
    }
    var compilerHost = {
        getSourceFile: getSourceFile,
        getDefaultLibLocation: function () { return bbDefaultLibLocation; },
        getDefaultLibFileName: function (options) { return bbDefaultLibLocation + "/" + ts.getDefaultLibFileName(options); },
        writeFile: function () { },
        getCurrentDirectory: function () { return bbCurrentDirectory; },
        useCaseSensitiveFileNames: function () { return true; },
        getCanonicalFileName: function (path) {
            return path;
        },
        getNewLine: function () { return "\n"; },
        fileExists: function (fileName) { return bb.fileExists(fileName); },
        readFile: function (fileName) { return bb.readFile(fileName); },
        trace: function (s) { return bb.trace(s); },
        directoryExists: function (directoryName) { return bb.dirExists(directoryName); },
        getEnvironmentVariable: function (_name) {
            return "";
        },
        getDirectories: function (path) { return mySys.getDirectories(path); },
        realpath: function (path) {
            // It should call bb.realpath, but for now this is faster
            return path;
        },
        readDirectory: function (path, extensions, include, exclude, depth) {
            return mySys.readDirectory(path, extensions, include, exclude, depth);
        }
    };
    return compilerHost;
}
function bbCheckProgram(fileNames) {
    fixCompilerOptions();
    compilerOptions.noEmit = true;
    var host = createCompilerHost();
    var program = ts.createProgram(fileNames.split("|"), compilerOptions, host);
    wasError = false;
    reportDiagnostics(program.getOptionsDiagnostics());
    reportDiagnostics(program.getGlobalDiagnostics());
    reportDiagnostics(program.getSyntacticDiagnostics());
    if (wasError)
        return;
    reportDiagnostics(program.getSemanticDiagnostics());
}
