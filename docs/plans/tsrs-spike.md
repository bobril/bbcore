# tsrs spike (Phase 0) results

Date: 2026-10-24. Plan: [tsrs.md](tsrs.md). No bbcore code was changed and no WebMma file was modified.

**Verdict: go for all three capabilities.** Typecheck and transpile output is byte-identical to the matching
TypeScript 7 build. tsrs is about 4× faster than tsgo for typechecking and about 5× faster than bbcore's current
TS 6 transpile. Two integration risks surfaced; see "Findings that change the plan".

## Setup

- Machine: Apple M2 Max, 12 cores, 64 GB, macOS (Darwin 25.6).
- Installed in a scratch directory with `npm i`:
  - `tsrs@0.18.0-ts7.1.0-dev.20260929`, which pulls in the `@tsrs/darwin-arm64` native binary.
    `tsrs --version` prints `Version 7.1.0-dev.20260930.4 (tsrs 0.18.0-ts7.1.0-dev.20260929, microsoft/TypeScript@b85298b6a81f)`.
  - **Oracle:** `typescript@7.1.0-dev.20260930.4`, the exact TS build that tsrs reports. It is the Go compiler,
    with its native binary in `@typescript/typescript-darwin-arm64/lib/tsc`.
  - `typescript@6.0.3`, the version WebMma's `.bbrc` pins for bbcore's transpile today.
  - Also measured: the Homebrew `tsgo` that WebMma uses today with `gots: true`, `7.0.0-dev.20260519.1`.
- Projects: WebMma at inspirecloud `ef39c077c27`, using the bbcore-generated `apps/*/tsconfig.json`.
- **WebMma's `node_modules` is incomplete.** `monaco-editor`, `bbseeker-extended` and the `jquery` /
  `external--redactor-ii` types are missing. TS 7.1 (tsrs and the oracle) then stops at TS6053/TS2688 before
  checking anything. Homebrew TS 7.0 checks anyway. To typecheck for real I used scratch overlay configs that
  `extends` the app `tsconfig.json`, drop the missing `monaco.d.ts`, and (for WebAdministration and WebCjm only) set
  `types: []`. The hundreds of remaining errors come from those missing packages and are identical across compilers.
- Timings are the median of 3 runs with warm OS caches. RSS comes from `/usr/bin/time -l`.

## 1. One-shot typecheck (`-p <config> --noEmit --pretty false`)

| app | files in program | diagnostics | tsrs | TS 7.1 oracle | Homebrew tsgo 7.0 | TS 6.0.3 (JS) |
| --- | ---: | ---: | --- | --- | --- | --- |
| WebAppRoot | 1594 | 6 | **0.10 s / 257 MB** | 0.42 s / 536 MB | 0.52 s / 577 MB | |
| WebOco | 2042 | 170 | **0.12 s / 297 MB** | 0.51 s / 655 MB | 0.62 s / 697 MB | |
| WebAdministration¹ | 2446 | 151 | **0.15 s / 357 MB** | 0.64 s / 779 MB | 0.73 s / 827 MB | |
| WebCjm¹ | 2289 | 2185 | **0.17 s / 386 MB** | 0.74 s / 862 MB | 0.85 s / 950 MB | |
| WebOnDemand¹ | 3153 | 812 | **0.19 s / 395 MB** | 0.79 s / 901 MB | 0.90 s / 1007 MB | 8.46 s / 1162 MB |

¹ through an overlay config, as described in Setup.

- **tsrs vs the oracle: identical on every app.** The same diagnostic lines come out in the same order (raw `diff`
  is empty, not only the sorted one). Exit codes match, and so does `--listFilesOnly` (3,153 files for WebOnDemand).
- tsrs runs at 3.5–4.4× the speed of the matching tsgo and uses about 45% of its memory.
- Homebrew tsgo 7.0 vs the 7.1 oracle: WebOco differs on 1 diagnostic and WebCjm on 9, all message-text changes
  between 7.0 and 7.1. This confirms that any equality check must use a TS build that matches the tsrs pin.
- WebOnDemand thread scaling: default 0.19 s / 395 MB, `--checkers 12` 0.19 s / 398 MB, `--checkers 4`
  0.28 s / 335 MB, `--singleThreaded` 0.98 s / 286 MB. The default needs no tuning.
- The existing `NativeTsCompiler` diagnostic regexes parse the tsrs output unchanged, including the multi-line
  `The file is in the program because:` blocks, which that compiler logs only in verbose mode.

## 2. Watch mode (`--watch`)

