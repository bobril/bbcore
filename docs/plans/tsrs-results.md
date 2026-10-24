# tsrs mode: implementation and WebMma results

Date: 2026-10-24. Plan: [tsrs.md](tsrs.md). Spike: [tsrs-spike.md](tsrs-spike.md). Changes are on branch
`bobris/parallel-variable-hoisting-fix` and not yet committed.

## Outcome

With `.bbrc` `"tsrs": true` on all 15 WebMma apps, compared with WebMma's current setup (`gots: true` with tsgo
7.0 for typechecking, and the TS 6.0.3 JavaScript compiler for transpile):

- **Transpile:** 0 of all transpiled files differ under `validate: "tsrs"`, in all 15 apps. That is 23,425 files,
  counting shared files once per app.
- **Bundles:** byte-identical for every app that builds (6 of 15). The other 9 fail the same way in both modes
  because packages are missing from WebMma's `node_modules` (see "Environment").
- **Typecheck:** identical diagnostics in all 15 apps.
- **Speed and memory:** builds are 1.1–2.0× faster, transpilation 2.2–3.0× faster and typechecking about 4.5× faster
  (see the table below). Peak memory is 20–40% lower.

## Re-verification after WebMma environment update (same day, inspirecloud `334bccec532`)

After the WebMma environment was refreshed, inspirecloud's root `package.json` added `@typescript/native-preview`
`7.0.0-dev.20260525.1`. bbcore now picks it up as the `gots` baseline in place of Homebrew tsgo. Some packages are
still missing from `WebMma/node_modules`: `monaco-editor`, `bbseeker-extended`, `bobwai--l-separate-sections`,
`bobwai--carousel` and a few more. So only WebDeviceLogin and WebMain build without errors now. WebAppRoot fails in
both modes on missing `bobwai--*` packages.

Results with `tools/tsrs-spike/webmma-compare.sh` on all 15 apps:

- **Transpile:** 0 of 23,366 transpiled files differ.
- **Typecheck:** identical diagnostics in all 15 apps.
- **Bundles:** byte-identical `.js` for WebDeviceLogin and WebMain.
- **Remaining difference, source maps:** TS 7 emits finer source map segments than TS 6. tsrs matches TS 7 exactly.
  With `-g yes`, WebMain's `a.js` is identical, but `a.js.map` has 222,029 segments instead of 225,321 (same
  sources). bbcore positions its own build diagnostics (such as `Missing import`, code -3) through this map. About
  20% of those differ in **column only**, never in line or message. For example `pageMainContent.ts(10,39)` becomes
  `(10,45)`; the import string itself starts at column 27 (1-based). Matching TS 6's segment layout is not practical, so this
  stays a known difference.

Median of 3 runs on the new environment:

| app (transpiled files) | build wall | peak RSS | transpile emit | typecheck (bbcore log) |
| --- | --- | --- | --- | --- |
| WebMain (1194) | 10.8 → **9.2 s** | 633 → **488 MB** | 3.80 → **1.63 s** | 0.41 → **0.09 s** |
| WebDeviceLogin (1075) | 10.2 → **9.0 s** | 596 → **472 MB** | 3.66 → **1.65 s** | 0.40 → **0.10 s** |
| WebOnDemand (2953) | 13.6 → **6.2 s** | 639 → **378 MB** | 10.80 → **3.44 s** | 0.88 → **0.19 s** |

Notes from the re-run:

- While `node_modules` was apparently still changing, the tsgo baseline was briefly unstable: WebMessenger gave 305,
  then 93, then 53 errors, all extras being "Cannot find module" for installed packages. One tsgo `-t only` run hung
  at 0% CPU. Afterwards tsgo, tsrs and the TS 7.1 oracle all gave 53, stable over 12 bb runs and 8 direct runs each.
  tsrs never varied.
- The hang wasn't reproducible in 10 more runs. While looking into it, `NativeTsCompiler.RunTypeScript` was fixed to
  read stderr concurrently with stdout, because a full stderr pipe could deadlock it. `webmma-compare.sh` now limits
  each bb run to 300 s (`LIMIT`) and reports `TIMEOUT`.

## TypeScript 7.1+ native mode and fallback (for Windows)

