# Plan: tsrs (Rust TypeScript) mode for bbcore

Status: proposal, 2026-10-24. Phase 0 is done ([spike results](tsrs-spike.md)): go for all three capabilities.
Nothing else is implemented yet.

## Phase 0 outcome in brief

- Typecheck: on 5 WebMma apps the output is identical to TS `7.1.0-dev.20260930.4`, the build tsrs ports, in the
  same order. tsrs is 3.5–4.4× faster than that tsgo build and uses about 45% of its memory. WebOnDemand takes
  0.19 s against 0.79 s.
- Watch: banners are identical to tsc, so the `NativeTsCompiler` parsing carries over. A leaf edit rechecks in
  39 ms (tsgo: 108 ms); a global invalidation takes 249 ms (tsgo: 761 ms).
- Transpile through the API: 6,766 WebMma files, with JS and source maps byte-identical to TS 7.1's API.
  Compared with TS 6.0.3, only whitespace changes, plus redundant parens and **destructured `exports` emit**
  (a risk for the Njsast bundler). A pool of 8 `tsrs --api` processes runs about 4.8× faster than 8 parallel TS6
  engines.
- Corrections to this plan (applied below): the oracle is `typescript@7.x`, not `@typescript/native-preview`. One API
  process serializes requests, so a process pool is required. TS 7.1 skips checking entirely when a `files`/`types`
  entry is missing.

## Goal

Add an opt-in `.bbrc` mode that uses [tsrs](https://github.com/oxc-project/tsrs) (the Cloudflare/VoidZero Rust port
of TypeScript 7) for:

1. one-shot typechecking (`bb build`, `bb test` typecheck),
2. incremental typechecking with watching (interactive `bb`),
3. TypeScript → JavaScript transpilation of each module, done through the tsrs API, not the CLI.

It must give the same diagnostics and the same JavaScript as the paths it replaces, and be clearly faster. It is
validated on `../inspirecloud/WebMma`.

## What tsrs offers (as of 0.18.0, 2026-10-19)

- npm `tsrs` (version `0.18.0-ts7.1.0-dev.20260929`), bin `./bin/tsrs` (launcher). Native binaries are in optional deps
  `@tsrs/darwin-arm64` and `@tsrs/linux-{x64,arm64}-{gnu,musl}`. **There is no Windows or Intel macOS binary yet**; both
  are planned for the next release. `TSRS_BINARY=/path` overrides the binary.
- CLI is tsc/tsgo compatible: `-p`, `--noEmit`, `--pretty false`, `--watch` (Linux and macOS only; on Windows it exits
  with "not supported"), `--incremental`, `--version`. It also adds performance flags: `--checkers N`,
  `--singleThreaded`, `--checkerCostCache <file>`, `--maxMemory`.
- The goal is 100% conformance with TS7 (Go). Any difference from tsgo in diagnostics, their order, message text or
  exit code counts as a tsrs bug. Known exceptions involve thread-count determinism near instantiation-depth limits.
- `tsrs --api` serves the TypeScript 7 native API protocol (`tsrs/sync` uses MessagePack over stdio, `tsrs/async` uses
  JSON-RPC). It covers `transpileModule`, `transpileModuleFromFile`, snapshots with `getSemanticDiagnostics`,
  `updateSnapshot` and `emit`. Transpile requests accept raw compiler options.
- Vendor benchmark: up to 8× faster and about 70% less memory than tsgo 7.0. This depends on the workload.
- It is experimental. It was announced on 2026-10-20 and releases come every few days, so expect protocol and
  flag churn.

## Current state in bbcore

| concern | default path | existing fast path |
| --- | --- | --- |
| typecheck once | `TsCompiler` (JS TypeScript in JavaScriptEngineSwitcher, `bbtsc.ts`) | `NativeTsCompiler` runs `tsgo --project .bbcore-tsgo-typecheck.json --noEmit --pretty false` |
| typecheck watch | `TsCompiler.CreateProgram/TriggerUpdate` | `NativeTsCompiler` keeps `tsgo --watch` running and parses `Found N errors. Watching for file changes.` |
| transpile | `BuildModuleCtx` → `CompilerPool.GetTs(...).Transpile` → `ts.transpileModule` (CommonJS, sourceMap) | `future`/`BBMODE=Buildin`: Njsast `NjsastTsValidator.TranspileToCommonJsAst` |
| output parity | — | `validate`/`BBMODE=ValidateTS`: runs both transpilers, compares formatted JS, writes repro files |

