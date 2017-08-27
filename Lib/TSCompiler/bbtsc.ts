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
        var locStart = diagnostic.file.getLineAndCharacterOfPosition(diagnostic.start);
        var locEnd = diagnostic.file.getLineAndCharacterOfPosition(diagnostic.start + diagnostic.length);
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