`.bbrc` `tsVersion` 7.1+ (or a project-local `typescript` 7.1+) installs `@typescript/typescript-<platform>` and uses
its `tsc` the way tsrs is used: for typechecking (one-shot and `--watch`) and for transpilation through
`tsc --api` `transpileModule`, which speaks the same protocol, with the same output patcher. Where tsrs isn't
available, bbcore now falls back to this mode at `7.1.0-dev.20260930.4`, the build tsrs 0.18 ports, and only after
that to tsgo or regular TypeScript. TypeScript 7.0 is rejected, because its API doesn't answer `initialize`.

Verified on macOS arm64 by forcing the Windows path (an unpublished `tsrsVersion` and no tsrs binary), so every app
ran with the auto-installed TS 7.1 `tsc`:

- **Transpile:** 0 of 23,444 files differ (`validate` compared against `tsc`).
- **Typecheck:** identical diagnostics in all 15 apps.
- **Bundles:** byte-identical for all 5 apps that build (WebAppRoot, WebAppRootCls, WebDeviceLogin, WebMain,
  WebMessengerUnsubscribe).
- **Speed:** transpile is 1.4–2.1× faster than TS 6 (WebOnDemand 9.9 → 7.2 s, WebAdministration 7.9 → 3.7 s), but
  slower than tsrs. Typecheck is about the same as tsgo. Peak memory is lower.
- **Interactive mode:** `tsc --watch` reports and clears a type error; no `tsc` processes remain after `bb` exits.

Not tested on Windows itself. The code paths are platform-neutral: `tsc.exe`, `--useCaseSensitiveFileNames=false`,
no Unix file modes, signal registration tolerant of unsupported signals. TypeScript 7's own `--watch` behavior on
Windows is the main thing to check there.

## Parallel transpilation

The dependency crawl in `BuildModuleCtx` is sequential. Now, before each crawl step it starts background transpiles
for files already queued in `ToCheck`, up to 2× the native pool size (`min(8, cores)` processes). Only TypeScript
files that changed since the last crawl and aren't in the build cache are prefetched. `Transpile` takes the finished
result when the source still matches, and otherwise transpiles inline as before. Only the pure transpile call
(native API plus `NativeOutputPatcher`) runs in parallel; everything touching build state stays on the crawl thread.

A/B on WebMma, sequential vs parallel binary, median of 3 runs, `bb build -n --nocache -t no`. "Transpile wait" is
time the crawl spent waiting for transpile results (the `transpile emit done` log line):

| app (files) | mode | build wall | transpile wait |
| --- | --- | --- | --- |
| WebAppRoot (1449) | tsrs | 11.8 → **11.1 s** | 2.00 → **0.26 s** |
| WebAppRoot | tsc | 12.8 → **10.6 s** | 2.63 → **0.24 s** |
| WebMain (1194) | tsrs | 9.6 → **9.1 s** | 1.64 → **0.17 s** |
| WebMain | tsc | 10.4 → **9.4 s** | 2.05 → **0.19 s** |
| WebOnDemand (2953) | tsrs | 6.2 → **5.0 s** | 3.42 → **0.70 s** |
| WebOnDemand | tsc | 9.8 → **7.2 s** | 7.15 → **3.33 s** |
| WebAdministration (2202) | tsrs | 5.2 → **3.7 s** | 2.84 → **0.26 s** |
| WebAdministration | tsc | 6.5 → **3.7 s** | 3.70 → **0.27 s** |

- **Identical output:** bundles are byte-identical between sequential and parallel for every app that builds
  (WebAppRoot, WebMain), in both modes. Build diagnostics are identical in all four apps.
- **Compared with the original TS 6 setup:** WebOnDemand builds in 13.6 → 5.0 s with tsrs and 7.2 s with TS 7
  `tsc`. WebAdministration builds in 10.1 → 3.7 s in either mode.
- **Interactive mode:** incremental rebuilds after an edit go through the same prefetch; checked on the scratch
  project.
- **Remaining wait, WebOnDemand with `tsc`:** about 3.3 s, apparently files discovered late in the crawl, where the
  look-ahead window is small. Prefetching the whole known source list up front could reduce that further.

## What was built