Key files: `Lib/TSCompiler/NativeTsCompiler.cs`, `Lib/TSCompiler/ITSCompiler.cs`,
`Lib/Composition/BuildCtx.cs` (`CreateTypeChecker`, `DoTypeCheck`, `DetectNativeTypeScriptDirectory`, cache key via
`BuildCache.MapConfiguration`), `Lib/TSCompiler/BuildModuleCtx.cs` (~line 1225, transpile branch),
`Lib/TSCompiler/BobrilBuildOptions.cs` + `ProjectOptions.cs` (`gots`, `future`, `validate`),
`Lib/TSCompiler/NjsastTsValidator.cs`.

WebMma today: `.bbrc` sets `"bbVersion": "5.15.5", "tsVersion": "6.0.3", "gots": true`. tsgo comes from Homebrew
(`7.0.0-dev.20260519.1`). Apps live in `apps/*`. The largest are WebOnDemand (~1700 TS files), WebAdministration
(~950), WebCjm (~840) and WebOco (~620). Notable options: `experimentalDecorators`, `useDefineForClassFields: false`,
`noEmitHelpers`, `jsx: react` + `reactNamespace: b`, `target es2022`, `moduleResolution bundler`.

## Configuration

```json
{ "tsrs": true }
```

- `tsrs: true` turns on tsrs for typecheck (once and watch) and for transpile.
- `tsrsTranspile: false` is an escape hatch while tsrs is experimental: tsrs typechecks only, and transpile stays on
  the current path.
- The setting can be in a project or global `.bbrc`, or in the `bobril` section of `package.json`, the same way
  `gots` works (`BobrilBuildOptions.tsrs`/`tsrsTranspile`, merged in `Merge`, read in the bobril-section parser, and
  exposed as `ProjectOptions.Tsrs`/`TsrsTranspile`).
- Precedence: `tsrs` > `gots` > `devDependencies` detection of `@typescript/native-preview` > JS TypeScript.
  `validate` still forces the comparison path (see Phase 4). `future` (Njsast transpile) wins over
  `tsrsTranspile`. Log one line if both are set.
- Binary discovery, in order: `TSRS_BINARY` env, then a native binary in
  `node_modules/@tsrs/<os>-<arch>[-<libc>]/` found by walking up from the project like
  `TryFindNativeTypeScriptDirectory`, then `node_modules/tsrs/bin/tsrs`, then `tsrs` on `PATH`. Prefer the
  platform package's native binary over the launcher so each spawn avoids a Node startup.
- Unsupported platform (Windows today) or binary not found: fail with a clear message, the same as
  `gots` ("tsrs is enabled, but tsrs was not found …"). On Windows also suggest `gots`. Do not fall back silently, so a
  benchmark can't quietly measure the wrong compiler.

## Design

### 1. Shared native CLI typechecker

Refactor `NativeTsCompiler` into a base class for a native tsc-compatible CLI (`NativeCliTsCompiler`). The base is
parameterized by:

- executable resolution (`CreateStartInfo`),
- a display name for logs (`Go TypeScript` / `Rust TypeScript (tsrs)`),
- the overlay file name (`.bbcore-tsgo-typecheck.json` / `.bbcore-tsrs-typecheck.json`, so switching modes never
  leaves a stale overlay watched by the other process),
- extra arguments: for tsrs, `--checkerCostCache <.bbcore/tsrs-checker-cost>` and optionally `--checkers N` from
  `.bbrc` `tsrsCheckers`.

`TsgoCompiler` and `TsrsCompiler` are thin subclasses. The rest is reused unchanged: diagnostic regexes, watch-line
state machine, generation counting, `WaitForWatchCompilation` and the overlay writing.

Things to verify against real tsrs output (these are Phase 0 checks):

- the watch banner strings (`Starting compilation`, `Starting incremental compilation`,
  `Found N errors. Watching for file changes.`) match tsgo exactly,