`watch.mjs` starts the watcher on the WebOnDemand overlay plus two probe files in the scratch directory. It then makes
six edits of each kind and measures from the file write to the `Found N errors. Watching for file changes.` line:

- **local:** a type error in a leaf module is toggled on and off,
- **global:** a `declare global { interface Window { … } }` member changes, which invalidates every file.

| | tsrs | TS 7.1 oracle |
| --- | --- | --- |
| first check | 0.37 s (warm; one cold first-ever run took 1.38 s) | 0.86 s |
| local edit, median | **39 ms** | 108 ms |
| global edit, median | **249 ms** | 761 ms |

- Error counts toggled correctly on every edit (812 ↔ 813).
- The banner lines are **identical to tsc/tsgo**: `Starting compilation in watch mode...`,
  `File change detected. Starting incremental compilation...`, `Found N errors. Watching for file changes.` The
  `NativeTsCompiler` watch state machine can therefore be reused as is.
- `--extendedDiagnostics` slows the first watch check to 3.4 s, so bbcore must not pass it by default.
- Not measured: editing a widely imported real WebMma source file, because the spike doesn't touch WebMma. The global
  probe is the worst case for invalidation.

## 3. Transpile through the API (`transpileModule`)

`transpile.mjs` transpiles every non-`.d.ts` `.ts`/`.tsx` file in WebMma `apps`, `api` and `packages`
(6,766 files, 35.9 MB). It uses bbcore's options: `GetDefaultTSCompilerOptions()` plus the `BuildModuleCtx`
overrides (`module: CommonJS`, `sourceMap: true`, no `moduleResolution`), with `target: ES2022`, `jsx: React`,
`reactNamespace: "b"`, `experimentalDecorators`, `useDefineForClassFields: false` and `noEmitHelpers`.

### Equality

| comparison | JS | source maps | diagnostics |
| --- | --- | --- | --- |
| tsrs vs TS 7.1 oracle (same API protocol) | **identical, 6766/6766** | **identical, 6766/6766** | identical (none reported) |
| tsrs vs TS 6.0.3 `ts.transpileModule` | differs in 148 files, 146 of them whitespace only | not compared | same codes |

The two TS6 → TS7 differences that aren't whitespace:

1. Redundant parentheses are dropped: `const C = (X.Y);` becomes `const C = X.Y;`. Harmless.
2. **Destructured exports** (`export const { is, get, set } = f();` in
   `apps/WebCjm/src/programs/list/programDraggableArea.tsx`):
   - TS6: `var _a; … _a = f(), exports.is = _a.is, …`
   - TS7: `({ is: exports.is, get: exports.get, set: exports.set } = f());`

   Both are correct JavaScript. The risk is bbcore's Njsast bundler, which recognizes CommonJS `exports.x = …`
   assignments. See the findings section.

The whitespace-only differences are indentation of object literals nested inside `b.createElement(...)` JSX
emit.

### Throughput (all 6,766 files)

| path | time | per file |
| --- | ---: | ---: |
| TS 6.0.3 in-process, 1 thread (warm) | 15.1 s | 2.24 ms |
| TS 6.0.3, 8 Node worker threads (warm), a proxy for bbcore's parallel V8 `CompilerPool` | 3.16 s | 0.47 ms |
| TS 7.1 oracle, sync API, 1 process | 5.6 s | 0.83 ms |
| tsrs, sync API, 1 process | 2.9 s | 0.43 ms |
| tsrs, async API, 1 process, 8 or 32 requests in flight | 3.7 s | 0.54 ms |
| tsrs, async API, **pool of 4 processes** | 1.14 s | 0.17 ms |
| tsrs, async API, **pool of 8 processes** | **0.65 s** | **0.10 ms** |

- Spawning `tsrs --api` and answering the first call takes about 7 ms, so process startup costs essentially nothing.
- **A single API process does not parallelize.** Requests in flight on one process run one after another, so
  parallel transpile needs a pool of processes. Against bbcore's parallel TS6 setup, 8 tsrs processes are about
  4.8× faster on this repo.
- The IPC cost per file was the main open risk in the plan. It does not cancel out the gain.

### Wire protocol (for the C# client)

- Spawn: `tsrs --api --async --cwd <dir> --useCaseSensitiveFileNames=<bool>`, with stdin/stdout piped and stderr
  inherited. `--async` selects JSON-RPC, using `vscode-jsonrpc` stream framing (`Content-Length` headers, the same
  as LSP). Without `--async` the protocol is the sync MessagePack channel.
- Requests: `initialize` (params `null`), then `transpileModule` with
  `{ input, options: { compilerOptions, fileName, reportDiagnostics } }`. The response is
  `{ outputText, sourceMapText?, diagnostics? }`.
