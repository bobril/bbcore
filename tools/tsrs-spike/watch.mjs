// usage: node watch.mjs <binary> <cwd> <baseOverlay.json> <label>
// Runs `<binary> --watch` on an overlay that adds probe files, edits the probes, measures save -> "Found N errors".
import { spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import readline from "node:readline";

const [bin, cwd, baseOverlay, label] = process.argv.slice(2);
const dir = path.join(path.dirname(baseOverlay), "watch-" + label);
fs.mkdirSync(dir, { recursive: true });
const local = path.join(dir, "probeLocal.ts");
const global = path.join(dir, "probeGlobal.ts");
fs.writeFileSync(local, "export const probe: number = 1;\n");
fs.writeFileSync(global, "export {};\ndeclare global { interface Window { __probe0: number } }\n");
const base = JSON.parse(fs.readFileSync(baseOverlay, "utf8"));
const overlay = path.join(dir, "tsconfig.json");
fs.writeFileSync(overlay, JSON.stringify({ ...base, files: [...base.files, local, global] }, null, 1));

const child = spawn(bin, ["--watch", "-p", overlay, "--noEmit", "--pretty", "false", ...(process.env.EXTRA ? process.env.EXTRA.split(" ") : [])], { cwd });
const banners = new Set();
let waiter = null;
let t0 = 0;
const rl = readline.createInterface({ input: child.stdout });
readline.createInterface({ input: child.stderr }).on("line", (l) => console.error("[stderr] " + l));
rl.on("line", (raw) => {
    const line = raw.replace(/\x1B\[[0-?]*[ -/]*[@-~]/g, "");
    if (!/ error TS|^ /.test(line)) banners.add(line.replace(/^\[?[\d:. APM]+\]? ?(- )?/, "").replace(/\d+/g, "N"));
    const m = /Found (\d+) errors?\. Watching for file changes\./.exec(line);
    if (m && waiter) { const w = waiter; waiter = null; w({ ms: performance.now() - t0, errors: +m[1] }); }
});
const next = () => new Promise((r) => (waiter = r));

t0 = performance.now();
const initial = await next();
console.log(`${label} initial ${initial.ms.toFixed(0)}ms errors=${initial.errors}`);
const results = { local: [], global: [] };
for (let i = 1; i <= 6; i++) {
    for (const kind of ["local", "global"]) {
        await new Promise((r) => setTimeout(r, 400));
        const p = next();
        t0 = performance.now();
        if (kind === "local")
            fs.writeFileSync(local, `export const probe: number = ${i % 2 ? '"error"' : i};\n`);
        else
            fs.writeFileSync(global, `export {};\ndeclare global { interface Window { __probe${i}: number } }\n`);
        const r = await Promise.race([p, new Promise((res) => setTimeout(() => res({ ms: NaN, errors: -1 }), 30000))]);
        results[kind].push(r.ms);
        console.log(`${label} ${kind} edit ${i}: ${r.ms.toFixed(0)}ms errors=${r.errors}`);
    }
}
const median = (a) => { const s = [...a].sort((x, y) => x - y); return s[Math.floor(s.length / 2)]; };
console.log(`${label} MEDIAN local=${median(results.local).toFixed(0)}ms global=${median(results.global).toFixed(0)}ms`);
console.log(`${label} banners: ${JSON.stringify([...banners])}`);
child.kill();