| area | files |
| --- | --- |
| `.bbrc` `tsrs`, `tsrsTranspile`, `validate: "tsrs"` | `BobrilBuildOptions.cs`, `ProjectOptions.cs` |
| tsrs binary discovery (`TSRS_BINARY`, `node_modules/@tsrs/<platform>`, `node_modules/tsrs`, `PATH`) | `TsrsLocator.cs` |
| typecheck once and in watch mode, sharing the tsgo code path | `NativeTsCompiler.cs` (`NativeTypeScriptKind`), `BuildCtx.cs` |
| JSON-RPC client for `tsrs --api --async` | `TsrsApiClient.cs` |
| `transpileModule` transpiler, option and enum mapping, diagnostics | `TsrsTranspiler.cs` |
| bbcore's TypeScript emit patches re-applied to tsrs output, with source map composition | `TsrsOutputPatcher.cs` |
| tsrs transpiler pool, transpile branch, build cache key `tsrs-<version>`, `validate: "tsrs"` | `CompilerPool.cs`, `BuildModuleCtx.cs`, `BuildCtx.cs`, `NjsastTsValidator.cs` |
| tests | `Lib.Test/TsrsTests.cs` (17), `Njsast.Test/Input/Bundler/DestructuredExportsTs7` |
| docs | `README.md`, `CHANGELOG.md` |
| WebMma comparison tool | `tools/tsrs-spike/webmma-compare.sh` |

Test suites: Lib.Test 110/110 (run with `TSRS_BINARY` set, so the real tsrs integration test ran), Njsast.Test
1536/1536, Bbcore.Lib.Test 10/10. `Bbcore.Lib` builds.

## Findings that shaped the implementation

1. **bbcore patches TypeScript emit, and tsrs can't be patched.** `ToolsDir.TypeScriptJsContent` rewrites the
   TypeScript compiler source. tsrs output without those patches lost most translations: locale bundles shrank from
   99 KB to 4.7 KB, because bobril-g11n `t()` detection doesn't recognize `(0, g11n_1.t)(...)`. It also differed in 550
   of 1,449 WebAppRoot files. `TsrsOutputPatcher` applies the same changes to tsrs output: it unwraps the indirect calls
   TypeScript generated, removes `__esModule` tagging, adds setters to reexports, chunks export initialization, and
   emits destructured exports the TS6 way with `_a` temporaries. Each change is a text edit through Njsast
   `SourceReplacer` + `SourceMapBuilder`, so the source map stays exact. Indirect calls that already exist in the
   input source, such as `(0, eval)(...)` or prebuilt JavaScript like prettier, are left alone.
2. **Transpiling one file at a time is sequential in bbcore.** `CrawlFile` transpiles one file after another, so a
   build uses one `tsrs --api` process. The pool only helps `validate: "tsrs"` and parallel sub-builds. Parallel
   prefetching of transpiles is the next speedup (see follow-ups).
3. **TS 7.1 stops type checking when a root file is missing.** It reports only TS6053, while TS 7.0 checks anyway.
   WebOnDemand and WebCjm list `node_modules/monaco-editor/monaco.d.ts`, which isn't installed. The native typecheck
   project now leaves out files that don't exist, which matches what TS 7.0 did. tsgo results are unchanged.
4. **`tsrs --watch` doesn't exit when bb does.** A killed `bb` interactive session left it running; Homebrew tsgo
   exits on its own. Watch processes are now killed on exit and on SIGTERM, SIGINT and SIGHUP. Verified for SIGTERM
   and SIGHUP.
5. **Njsast bugs found on the way:**
   - Variable hoisting threw `NotImplementedException` on destructuring-assignment targets. bbcore's own
     `FastDefault` compress options don't hit it, but `CompressOptions.Default` does.
   - `SourceMapBuilder`'s plain-text adder jumped to the end of the line when seeking a column.
   Both are fixed, with tests.

## WebMma results

All runs: macOS arm64 (M2 Max, 12 cores, 64 GB), local Release `bb.dll`, `bb build -n --nocache`. `-t no` measures
build and bundle and `-t only` measures typecheck. Baseline is WebMma's `.bbrc` (`gots: true`, `tsVersion: 6.0.3`).
tsrs mode adds `tsrs: true`. tsrs is `0.18.0-ts7.1.0-dev.20260929`.

