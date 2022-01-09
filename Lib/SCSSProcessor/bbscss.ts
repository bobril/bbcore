class URL {
    _url: string;

    constructor(url: string, base?: string | URL) {
        if (base == undefined) base = "";
        if (base instanceof URL) base = base.href;
        this._url = bb.join(base, url);
    }

    get href() {
        return this._url;
    }

    set href(v: string) {
        this._url = v;
    }

    get origin() {
        return "";
    }

    get protocol() {
        return "file:";
    }

    set protocol(_v: string) {
        throw new Error("not implemented");
    }

    get username() {
        return "";
    }

    set username(_v: string) {}

    get password() {
        return "";
    }

    set password(_v: string) {}

    get host() {
        return "";
    }

    set host(_v: string) {
        throw new Error("not implemented");
    }

    get hostname() {
        return "";
    }

    set hostname(_v: string) {
        throw new Error("not implemented");
    }

    get port() {
        return "";
    }

    set port(_v: string) {
        throw new Error("not implemented");
    }

    get pathname() {
        throw new Error("not implemented");
    }

    set pathname(_v: string) {
        throw new Error("not implemented");
    }

    get search() {
        return "";
    }

    set search(_v: string) {}

    get searchParams() {
        throw new Error("not implemented");
    }

    get hash() {
        return "";
    }

    set hash(_v: string) {}

    toJSON() {
        return this.href;
    }
}

type Syntax = "scss" | "indented" | "css";
type OutputStyle = "expanded" | "compressed";

interface SourceLocation {
    /**
     * The 0-based index of this location within its source file, in terms of
     * UTF-16 code units.
     */
    offset: number;

    /** The 0-based line number of this location. */
    line: number;

    /** The 0-based column number of this location. */
    column: number;
}

interface SourceSpan {
    start: SourceLocation;
    end: SourceLocation;

    url?: URL;

    /** The text covered by the span. */
    text: string;

    context?: string;
}

interface Logger {
    warn?(
        message: string,
        options: {
            deprecation: boolean;
            span?: SourceSpan;
            stack?: string;
        }
    ): void;

    debug?(message: string, options: { span: SourceSpan }): void;
}

interface FileImporter {
    findFileUrl(url: string, options: { fromImport: boolean }): URL | null;
}

interface Importer {
    canonicalize(url: string, options: { fromImport: boolean }): URL | null;

    load(canonicalUrl: URL): ImporterResult | null;
}

interface ImporterResult {
    contents: string;
    syntax: Syntax;
    sourceMapUrl?: URL;
}

interface Options {
    alertAscii: false;

    alertColor: false;

    importers?: (Importer | FileImporter)[];

    loadPaths?: string[];

    logger?: Logger;

    quietDeps: false;

    sourceMap?: boolean;

    style?: OutputStyle;

    verbose?: boolean;

    importer: Importer | FileImporter;

    url: URL;
}

interface CompileResult {
    css: string;
    loadedUrls: URL[];
}

var exports: {
    load(modules: {}): void;
    compileString(content: string, options: Options): CompileResult;
} = {} as any;

declare const bb: IBB;

interface IBB {
    load(url: string): string;
    finish(result: string): void;
    fail(result: string): void;
    log(text: string): void;
    join(base: string, url: string): string;
}

function bbCompileScss(source: string, from: string) {
    bb.log(JSON.stringify(Object.keys(exports)));
    try {
        bb.finish(
            exports.compileString(source, {
                url: new URL(from),
                alertAscii: false,
                alertColor: false,
                quietDeps: false,
                style: "compressed",
                logger: {},
                importer: {
                    canonicalize(url: string, _options) {
                        return new URL(url);
                    },
                    load(canonicalUrl: URL) {
                        return { contents: bb.load(canonicalUrl.toString()), syntax: "scss" };
                    },
                },
            }).css
        );
    } catch (e: any) {
        bb.fail(e.message);
    }
}

function bbInit() {
    exports.load({});
}
function require(s: string): any {
    if (s == "util") {
        return { inspect: { custom: Symbol() } };
    }
    bb.log(s);
    return {};
}
var global = globalThis;

(global as any).URL = URL;

var process = { env: {} };
