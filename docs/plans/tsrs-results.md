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