- `compilerOptions` uses tsconfig names. Enums are **numbers**, with the same numbering as TS 6:
  `ModuleKind.CommonJS = 1`, `ScriptTarget.ES2022 = 9`, `JsxEmit.React = 2`. TS 7 drops ES3/ES5.
- Diagnostics carry `pos`, `end`, `code`, `category`, `fileName`, and zero-based `startPosition` / `endPosition`
  `{ line, character }`. They map directly onto bbcore's `Diagnostic`.
- tsrs and `typescript@7` serve the same protocol (`npm/sdk` in tsrs is copied from `packages/typescript`), so one
  C# client also works against tsgo's `tsc --api`.

## Findings that change the plan

1. **TypeScript 7 is now GA as the `typescript` package** (`latest` = 7.0.2, `beta` = 7.1.0-beta). It ships the
   native `tsc` binary and the same `unstable/sync|async` API, and it has **no JS `ts.transpileModule`**. The
   following updates go into `tsrs.md`:
   - The equality oracle is `typescript@<the version tsrs --version prints>`, not `@typescript/native-preview`.
   - Separate follow-up: today bbcore loads `typescript` from `devDependencies` into its JS engine (`bbtsc.js`). A
     project with `typescript@7` in `devDependencies` would likely break that path; this is inferred from the package contents and
     was not tested. The C# API client built for tsrs is
     also the fix for that, since it works against `typescript@7`'s `tsc --api`.
2. **One API process serializes requests.** The design must use a pool of `tsrs --api` processes; 8 is a good
   default. Pipelining requests on one process is not worth building.
3. **Destructured-export emit (TS6 → TS7).** Phase 2 must test the Njsast bundler on
   `({ a: exports.a } = …)` output. Use `programDraggableArea.tsx` as the regression case, and if export detection
   misses it, add the pattern to Njsast.
4. **TS 7.1 is stricter about a missing entry in `files` or `types`.** It reports only TS6053/TS2688 and skips
   type checking, where TS 7.0 checked anyway. bbcore's generated `tsconfig.json` puts
   `node_modules/monaco-editor/monaco.d.ts` in `files` for WebOnDemand and WebCjm. When that package is missing,
   tsrs mode would report one global error and nothing else. Phase 1 should either only add files that exist or
   surface this case clearly.

## What was not covered

- Windows and Linux: no tsrs Windows binary exists in 0.18. Everything above was measured on macOS arm64 only.
- An end-to-end `bb build` or interactive `bb` with tsrs, which needs Phase 1–2 code. This includes bundle equality
  and the Njsast destructured-export check.
- Source map equality against TS 6, since only tsrs vs TS7 was compared byte for byte.
- Typecheck with a complete WebMma install. The missing packages hide some real type errors, the same way for every
  compiler.

## Reproducing

The scripts are in [`tools/tsrs-spike/`](../../tools/tsrs-spike). Install their pinned compilers with `npm i` in
that directory; `node_modules`, outputs and overlays are gitignored.

```sh
cd tools/tsrs-spike && npm i
WEBMMA=../../../inspirecloud/WebMma ./check.sh WebOco 3            # one-shot typecheck: tsrs vs TS7 oracle vs tsgo on PATH
WEBMMA=../../../inspirecloud/WebMma ./check.sh WebOnDemand 3 "$PWD/overlays/WebOnDemand.json"
node watch.mjs node_modules/@tsrs/darwin-arm64/tsrs <WebMma>/apps/WebOnDemand "$PWD/overlays/WebOnDemand.json" tsrs
node transpile.mjs <WebMma> "$PWD/tdiff" apps api packages     # equality + throughput; samples of diffs go to tdiff/
node ts6pool.mjs <WebMma> 8 apps api packages                  # TS 6 worker-thread baseline
```

An overlay is a scratch `tsconfig` that `extends` an app's `tsconfig.json` (absolute path). It replaces `files`
with absolute paths, leaving out the missing `monaco.d.ts`, and gives `include` as absolute globs. WebAdministration
and WebCjm also need `"compilerOptions": { "types": [] }`. Example for `overlays/WebOnDemand.json`:

```json
{ "extends": "<WebMma>/apps/WebOnDemand/tsconfig.json",
  "files": ["<WebMma>/apps/WebOnDemand/index.ts", "<home>/.bbcore/tools/jasmine400.d.ts"],
  "include": ["<WebMma>/apps/WebOnDemand/src/**/*", "<WebMma>/apps/WebOnDemand/spec/**/*"] }
```