### Correctness, all apps (final code)

| app | transpile differences | bundles | typecheck diagnostics |
| --- | ---: | --- | --- |
| WebAdministration | 0 | not built (errors in both) | identical |
| WebAppRoot | 0 | identical | identical |
| WebAppRootCls | 0 | identical | identical |
| WebCjm | 0 | not built (errors in both) | identical |
| WebCjmExportPdf | 0 | not built (errors in both) | identical |
| WebDeviceLogin | 0 | identical | identical |
| WebEnvironments | 0 | not built (errors in both) | identical |
| WebMain | 0 | identical | identical |
| WebMessenger | 0 | not built (errors in both) | identical |
| WebMessengerUnsubscribe | 0 | identical | identical |
| WebMobileBackend | 0 | not built (errors in both) | identical |
| WebMySettings | 0 | not built (errors in both) | identical |
| WebOco | 0 | not built (errors in both) | identical |
| WebOmniChannel | 0 | not built (errors in both) | identical |
| WebOnDemand | 0 | not built (errors in both) | identical |

### Speed and memory (median of 3 runs)

| app (transpiled files) | build wall | peak RSS | transpile emit | typecheck (bbcore log) | `-t only` wall |
| --- | --- | --- | --- | --- | --- |
| WebAppRoot (1449) | 13.8 → **11.8 s** | 757 → **590 MB** | 4.72 → **2.08 s** | 0.49 → **0.11 s** | 0.98 → **0.39 s** |
| WebMain (1194) | 11.1 → **9.9 s** | 619 → **483 MB** | 3.79 → **1.70 s** | 0.40 → **0.10 s** | 0.88 → **0.39 s** |
| WebOnDemand (2942) | 13.1 → **6.4 s** | 647 → **393 MB** | 10.38 → **3.50 s** | 0.82 → **0.18 s** | 1.78 → **0.96 s** |

The transpile emit time covers the tsrs round trip plus the Njsast parse in `TsrsOutputPatcher`. Without the
patcher it was 1.16 s on WebAppRoot. Build wall time for apps that bundle is dominated by bundling, compression and
sprites, which this change doesn't touch.

### Interactive mode

Checked on a scratch project with `bb y`. The initial build runs typecheck and transpile through tsrs. A file edit
that adds a type error is reported (`TS2322`) and clears on revert, with tsrs reporting about 0.00–0.01 s of check
time. The spike measured watch rechecks on WebOnDemand at 39 ms for a leaf edit and 249 ms for a global edit. Killing
`bb` leaves no tsrs process behind.

## Environment caveats

- WebMma's `node_modules` lacks `monaco-editor`, `bbseeker-extended`, and the `jquery` / `external--redactor-ii`
  types. That causes the build errors in 9 apps, the same way in both modes, so those apps have no bundles to
  compare.
- Measured on macOS arm64 only. tsrs 0.18 has no Windows or Intel macOS build. Linux was not tested.
- Watch latency inside a real WebMma app was not measured, because that would mean editing WebMma sources.

## Reproducing

```sh
dotnet build bb/bb.csproj -c Release
cd tools/tsrs-spike && npm i
WEBMMA=<path>/WebMma BBRC_DIR=<a directory above WebMma, outside any repository> ./webmma-compare.sh 3 WebAppRoot
```

The script turns on tsrs through a temporary `.bbrc` in `BBRC_DIR` and removes it afterwards. It writes bundles to
`tools/tsrs-spike/out`, restores the app's `tsconfig.json`, and moves `validate` repro files out of the app.

## Follow-ups

- **Parallel transpile prefetch:** transpile known sources ahead of the sequential crawl using the tsrs pool.
  Transpile time is now about 1.2 ms per file, with the bulk being the round trip and the patcher parse.
- **Reuse the patcher's AST:** pass the parsed AST on as `TranspileResult.Ast` when no edits were needed, which
  saves the second parse in `BuildModuleCtx`.
- **`typescript@7` in `devDependencies`:** bbcore's JS-engine path can't load TS 7, which has no `typescript.js`.
  The same API client works against `tsc --api`.
- **Upstream:** ask tsrs for an option to emit without indirect-call wrappers. That would remove the biggest part of
  `TsrsOutputPatcher`.
