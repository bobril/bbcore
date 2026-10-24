// usage: node transpile.mjs <rootDir> <outDir> <dir1> [dir2...]
// Transpiles every .ts/.tsx (no .d.ts) with bbcore's transpile options via tsrs API, TS7 API and TS 6.0.3,
// compares outputs and times each path.
import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import { API as TsrsAPI } from "tsrs/unstable/sync";
import { API as Ts7API } from "ts7/unstable/sync";
import { API as TsrsAsyncAPI } from "tsrs/unstable/async";

const require = createRequire(import.meta.url);
const ts6 = require("ts6");
const here = path.dirname(new URL(import.meta.url).pathname);
const [root, outDir, ...dirs] = process.argv.slice(2);
fs.mkdirSync(outDir, { recursive: true });

function walk(d, acc) {
    for (const e of fs.readdirSync(d, { withFileTypes: true })) {
        if (e.name === "node_modules" || e.name === "dist" || e.name.startsWith(".")) continue;
        const p = path.join(d, e.name);
        if (e.isDirectory()) walk(p, acc);
        else if (/\.tsx?$/.test(e.name) && !e.name.endsWith(".d.ts")) acc.push(p);
    }
    return acc;
}
const files = dirs.flatMap((d) => walk(path.resolve(root, d), [])).map((f) => ({
    fileName: path.relative(root, f),
    input: fs.readFileSync(f, "utf8"),
}));
console.log(`files=${files.length} bytes=${files.reduce((s, f) => s + f.input.length, 0)}`);

// bbcore GetDefaultTSCompilerOptions + BuildModuleCtx transpile overrides (module=CommonJS, sourceMap, no moduleResolution)
const opts = {
    sourceMap: true, skipLibCheck: false, skipDefaultLibCheck: true, useDefineForClassFields: false,
    allowImportingTsExtensions: true, noEmit: true, declaration: false, reactNamespace: "b",
    experimentalDecorators: true, noEmitHelpers: true, allowJs: true, checkJs: false, removeComments: false,
    resolveJsonModule: true, strict: true, allowSyntheticDefaultImports: true, forceConsistentCasingInFileNames: true,
    lib: ["lib.es2022.d.ts", "lib.dom.d.ts"],
};
const nativeOpts = { ...opts, target: 9 /*ES2022*/, module: 1 /*CommonJS*/, jsx: 2 /*React*/ };
const ts6Opts = { ...opts, target: ts6.ScriptTarget.ES2022, module: ts6.ModuleKind.CommonJS, jsx: ts6.JsxEmit.React };

function timed(label, fn) {
    const t = performance.now();
    const r = fn();
    const ms = performance.now() - t;
    console.log(`${label}: ${ms.toFixed(0)}ms (${(ms / files.length * 1000).toFixed(0)}us/file)`);
    return r;
}
const norm = (d) => (d ?? []).map((x) => `${x.code}@${x.startPosition?.line ?? "?"}:${x.startPosition?.character ?? "?"}`).sort().join(",");

const tStart = performance.now();
const tsrs = new TsrsAPI({ cwd: root, tsserverPath: path.join(here, "node_modules/@tsrs/darwin-arm64/tsrs") });
tsrs.transpileModule("let a = 1;", { compilerOptions: nativeOpts, fileName: "warm.ts" });
console.log(`tsrs api spawn+first call: ${(performance.now() - tStart).toFixed(0)}ms`);
const t7Start = performance.now();
const ts7 = new Ts7API({ cwd: root, tsserverPath: path.join(here, "node_modules/@typescript/typescript-darwin-arm64/lib/tsc") });
ts7.transpileModule("let a = 1;", { compilerOptions: nativeOpts, fileName: "warm.ts" });
console.log(`ts7 api spawn+first call: ${(performance.now() - t7Start).toFixed(0)}ms`);

const run = (api) => files.map((f) => api.transpileModule(f.input, { compilerOptions: nativeOpts, fileName: f.fileName, reportDiagnostics: true }));
const rTsrs = timed("tsrs sync api", () => run(tsrs));
const rTs7 = timed("ts7 sync api", () => run(ts7));
timed("tsrs sync api (2nd pass)", () => run(tsrs));
const rTs6 = timed("ts6 in-process transpileModule", () => files.map((f) => ts6.transpileModule(f.input, { compilerOptions: ts6Opts, fileName: f.fileName, reportDiagnostics: true })));
timed("ts6 in-process (2nd pass)", () => files.map((f) => ts6.transpileModule(f.input, { compilerOptions: ts6Opts, fileName: f.fileName, reportDiagnostics: true })));
tsrs.close();
ts7.close();

