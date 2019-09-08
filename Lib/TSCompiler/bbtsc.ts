/// <reference path="../node_modules/typescript/lib/typescriptServices.d.ts" />

declare var bbCurrentDirectory: string;
declare var bbDefaultLibLocation: string;

declare const bb: IBB;

interface IBB {
    getChangeId(fileName: string): number | undefined;
    readFile(fileName: string): string;
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
}

let compilerOptions = ts.getDefaultCompilerOptions();
let optionsNeedFix = true;

function bbInitDefaultCompilerOptions() {
    compilerOptions = ts.getDefaultCompilerOptions();
    optionsNeedFix = true;
}

function bbSetCurrentCompilerOptions(json: string) {
    compilerOptions = JSON.parse(json);
    optionsNeedFix = true;
}

function bbMergeCurrentCompilerOptions(json: string) {
    Object.assign(compilerOptions, JSON.parse(json));
    optionsNeedFix = true;
}

function bbGetCurrentCompilerOptions(): string {
    return JSON.stringify(compilerOptions);
}

function addLibPrefixPostfix(names: string[]) {
    for (var i = 0; i < names.length; i++) {
        if (names[i].startsWith("lib.")) continue;
        names[i] = "lib." + names[i] + ".d.ts";
    }
}

function fixCompilerOptions() {
    if (!optionsNeedFix) return;
    optionsNeedFix = false;
    if (compilerOptions.lib != null) addLibPrefixPostfix(compilerOptions.lib);
}

let lastSourceMap: string | undefined;

function bbTranspile(fileName: string, input: string): string {
    //bb.trace(JSON.stringify(compilerOptions));
    var res = ts.transpileModule(input, { compilerOptions, reportDiagnostics: true, fileName });
    if (res.diagnostics) reportDiagnostics(res.diagnostics);
    lastSourceMap = res.sourceMapText;
    return res.outputText;
}