- `--pretty false` diagnostic line format, including multi-line related information,
- `--version` output, which feeds `GetTSVersion` and the build-cache key.

`BuildCtx` changes:

- Replace `_nativeTypeScriptDirectory` with a small `NativeTypeCheckerKind { None, Tsgo, Tsrs }` plus a path.
- `CreateTypeChecker` and `ReleaseTypeChecker` switch on the kind.
- `UpdateTSConfigJson` runs for both native kinds.

Diagnostics ordering: tsrs promises tsgo order, but bbcore should still sort native diagnostics by
(file, line, col, code) before storing them. Then a compiler swap never reorders the reported output.

### 2. tsrs API client for transpilation (C#)

Add `Lib/TSCompiler/Tsrs/TsrsApiClient.cs`. It spawns `tsrs --api` and talks the TS7 native API protocol over stdio.
Use JSON-RPC: spawn with `--api --async --cwd <dir> --useCaseSensitiveFileNames=<bool>`, which gives LSP-style
`Content-Length` framing. Use Newtonsoft so no new dependency is needed. Phase 0 measured the sync MessagePack
channel at 0.43 ms/file and async JSON-RPC at 0.54 ms/file per process, a gap that disappears with a pool. The exact
request shapes are in [the spike results](tsrs-spike.md#wire-protocol-for-the-c-client). The same client works
against `typescript@7`'s `tsc --api`.

- Methods needed: `initialize`, `transpileModule(fileName, sourceText, compilerOptions)` → `outputText`,
  `sourceMapText`, `diagnostics`; `close`. Take the exact method names and params from tsrs
  `npm/sdk/METHODS.md` and `packages/typescript/src/api` at the pinned commit. Don't guess them.
- `TsrsTranspiler : ITSCompiler` implements only `Transpile`, `GetTSVersion` and `CompilerOptions`. Typecheck members
  throw `NotSupportedException`, the same as `NativeTsCompiler.Transpile` does today.
- Option mapping: `TSCompilerOptions` → raw TS7 compiler options. bbcore already forces `module: commonjs`,
  `sourceMap: true` and clears `moduleResolution: bundler` for transpile. Check every option bbcore can emit against
  TS7 removals (ES5 target, AMD/UMD/System, `baseUrl`, …), because TS7 rejects them with TS5102/TS5108. Map or drop
  those, and say so in the log.
- Diagnostics: convert API diagnostics (position + length) to bbcore `Diagnostic` lines and columns using the
  source text. Report them exactly as `bbTranspile` does today.
- Concurrency: `BuildModuleCtx` transpiles in parallel through `CompilerPool`. Pool `TsrsTranspiler` instances the
  same way: one long-lived `tsrs --api` process per pool slot, created lazily, reused across interactive
  rebuilds, and killed on `Dispose`. Phase 0 showed that a single process serializes requests: 8 in flight on one
  process ran no faster than 1. So the pool is required. Default to `min(8, cores)` processes; a pool of 8 did
  WebMma's 6,766 files in 0.65 s.
- Njsast: TS7 emits `export const { a, b } = f();` as `({ a: exports.a, b: exports.b } = f());`, where TS6 used a
  temporary followed by `exports.a = _a.a`. Verify that Njsast's CommonJS export detection handles this
  (regression case: `WebCjm/src/programs/list/programDraggableArea.tsx`) and extend it if not.
- Lifetime: on a dead process, fail the in-flight request with a clear diagnostic and respawn on the next request.
  Never hang the build. Every request has a timeout.
- Build cache: `ConfigurationBuildCacheId = MapConfiguration("tsrs-" + tsrsVersion, compilerOptionsJson)` so
  cached outputs from TS6 or Njsast are never reused for tsrs and the reverse. Log `Using tsrs version X (TypeScript Y)`
  instead of the TypeScript version.
- Logging: extend the existing `"{Built-in|TypeScript} transpile emit done in …"` line with `tsrs`.

### 3. Incremental typecheck via the API (stretch, after Phases 1–3)

The CLI `--watch` makes tsrs watch the file system a second time, alongside bbcore's own `DiskCache` watcher. bbcore
already knows the exact changed files. The API offers `createSnapshot` / `updateSnapshot(fileNotifications)` /
`getSemanticDiagnostics`. A `TsrsApiTypeChecker` could drive incremental checks from bbcore's change set. That
removes the overlay file, the watch banner parsing and the debounce wait (`WaitForWatchCompilation` 5 s window), and
it can share the API process with transpile.

Do this only if Phase 5 measurements show watch latency is dominated by tsrs's own file watching or debounce. Keep the
CLI watch as the default until then.

## Phases

### Phase 0: spike, no bbcore changes

- `npm i -D tsrs@0.18.0` in a scratch copy, or install globally, on macOS arm64.
- Run `tsrs -p <overlay> --noEmit --pretty false` and `tsgo` (same flags) on WebOnDemand and WebAppRoot. Diff the
  sorted diagnostics and record times.
- Run `tsrs --watch`. Capture the banners, touch one file, record the recheck latency.
- Write a ~50-line Node script using `tsrs/sync` `API` to call `transpileModule` on all WebOnDemand files with the
  options bbcore uses (dump them from `BuildModuleCtx`). Diff against TS 6.0.3 `ts.transpileModule` and against tsgo's
  `--api` `transpileModule`, which uses the same protocol. Record per-file latency.
- Capture the wire protocol (`initialize`, `transpileModule` request and response) to design the C# client.
- Output: a short findings note under `docs/plans/tsrs-spike.md`. Go/no-go for each capability.

### Phase 1: config + typecheck (once and watch)

- `BobrilBuildOptions`/`ProjectOptions` `tsrs`, `tsrsTranspile` (+ optional `tsrsCheckers`).
- Refactor `NativeTsCompiler` → base + `TsgoCompiler` / `TsrsCompiler`. Binary discovery as described above.
- `BuildCtx` wiring and precedence. Clear errors for a missing binary or an unsupported platform.
- Unit tests in `Lib.Test`: option parsing and merge, precedence, binary discovery (fake `IFsAbstraction`),
  diagnostic line parsing for tsrs samples, watch-line state machine fed with captured tsrs output.

### Phase 2: tsrs transpile

- `TsrsApiClient`, `TsrsTranspiler`, pooling in `CompilerPool`, `BuildModuleCtx` branch, cache key, logging.
- Tests: client framing against a recorded transcript, so no binary is needed. Option mapping. Diagnostic position
  conversion. One integration test that runs only when `TSRS_BINARY` or a discoverable tsrs exists and transpiles a
  few `TestProjects` files.

### Phase 3: docs

- README section next to "Enable faster native TypeScript typechecking": `.bbrc` `tsrs`, install
  (`npm i -D tsrs`), the platform limits, `tsrsTranspile: false`.
- `CHANGELOG.md` `[unreleased]` `### Added` entry (per AGENTS.md). If the refactor touches files linked into
  `Bbcore.Lib`, run `dotnet build Bbcore.Lib/Bbcore.Lib.csproj --no-restore`.

### Phase 4: output-equality tooling

The definition of "equal" is:

- **Typecheck:** the set of (file, line, col, code, message) from tsrs equals tsgo's, with **matching TS versions**.
  WebMma's Homebrew tsgo (`7.0.0-dev.20260519`) is older than tsrs's pin (`7.1.0-dev.20260929`). For the oracle,
  install `typescript@<the TS version tsrs --version prints>` (TS 7 now ships as the `typescript` package, with a
  native `tsc`), or real diffs will drown in version noise. Against the TS6 JS checker, expect only documented TS7 differences.
- **Transpile:** byte-identical to tsgo `--api` `transpileModule` for the same options. Against the current TS 6.0.3
  output, identical after the existing Njsast `FormatJavaScript` normalization. Allowed known TS6→TS7 differences go
  in an explicit list.
- **Bundle:** `bb build` output (`dist/` bundle.js + sourcemaps) for each WebMma app with `tsrs: true` equals the
  baseline (`gots: true`, TS 6.0.3 transpile) after normalization. Any remaining diff must be traced to a single module.

Tooling:

- Extend `validate` so it can take a compiler name, e.g. `.bbrc "validate": "tsrs"` or `BBMODE=ValidateTsrs`.
  Transpile with both the current TS path and tsrs, then compare formatted JS. Repro files are written exactly as
  `NjsastTsValidator.WriteRegressionTestCase` does today. Generalize that class into a small
  `TranspileValidator` with pluggable "candidate" transpilers (Njsast, tsrs).
- `tools/compare-typecheck.sh <projectDir>`: runs tsgo and tsrs on the same overlay and diffs the sorted diagnostics.

### Phase 5: WebMma validation and benchmarks

Setup:

- Build bbcore Release locally. Run that build directly in each app directory, e.g.
  `dotnet <bbcore>/bb/bin/Release/<tfm>/bb.dll build`. First confirm the local build does not hand off to the pinned
  `bbVersion` 5.15.5.
- Toggle `tsrs` in a **global** `.bbrc`, or uncommitted local edits, so the WebMma repo isn't changed.
- `npm i -D tsrs` only in a throwaway checkout or worktree of inspirecloud. Never commit to WebMma as part of this work.

Apps: WebAppRoot (small), WebOco (medium), WebOnDemand (largest), plus one run over all apps for crashes or rejected
options.

Measure each configuration (baseline JS-TS, `gots`, `tsrs` typecheck only, `tsrs` full) with `hyperfine`, 5+ runs
after a warm-up, on a clean build cache (`.bbcore` cache cleared) and on a warm one:

| metric | source |
| --- | --- |
| total `bb build` wall time | hyperfine |
| typecheck time | existing `… typecheck found N errors in Xs` log line |
| transpile emit time | existing `transpile emit done in Xs for N files` log line |
| watch: time from save to diagnostics, one file edit and one widely imported file edit | interactive `bb`, timestamped logs |
| peak RSS of bbcore + compiler processes | `/usr/bin/time -l` |

Correctness gates (all on WebMma):

- typecheck diagnostic sets equal tsgo (matching versions) on every app,
- `validate: "tsrs"` reports zero transpile differences, or only listed TS7 differences,
- bundles equal baseline after normalization,
- `bb test` passes with `tsrs: true` in at least WebAppRoot and WebOnDemand.

Record results in `docs/plans/tsrs-results.md` with tsrs, tsgo and bbcore versions and machine details.

## Risks and open questions

- **Maturity:** tsrs was announced 4 days ago and is pre-1.0. Keep the mode opt-in and off by default. Pin a tested
  minimum version and log a warning outside the tested range.
- **Windows:** no binary or watch support today. bbcore users on Windows can't use this mode until tsrs ships it.
  The README must say so.
- **API protocol churn:** the client talks a beta protocol (TS7 "source-58 beta names"). Keep the C# client tiny and
  cover it with a recorded transcript test so upgrades fail loudly.
- **Is transpile actually faster?** Resolved in Phase 0. Spawning a process takes about 7 ms, and a pool of 8 is
  about 4.8× faster than 8 warm TS6 engines.
- **TS 7.1 skips checking when a `files`/`types` entry is missing.** It reports only TS6053/TS2688; TS 7.0 checked
  anyway. bbcore's generated `tsconfig.json` lists `node_modules/monaco-editor/monaco.d.ts` for some WebMma apps.
  Phase 1 should leave out files that don't exist, or surface this case clearly.
- **`typescript@7` in `devDependencies`** (TS 7 is GA, with no JS `transpileModule`) likely breaks bbcore's JS-engine path (inferred from the package contents, not tested).
  This is a separate follow-up, but the tsrs API client can be reused for it.
- **TS6 → TS7 emit differences:** "equal to the current output" can't be byte-exact while the current transpiler is
  TS 6.0.3. tsgo `--api` is the byte-exact oracle, and TS6 is a normalized semantic check.
- **Double file watching** with CLI `--watch` on large monorepos (kqueue fallback uses one descriptor per file). This
  is the reason for Design §3 above.

## Sources

- tsrs repository and docs: https://github.com/oxc-project/tsrs (`docs/USAGE.md`, `docs/COMPATIBILITY.md`,
  `docs/NODE_API.md`, `docs/EMIT.md`)
- Announcement: https://blog.cloudflare.com/typescript-rust-port/
- Overview: https://flaviocopes.com/tsrs/