// async API: one process, many requests in flight
for (const conc of [1, 8, 32]) {
    const a = new TsrsAsyncAPI({ cwd: root, tsserverPath: path.join(here, "node_modules/@tsrs/darwin-arm64/tsrs") });
    await a.transpileModule("let a = 1;", { compilerOptions: nativeOpts, fileName: "warm.ts" });
    const t = performance.now();
    let i = 0;
    await Promise.all(Array.from({ length: conc }, async () => {
        while (i < files.length) {
            const f = files[i++];
            await a.transpileModule(f.input, { compilerOptions: nativeOpts, fileName: f.fileName, reportDiagnostics: true });
        }
    }));
    const ms = performance.now() - t;
    console.log(`tsrs async api concurrency=${conc}: ${ms.toFixed(0)}ms (${(ms / files.length * 1000).toFixed(0)}us/file)`);
    await a.close();
}

// pool of N tsrs --api processes, each with one request in flight
for (const procs of [4, 8]) {
    const apis = await Promise.all(Array.from({ length: procs }, async () => {
        const a = new TsrsAsyncAPI({ cwd: root, tsserverPath: path.join(here, "node_modules/@tsrs/darwin-arm64/tsrs") });
        await a.transpileModule("let a = 1;", { compilerOptions: nativeOpts, fileName: "warm.ts" });
        return a;
    }));
    const t = performance.now();
    let i = 0;
    await Promise.all(apis.map(async (a) => {
        while (i < files.length) {
            const f = files[i++];
            await a.transpileModule(f.input, { compilerOptions: nativeOpts, fileName: f.fileName, reportDiagnostics: true });
        }
    }));
    const ms = performance.now() - t;
    console.log(`tsrs async api pool of ${procs} processes: ${ms.toFixed(0)}ms (${(ms / files.length * 1000).toFixed(0)}us/file)`);
    await Promise.all(apis.map((a) => a.close()));
}

let jsDiff7 = 0, mapDiff7 = 0, diagDiff7 = 0, jsDiff6 = 0, wsDiff6 = 0, diagDiff6 = 0, diagFiles = 0;
const samples6 = [], samples7 = [];
const ws = (s) => s.replace(/\/\/# sourceMappingURL=.*$/m, "").replace(/\s+/g, "");
files.forEach((f, i) => {
    const a = rTsrs[i], b = rTs7[i], c = rTs6[i];
    if (a.outputText !== b.outputText) { jsDiff7++; if (samples7.length < 5) samples7.push(i); }
    if ((a.sourceMapText ?? "") !== (b.sourceMapText ?? "")) mapDiff7++;
    if (norm(a.diagnostics) !== norm(b.diagnostics)) diagDiff7++;
    if ((a.diagnostics ?? []).length) diagFiles++;
    if (a.outputText !== c.outputText) {
        jsDiff6++;
        if (ws(a.outputText) !== ws(c.outputText)) wsDiff6++;
        if (samples6.length < 80) samples6.push(i);
    }
    const d6 = (c.diagnostics ?? []).map((x) => x.code).sort().join(","), dr = (a.diagnostics ?? []).map((x) => x.code).sort().join(",");
    if (d6 !== dr) diagDiff6++;
});
console.log(`tsrs vs ts7: js differs in ${jsDiff7}, sourcemap differs in ${mapDiff7}, diagnostics differ in ${diagDiff7} files`);
console.log(`tsrs vs ts6: js differs in ${jsDiff6} files, ${wsDiff6} after whitespace removal; diagnostic codes differ in ${diagDiff6}; files with tsrs diagnostics: ${diagFiles}`);
for (const [tag, list, other] of [["ts7", samples7, rTs7], ["ts6", samples6, rTs6]])
    for (const i of list) {
        const base = path.join(outDir, tag + "-" + files[i].fileName.replace(/[\/]/g, "__"));
        fs.writeFileSync(base + ".tsrs.js", rTsrs[i].outputText);
        fs.writeFileSync(base + "." + tag + ".js", other[i].outputText);
    }
