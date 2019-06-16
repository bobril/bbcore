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

function createCompilerHost(setParentNodes?: boolean): ts.CompilerHost {
    function getCanonicalFileName(fileName: string): string {
        return fileName;
    }

    function getSourceFile(
        fileName: string,
        languageVersion: ts.ScriptTarget,
        onError?: (message: string) => void
    ): ts.SourceFile {
        let text = bb.readFile(fileName, true);
        if (text == undefined) {
            if (onError) {
                onError("Read Error in " + fileName);
            }
            throw new Error("Cannot getSourceFile " + fileName);
        }
        let res = ts.createSourceFile(fileName, text, languageVersion, setParentNodes);
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
