// usage: node ts6pool.mjs <rootDir> <workers> <dir1> [dir2...] — TS 6.0.3 transpileModule over N worker threads
import { Worker, isMainThread, parentPort, workerData } from "node:worker_threads";
import { createRequire } from "node:module";
import fs from "node:fs";
import path from "node:path";

if (isMainThread) {
    const [root, n, ...dirs] = process.argv.slice(2);
    const walk = (d, acc) => {
        for (const e of fs.readdirSync(d, { withFileTypes: true })) {
            if (e.name === "node_modules" || e.name === "dist" || e.name.startsWith(".")) continue;
            const p = path.join(d, e.name);
            if (e.isDirectory()) walk(p, acc);
            else if (/\.tsx?$/.test(e.name) && !e.name.endsWith(".d.ts")) acc.push(p);
        }
        return acc;
    };
    const files = dirs.flatMap((d) => walk(path.resolve(root, d), [])).map((f) => ({ fileName: path.relative(root, f), input: fs.readFileSync(f, "utf8") }));
    const workers = Array.from({ length: +n }, () => new Worker(new URL(import.meta.url)));
    await Promise.all(workers.map((w) => new Promise((r) => { w.once("message", r); w.postMessage({ warm: true }); })));
    for (const pass of [1, 2]) {
        const t = performance.now();
        let i = 0;
        await Promise.all(workers.map((w) => new Promise((resolve) => {
            const next = () => { if (i >= files.length) return resolve(); w.postMessage(files[i++]); };
            w.on("message", next);
            next();
        }).then(() => w.removeAllListeners("message"))));
        const ms = performance.now() - t;
        console.log(`ts6 ${n} worker threads pass ${pass}: ${ms.toFixed(0)}ms (${(ms / files.length * 1000).toFixed(0)}us/file) files=${files.length}`);
    }
    workers.forEach((w) => w.terminate());
} else {
    const ts = createRequire(import.meta.url)("ts6");
    const compilerOptions = {
        sourceMap: true, useDefineForClassFields: false, allowImportingTsExtensions: true, noEmit: true, reactNamespace: "b",
        experimentalDecorators: true, noEmitHelpers: true, allowJs: true, removeComments: false, strict: true,
        target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.React,
    };
    parentPort.on("message", (f) => {
        if (!f.warm) ts.transpileModule(f.input, { compilerOptions, fileName: f.fileName, reportDiagnostics: true });
        else ts.transpileModule("let a = 1;", { compilerOptions, fileName: "w.ts" });
        parentPort.postMessage(0);
    });
}