function bbGetLastSourceMap(): string | undefined {
    var res = lastSourceMap;
    lastSourceMap = undefined;
    return res;
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

class FileWatcher {
    path: string;
    callback: ts.FileWatcherCallback | undefined;
    changeId: number | undefined;
    content?: string;
    closed: boolean;

    constructor(path: string, callback?: ts.FileWatcherCallback) {
        this.path = path;
        this.callback = callback;
        this.changeId = bb.getChangeId(path);
        this.closed = false;
    }

    check() {
        if (this.closed) return;
        let newChangeId = bb.getChangeId(this.path);
        const oldChangeId = this.changeId;
        if (newChangeId !== oldChangeId) {
            this.changeId = newChangeId;
            if (this.callback != undefined)
                this.callback(
                    this.path,
                    newChangeId === undefined
                        ? ts.FileWatcherEventKind.Deleted
                        : oldChangeId === undefined
                        ? ts.FileWatcherEventKind.Created
                        : ts.FileWatcherEventKind.Changed
                );
            this.content = undefined;
        }
    }

    getContent(): string | undefined {
        let content = this.content;
        if (content != undefined) return content;
        if (this.changeId == undefined) {
            return undefined;
        }
        content = bb.readFile(this.path);
        this.content = content;
        return content;
    }

    close() {
        if (this.closed) return;
        bb.trace("Closed watching file " + this.path);
        this.closed = true;
        watchDirMap.delete(this.path);
    }
}

class DirWatcher {
    path: string;
    callback: ts.DirectoryWatcherCallback | undefined;
    changeId: number | undefined;
    closed: boolean;

    constructor(path: string, callback?: ts.DirectoryWatcherCallback) {
        this.path = path;
        this.callback = callback;
        this.changeId = bb.getChangeId(path);
        this.closed = false;
    }

    check() {
        if (this.closed) return;
        let newChangeId = bb.getChangeId(this.path);
        const oldChangeId = this.changeId;
        if (newChangeId !== oldChangeId) {
            this.changeId = newChangeId;
            bb.trace("Watcher changed dir: " + this.path);
            if (this.callback != undefined) this.callback(this.path);
        }
    }

    close() {
        if (this.closed) return;
        bb.trace("Closed watching dir " + this.path);
        this.closed = true;
        watchDirMap.delete(this.path);
    }
}

const watchFileMap: Map<string, FileWatcher> = new Map<string, FileWatcher>();
const watchDirMap: Map<string, DirWatcher> = new Map<string, DirWatcher>();
let launchBuild: (() => void) | undefined;

const mySys: ts.System = {
    args: [],
    newLine: "\n",
    useCaseSensitiveFileNames: true,
    createDirectory() {},
    write(s: string) {
        bb.trace(s);
    },
    writeOutputIsTTY() {
        return false;
    },
    setTimeout(...args: any) {
        launchBuild = args[0];
        return 1;
    },
    clearTimeout() {},
    writeFile(path: string, _data: string, _writeOrderMark?: boolean) {
        bb.trace("should not be called writeFile: " + path);
    },
    readFile(path: string, _encoding?: string): string {
        let res = watchFileMap.get(path);
        if (res == undefined) {
            res = new FileWatcher(path);
            watchFileMap.set(path, res);
        }
        return res.getContent()!;
    },
    fileExists(path: string): boolean {
        let res = watchFileMap.get(path);
        if (res == undefined) {
            res = new FileWatcher(path);
            watchFileMap.set(path, res);
        }
        return res.changeId != undefined;
    },
    directoryExists(path: string): boolean {
        let res = watchDirMap.get(path);
        if (res == undefined) {
            res = new DirWatcher(path);
            watchDirMap.set(path, res);
        }
        return res.changeId != undefined;
    },
    getExecutingFilePath(): string {
        return bbCurrentDirectory;
    },
    getCurrentDirectory(): string {
        return bbCurrentDirectory;
    },
    exit(exitCode?: number) {
        bb.trace("should not be called exit: " + exitCode);
    },
    resolvePath(path: string): string {
        bb.trace("resolvePath:" + path);
        return path;
    },
    getDirectories(path: string): string[] {
        return bb.getDirectories(path).split("|");
    },
    realpath(path: string): string {
        return bb.realPath(path);
    },
    readDirectory(
        path: string,
        _extensions?: ReadonlyArray<string>,
        _exclude?: ReadonlyArray<string>,
        _include?: ReadonlyArray<string>,
        _depth?: number
    ): string[] {
        bb.trace("should not be called readDirectory: " + path);
        return [];
    },
    watchFile(path: string, callback: ts.FileWatcherCallback, _pollingInterval?: number): ts.FileWatcher {
        let res = watchFileMap.get(path);
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
    watchDirectory(path: string, callback: ts.DirectoryWatcherCallback, _recursive?: boolean): ts.FileWatcher {
        let res = watchDirMap.get(path);
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

function reportWatchStatusChanged(diagnostic: ts.Diagnostic) {
    bb.reportTypeScriptDiag(
        diagnostic.category === ts.DiagnosticCategory.Error,
        diagnostic.code,
        ts.flattenDiagnosticMessageText(diagnostic.messageText, "\n")
    );
}

let watchProgram: ts.WatchOfFilesAndCompilerOptions<ts.SemanticDiagnosticsBuilderProgram> | undefined;

function bbCreateWatchProgram(fileNames: string) {
    fixCompilerOptions();
    compilerOptions.noEmit = true;
    const host = ts.createWatchCompilerHost(
        fileNames.split("|"),
        compilerOptions,
        mySys,
        ts.createSemanticDiagnosticsBuilderProgram,
        reportDiagnostic,
        reportWatchStatusChanged
    );

    host.getDefaultLibLocation = () => bbDefaultLibLocation;
    host.getDefaultLibFileName = options => bbDefaultLibLocation + "/" + ts.getDefaultLibFileName(options);
    host.trace = s => {
        bb.trace(s);
    };
    watchProgram = ts.createWatchProgram(host);
}

function bbUpdateSourceList(fileNames: string) {
    watchProgram!.updateRootFileNames(fileNames.split("|"));
}

function bbTriggerUpdate() {
    bb.trace("triggerUpdateStart");
    watchFileMap.forEach(w => w.check());
    watchDirMap.forEach(w => w.check());
    const launch = launchBuild;
    launchBuild = undefined;
    bb.trace("watches updated File: " + watchFileMap.size + " Dir: " + watchDirMap.size);
    if (launch !== undefined) launch();
    bb.trace("triggerUpdateFinish");
}

function createCompilerHost(): ts.CompilerHost {
    function getSourceFile(
        fileName: string,
        languageVersion: ts.ScriptTarget,
        onError?: (message: string) => void
    ): ts.SourceFile | undefined {
        let text: string | undefined;
        try {
            text = bb.readFile(fileName);
        } catch (e) {
            if (onError) {
                onError(e.message);
            }
            text = "";
        }
        return text !== undefined ? ts.createSourceFile(fileName, text, languageVersion, false) : undefined;
    }

    const compilerHost: ts.CompilerHost = {
        getSourceFile,
        getDefaultLibLocation: () => bbDefaultLibLocation,
        getDefaultLibFileName: options => bbDefaultLibLocation + "/" + ts.getDefaultLibFileName(options),
        writeFile() {},
        getCurrentDirectory: () => bbCurrentDirectory,
        useCaseSensitiveFileNames: () => true,
        getCanonicalFileName(path: string) {
            return path;
        },
        getNewLine: () => "\n",
        fileExists: fileName => bb.fileExists(fileName),
        readFile: fileName => bb.readFile(fileName),
        trace: (s: string) => bb.trace(s),
        directoryExists: directoryName => bb.dirExists(directoryName),
        getEnvironmentVariable(_name: string) {
            return "";
        },
        getDirectories: (path: string) => mySys.getDirectories(path),
        realpath(path: string) {
            // It should call bb.realpath, but for now this is faster
            return path;
        },
        readDirectory: (path, extensions, include, exclude, depth) =>
            mySys.readDirectory(path, extensions, include, exclude, depth)
    };
    return compilerHost;
}

function bbCheckProgram(fileNames: string) {
    fixCompilerOptions();
    compilerOptions.noEmit = true;
    const host = createCompilerHost();
    let program = ts.createProgram(fileNames.split("|"), compilerOptions, host);
    wasError = false;
    reportDiagnostics(program.getOptionsDiagnostics());
    reportDiagnostics(program.getGlobalDiagnostics());
    reportDiagnostics(program.getSyntacticDiagnostics());
    if (wasError) return;
    reportDiagnostics(program.getSemanticDiagnostics());
}
