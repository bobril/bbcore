# Changelog

## [unreleased]

### Fixed

Async generators were not allowed in parser.

## 2.8.1

## 2.8.0

### Added

- bundle2.js in fast bundler mode could be loaded as module and still works.

### Changed

- Default TypeScript version is 4.9.5

## 2.7.0

### Added

Expose loader `R` to `globalThis`.

## 2.6.1

### Changed

- Default html head content is empty and not obsolete forcing IE to edge mode.

### Fixed

- Bundling corrupting code with overwriting import with saving original value.

## 2.6.0

### Changed

- Default TypeScript version is 4.7.4

### Fixed

- Typescript options enums to be in line with TypeScript 4.7

### Added

Localization update creates `locations.json` with original source code positions. Format is array of arrays of message, hint, withParams?1:0, ...sourcepath:line:column (line and column are one based, path is relative to project root).

```json
["Hello World! {c, number} {test}", null, 1, "src/app.ts:51:35"]
```

## 2.5.5

### Fixed

Compatibility with TypeScript 4.7. Imports could be jasmine.spy`ed again.

## 2.5.4

### Fixed

One more time fixed `test` command with combining `--out` and `--dir` options.

## 2.5.3

### Fixed

Minification of `()=>variable` didn't marked variable as read which could make wrong code generated on other places.

## 2.5.2

### Fixed

Running tests with code coverage in headless Chrome hangs. Now fixed and optimized also in Jasmine 3.3.

Printing class calculated method in brckets.

## 2.5.1

### Fixed

Running tests with code coverage in headless Chrome hangs.

## 2.5.0

### Fixed

Js assets are again at start before IIFE and are not touched in bundling (no renames).

## 2.4.2

### Fixed

Interactive mode with enabled spriting after change, all sprites shown same icon.

Extremely slow bundling of sources without source maps for example scss files.

## 2.4.1

### Fixed

Two bundler bugs.

## 2.4.0

### Added

Load assets also from `.bbrc` file not just `package.json`.

## 2.3.2

### Fixed

Arrow with await `async v => await v`.

## 2.3.1

### Fixed

Compression bug related to destructuring.

## 2.3.0

### Fixed

Preserve Tests results even after test client is disconnected. New test run will clear all these old results.

Scss import imports failed.

JS Relative import of path failed.

## 2.2.3

### Fixed

Dead code elimination didn't eliminated expansion creating invalid code.

## 2.2.2

### Fixed

Bundling of template string containing `\$`

## 2.2.1

### Fixed

- bundler failed on `class { get [x](){} }`

## 2.2.0

### Changed

- Default TypeScript version is 4.6.2

### Added

Allow to use Template strings inside bobril-g11n.t when targeting ES2015.

## 2.1.4

### Fixed

Parsing and printing `{ [key]: value }`.

## 2.1.3

### Fixed

Bundling bug.

Jasmine 4.0.0 was upgraded in place to 4.0.1. Also fixing problem with unhandled promise rejection and now showing available stack in console.

## 2.1.2

### Fixed

Regression in 2.1.1 with bb/test.

Coverage works for arrow functions with just expression.

## 2.1.1

### Fixed

Headless Chrome on Windows is restarted after each test run to workaround hang of tests.

`global` is not anymore defined in TypeScript and ESM JS files (it allows to declare it as let or const under same name). Just use `globalThis` instead if you have to.

Fixed parsing of expansion in object spread in argument of arrow function.

## 2.1.0

### Added

- Jasmine 4.0.0: "bobril": { "jasmineVersion" : "4.0" }

## 2.0.0

### New defaults

Target and TS Libs are ES2019. ES5 target is still supported just not default anymore.

### Removed

Old Bundler in JS. Command line option -x to choose bundler. New Njsast based bundler is now only options.

## 1.64.0

### Changed

- Default TypeScript version is 4.5.5

### Updated

- TypeScript Targets enum to include up to ES2021.
- Njsast with fix of Array Hole in arguments

## 1.63.1

### Updated

Njsast with bundling fix.

## 1.63.0

### Added

Library Bundling mode. (Define/Enable in .bbrc)

### Updated

Njsast with fixes and features.

## 1.62.1

### Fixed

Fixed problem with styleDef containing sequence (due to downleveling to ES5).

## 1.62.0

### Updated

Njsast with fixes and optimizations.

## 1.61.0

### Added

- Support for osx.arm64
- New command gen-tsconfig

## 1.60.1

### Fixed

- Default target must be still ES5

## 1.60.0

### Added

- Support for ES2015+ (WIP)
- Add build errors to JUnit xml

## 1.59.0

### Changed

- Default TypeScript version is 4.5.2

### Fixed

Typescript extractor does not fail with exception.

## 1.58.0

### Added

New `js sourcemap` command. That will create html visualization of source map. Red background means "no source".

```
bb js sourcemap dist/a.js
```

### Fixed

Build sourcemaps now has much less "[no source]" parts.

## 1.57.0

### Changed

- Default TypeScript version is 4.4.4

### Fixed

Fixed .Net 6.0 regression in Deflate stream behavior.

## 1.56.0

### Added

`yarn install` now always adds `--ignore-optional` parameter.

Upgraded to .Net 6.0. Updated all dependencies.

## 1.55.1

### Fixed

Incompatibility with TS 4.4.3 also made output smaller again like it was in TS 4.3 or previous.

## 1.55.0

### Added

Updated all 3 webs embedded in bb itself.

### Fixed

Bundling of latest Monaco works again.

## 1.54.0

### Added

- support for `useUnknownInCatchVariables` tsconfig.json property.

## 1.53.0

### Changed

- Default TypeScript version is 4.4.3

## 1.52.0

### Changed

- Nodejs in docker file updated from v10 to v14.

## 1.51.0

### Added

New `test` command option `-w` or `--printfailed` to see all failed tests with stacks also in stdout.

## 1.50.0

### Changed

- Default TypeScript version is 4.3.5

## 1.49.3

### Fixed

- Various fixes around mdxb support.

## 1.49.2

## 1.49.1

### Fixed

- Crash in Build mode for all mdbx in directory.

## 1.49.0

### Added

- Improved mdbx support. Demo with import all mdbx in directory.

- mdxb special code language `tsxinline` which will additionally inline code after imports.

- mdxb special code language `inline` which will just inline code after imports and not show it.

## 1.48.1

### Fixed

- feature from 1.48.0 now should work in interactive mode in browser.

## 1.48.0

### Added

- support to resolve modules on relative paths. Making resolver much more conformant to nodejs resolver.

- import all mdxb in directory by `import mdxbs from "./blog/.mdxb"`

## 1.47.0

### Fixed

- Patched TypeScript for another strange crash

### Added

- Mdxb support for autoupdating code blocks from another source code. Optionally add just line and column using colon. This `from:` directive must be last on line.

````md
```tsx from:app.tsx
// this will be updated from app.tsx file during compilation
```

```tsx from:app.tsx:1:5
// only first line from app.tsx and only from 5th column will be automaticaly updated here
```
````

## 1.46.1

### Fixed

- `sprites.ts` missed svg files.
- Mdx codeblock does not have last empty line.

## 1.46.0

### Changed

- Default TypeScript version is 4.2.4

### Added

- Updated dependencies
- `bb test` with both options `--dir` and `--out` now run also tests. Only `--dir` command without `--out` will just write build output and not run tests.

## 1.45.1

### Fixed

- Fast build failing on module with extension in name.

## 1.45.0

### Added

- Mdxb support

## 1.44.1

### Fixed

- parent `.bbrc` where not loaded correctly

## 1.44.0

### Added

- Configuration is now also loaded from `.bbrc` files. They have exactly same content as `bobril` section in `package.json`. Options defined in parent directories `.bbrc` files are overwritten by current one, allowing sharing common options for multiple projects.

## 1.43.3

### Fixed

- "en" and "en-us" localizations missing regression from 1.43.2

## 1.43.2

### Fixed

- Number delimiters were wrong in generated localization js files.

## 1.43.1

### Fixed

- Added missing new helper \_\_spreadArray from TS 4.2.
- Fixed detection of change of assets to correctly rebuild in interactive mode.

## 1.43.0

### Changed

- Default TypeScript version is 4.2.2
- Added new tsconfig options.

## 1.42.0

### Added

- Support for svg sprites for Bobril 16

## 1.41.1

### Fixed

- Patch workround for https://github.com/microsoft/TypeScript/issues/40747
- Bundler with special import cycle

## 1.41.0

### Added

- Interactive mode can write all build files to distributed directory by defining in `package.json` in `bobril` section `"interactiveDumpsToDist": true`. Useful for using Bobril with Cordova.

## 1.40.1

### Fixed

- Bundling of reexport of export star.

## 1.40.0

### Added

- Removal of unused self recursive functions

## 1.39.0

### Added

- Detection of `/*#__PURE__*/` comment as pure function.

### Fixed

- Bundler exception in special case.

## 1.38.1

### Fixed

- Compression of Enums with Whole Export.
- Made own Linux ClearScript.V8 build which is compatible with Debian 10.
- Bundler with import default when there is not default export

## 1.38.0

### Added

- New build parameter to control if run type checking (default `-t yes`) or not (`-t no`) or only type check (`-t only`).

### Fixed

- Optimized extraction of sprite quality from filename

## 1.37.1

### Fixed

- Css Processor for V8

## 1.37.0

### Added

- Upgrade to .Net 5.0
- Chakra => V8
- Enabled assembly trimming

## 1.36.2

### Fixed

- `IncludeSources` from package.json are now considered as used modules.

## 1.36.1

### Fixed

- another bug with symlinked modules

## 1.36.0

### Added

- node_module symlinked modules should be resolved to original paths - it dedupes such files

- 64bit version of Chrome is now also found for headless testing.

## 1.35.0

### Added

- Bundler now optimize TypeScript enums.

## 1.34.0

### Added

- new build-in `file` which allows to define global variables containing content of file.

## 1.33.0

### Added

- `bb t -u` command now supports any number of input files to merge. (Contributed by krewi1)

- Updated dependencies like Chakra.

- Support for `noUncheckedIndexedAccess`

### Changed

- Default TypeScript version is 4.1.2

## 1.32.2

### Fixed

Correctly handling `\n` in translations imports.

## 1.32.1

### Fixed

Bundler crash in variable rename in special case.

Wrongly not removing `var directly_unused = import.namespace`;

## 1.32.0

### Added

- updated to lastest Njsast - removes unused classes.

## 1.31.0

### Added

- define `global` as `window` in bundler even for common modules

### Fixed

- crash in special case

## 1.30.3

### Fixed

- another pattern for reexport `import { X as Y } from "Z"; export { Y };` works in bundler.
- fixed regression with patterns like `var x = ImportNamespace.Enum.Option`

## 1.30.2

### Fixed

- auto patching TypeScript to force it to use `__createBinding` helper for `export { x } from "y"` pattern. That's allow mocking of it, also it should improve release bundling.

## 1.30.1

### Fixed

- createBinding is not just getter but also setter, allows to mock imports from export star.
- constant evaluation works with TS 4.0 commonjs patterns.

## 1.30.0

### Added

- consider also imports from "examples" and "tests" directories as allowed from dev.
- proper patch for TS 3.9 and TS 4.0 bug https://github.com/microsoft/TypeScript/issues/38691 - it is really needed for some code.

## 1.29.0

### Changed

- Default TypeScript version is 4.0.3

### Added

- Workaround for TS 3.9 and TS 4.0 bug https://github.com/microsoft/TypeScript/issues/38691

### Fixed

- exportStar helper now binds exports making them mockable.

## 1.28.2

### Fixed

- tsconfig.json Newline option is named correctly `lf`.

## 1.28.1

### Fixed

- Warning -12 and -13 for @ scoped modules and detecting special named directories ("example", "test", "spec") in any parents directories as well.

## 1.28.0

### Added

- Support for export { x } from "y"; and export { x as y } from "z"; patterns in bundler.
- Updated build-in tslib.

### Fixed

- Regression in bundler from 1.27 when module exporting just types and then some module reexports some type from it.

## 1.27.1

### Fixed

- Compress Optimization so it shortens output code even more
- Regression in 1.27.0 with export of TypeScript Interfaces by name and with using export recursively

## 1.27.0

### Added

- Improved bundling and compress optimizations (by updating to latest Njsast)

## 1.26.4

### Fixed

- Warning -12 is not shown for dev dependencies imported from files in directories named "example", "test", "spec".

## 1.26.3

### Fixed

- Warning -12 is not shown for dev dependencies imported from examples or tests.

## 1.26.2

### Fixed

- Warning -13 should behave correctly in interactive mode

## 1.26.1

### Fixed

- Bundling of `export * as x from "y";`

## 1.26.0

### Added

- Warnings about dependencies (-12 and -13) should work again. In case you have Warnings as Errors it could be breaking change.

## 1.25.0

### Added

- bb test command now detects incomplete tests (no tests or some forgotten fit or fdescribe) and exits with error code 1
- Upgraded all dependencies

## 1.24.4

### Fixed

- package.json - bobril - assets correctly find directory starting with "node_modules/" even when "node_modules" are not in project root dir.

## 1.24.3

### Fixed

- Forgotten recalculation of variables and scopes after fixing these 2 patterns in previous version.
- Wrong ordering of JS dependencies when b.asset links mixed relative and node_modules dependencies (happen only first time without cache).

## 1.24.2

### Fixed

- Bundling of JS dependency with `var x = (function(){ window.x = window.x || {}; ...; return x; })();` pattern.
- Bundling of JS dependency with `if (typeof module !== "undefined" && module.exports) {` pattern.
- Made autogenerated assets source files deterministic and identical on all platforms by alphabetical sorting of declarations.

## 1.24.1

### Fixed

- Bundling of `export var x = func; x = func2;` pattern.

## 1.24.0

### Added

- Upgraded all dependencies

### Fixed

- Bundling of code generated by TypeScript 3.9.2

## 1.23.1

### Fixed

- Global variable could still be wrongly shadowed
- Added workaround for incompatible change in Moment library, by ignoring JS module in src, because default exports and typings are mess.

## 1.23.0

### Added

- New settings file in `home/.bbcore/.bbcfg`
- First setting to disable post compilation OS notifications

```json
{
  "notificationsEnabled": false
}
```

## 1.22.0

### Improved

- Coverage reporter `spa` is more polished.
- Compressor knows how to remove unused `__async`.

## 1.21.0

### Added

- Allow to override sourceMap root in interactive mode.
- Autocreate .eslintrc if there is eslint-config-\* module in dev dependencies.

## 1.20.1

### Improved

- Coverage reporter `spa` does not need to be hosted in web server anymore. Just open `.coverage/index.html` from disk.

## 1.20.0

### Added

- New coverage reporter `spa`. For now result needs to be hosted in web server `npx http-server`.

## 1.19.2

### Fixed

- really fixed last fix

## 1.19.1

### Fixed

- renaming in new bundler - eval should work now.

## 1.19.0

### Added

- Made Chrome default everywhere. And allowed define headless browser strategy in `project.json` see `README.md` for details.

## 1.18.1

### Fixed

- Compress/const eval bug

## 1.18.0

### Added

- Inline worker reference support: `b.asset("project:worker:node_modules/monaco-editor/esm/vs/editor/editor.worker.js")`

- Fast bundler in Interactive mode does not use inline scripts, which allow to always enable (preventing all XSS in modern browsers):

```json
  "bobril": {
    "head": "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"/><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'self';style-src 'self' 'unsafe-inline';\"/>"
  },
```

## 1.17.0

### Added

- New parameter --spriteversiondir for override written path to bundle.png inside bundle.js

### Fixed

- A lot of bugs around resource assets
- A lot of failure states around project assets
- JS Parser chokes on surrogates in strings

## 1.16.0

### Changed

- Default TypeScript version is 3.8.3

### Added

- New coverage reporter json-details.

- New js command with globals subcommand to print all global variables from provided js file.

```sh
bb js globals file.js
```

## 1.15.1

### Fixed

Mangling of bundled code with eval.

## 1.15.0

### Added

Autodetect important files for code coverage. Resolve links in workspaces. Don't files only in node_modules are not considered important.

### Fixed

Hopefully random error in testing.
Firefox now correctly starts and is killed. And uses its one Profile `bb` which automatically creates.

## 1.14.0

### Added

Use BBBROWSER environmental variable to specify path to browser executable.
Added support for headless Firefox.
On Windows Firefox is preferred over Chrome.

### Fixed

Coverage works also for Jasmine 2.99.
BB test works again also without coverage.

## 1.13.0

### Added

Test command can generate coverage. `bb test -c json-summary` will create `coverage-summary.json`. `bb test -c none` will just print total coverage percentages.

## 1.12.1

### Changed

Updated dependencies.

## 1.12.0

### Added

Allow to use `-v` parameter also for `bb test` command.

### Changed

Undefined `process.env.XXX` is now replaced by `unknown` instead of left as is.

### Fixed

Don't crash on syntax errors (for these TS compiles invalid code, without reporting error itself)

## 1.11.1

### Fixed

- Wrong renaming in new bundler

## 1.11.0

### Added

- Coverage (unfinished, disabled by default)

### Fixed

- Wrong code generated in new bundler for nested export usage before declaration.
- Autorestart headless chrome if stopped responding.

## 1.10.1

### Fixed

- Fix export another shadowing global bug in just in new bundler.

## 1.10.0

### Changed

- New bundler is now default.

### Fixed

- Wrong splitting of long lines in minification in new bundler.
- Fix export shadowing global in both bundlers.

## 1.9.3

### Fixed

- Another bug squashed in bundler (unneeded brackets)
- IE11 fast bundler compatibility with module.exports = location

## 1.9.2

### Fixed

- Another bugs squashed in bundler (missing parentis)

## 1.9.1

### Fixed

- Another bugs squashed in bundler (`this` is not undefined and hex numbers bigger than 32bit)

## 1.9.0

### Changed

- By default enable `allowSyntheticDefaultImports`

### Fixed

- Minification in new bundler for Quill library.

## 1.8.0

### Added

- Option to proxy all unknown requests to project defined url including websockets (`"proxyUrl": "http://localhost:3001"` in `bobril` section in `package.json`)
- Chrome on Linux could be installed by Snap

## 1.7.0

### Added

- Option to force use new bundler (`"forceNewBundler": true` in `bobril` section in `package.json`)
- Many fixes and small features to be able to bundle `socket.io-client` npm module (only fast and new bundler works)

## 1.6.0

### Changed

- Default TypeScript version is 3.7.4

### Added

- Option to disable calculation of common project root (`"preserveProjectRoot": true` in `bobril` section in `package.json`)

## 1.5.1

### Fixed

- Reimplemented -5 error with not constant `b.asset` parameter.
- Fixed crash with immutable.js `this(x)`

## 1.5.0

### Added

- All bundlers does not use inline script in html anymore, so it allows stricter CSP policy.

### Fixed

- Spritting was broken in fast bundler

## 1.4.1

### Fixed

- Many regressions from 1.3 and 1.4 versions.

## 1.4.0

### Added

- All module \*.js imports are now compiled and detected for dependencies.
- Support for `browser` in `package.json` by [spec](https://github.com/defunctzombie/package-browser-field-spec)
  - Additionally if you define `"browser" : { "module_name": "module_name/dist/bundle.js" }` it override main js file for module imported by its name.
- Njsast based bundler supports bundling of `module.exports =` commonjs pattern. For example it is capable of bundling `sockjs-client` as is.
- `process.env.X` replacement works in js files too.

## 1.3.0

### Added

- All relative and deep \*.js imports are now compiled and detected for dependencies.

### Changed

- Special handling of @stomp/stompjs updated to version 5+

## 1.2.0

### Added

- Support for ServiceWorkers/PWA and WebWorkers
  - `b.asset` support new `project:` prefix which needs to be followed by relative directory path with `project.json`
  - target project must have defined `"bobril": { "variant": "worker" }` or `"bobril": { "variant": "serviceworker" }`
  - service worker automatically defines `swBuildDate` (contains date of build in string), `swBuildId` (contains obfuscated date of build in string), `swFiles` (array with all files in compilation)
  - example in `TestProjects/PWA/main`

```ts
import * as b from "bobril";
import * as Comlink from "comlink";

if ("serviceWorker" in navigator) {
  navigator.serviceWorker.register(b.asset("project:../sw")).then(function() {
    console.log("Service Worker Registered");
  });
}

var obj = Comlink.wrap(
  new Worker(b.asset("project:../worker"))
)};
```

## 1.1.0

### Added

- Support for TSX translation with bobril-g11n 5.0

```tsx
<g.T hint="hint for translator" param="parameter">
  Normal text{" "}
  <b>
    bold text{" "}
    <i>
      <u>with</u>
    </i>{" "}
    {g.t("{param}")}
  </b>
</g.T>
```

## 1.0.0

### Changed

- Testing iframe in `bb/test` page covers 100% of main window now.

## 0.99.0

### Added

- Support for bobril-g11n 5.0 translation of virtual dom messages (full TSX support will come later)

```tsx
g.t("Before{1/}After", { 1: () => <hr /> });
g.t("Normal text {1}bold text {2}with{/2} {param}{/1}", {
  1: (p: b.IBobrilChildren) => <b>{p}</b>,
  2: (p: b.IBobrilChildren) => (
    <i>
      <u>{p}</u>
    </i>
  ),
  param: "parameter",
});
```

- Fixes and speed up of Njsast based compression and mangling.

## 0.98.0

### Added

- New bundler from Njsast purely in C#. Currently not enabled by default, but could enabled by adding `bb b -x 1`. New bundler allows to generate source map, enable by `-g yes`. Using `--sourceRoot ".."` define what should be written in source map sourceRoot field, default is `..` which nicely works with default `dist` output directory.

## 0.97.1

### Fixed

- regression from 0.97.0 in DEBUG constant.

## 0.97.0

### Changed

- Default TypeScript version is 3.7.2

### Added

- Support for `process.env.X` constants which are replaced during compilation by some constant. They don't directly read system environment, what whey do must be specified in `package.json` by JavaScript expression. There is default definition for `NODE_ENV` to be `DEBUG?"development":"production"` like in React/Node apps.

- You can define global constants also using `package.json`. Till now there was only `DEBUG` which has default definition to be equal to build-in `DEBUG`.

- In these definition you can use build-in `env` object which allows to get value of environmental variable. See examples in README.

- Interactive mode could now correctly watch for changes in Docker. There is also new BBWATCHER environmental variable to control responsiveness of such watcher. See README for details.

## 0.96.3

### Fixed

- TypeScript emulation of array spread improved so that it works also Bobx observable arrays on IE11.

## 0.96.2

### Fixed

- Prefer .ts(x) over .d.ts (should solve problem with Bobril 13)

## 0.96.1

### Fixed

- Correct counting of skipped tests with focused tests and Jasmine 3.3

## 0.96.0

### Changed

- Default TypeScript version is 3.6.4

### Added

- Enabled Generators in default configuration.

## 0.95.2

### Fixed

Special casing `@stomp/stompjs` to use `lib/stomp.js` as main file instead of `index.js`.

## 0.95.1

### Fixed

Loader.js had to be also updated to support namespace modules.
Css @import's are moved to beginning of bundled css.

## 0.95.0

### Added

Support for modules in namespace like @stomp/stompjs

### Fixed

Build mode now sorts multiple css files for bundling in same way as interactive mode.

## 0.94.0

### Added

- support for `__spreadArrays` from tslib.

## 0.93.0

### Added

- allow to define default build path in `package.json`/`bobril`/`buildOutputDir` (default is `./dist`)

## 0.92.1

### Fixed

- Build must show same configuration errors like interactive build.
- Attempt to make watch change detection to more reliable.

## 0.92.0

### Added

- Test onerror handler to catch errors outside of suites.

## 0.91.4

### Fixed

- js and css files in html head are treated like resources (not touched just copied to output dir).
- patch TypeScript 3.6.2 bug https://github.com/microsoft/TypeScript/issues/33142

## 0.91.3

### Fixed

- Special case lenticular-ts npm to not expect already bundled js in main and enable bundling.
- Fix to ignore package.json typescript.main when it does not exist.
- Maybe crash when renaming file.
- Removed "use strict" from loader.js, so now it is again not in strict mode from start.

## 0.91.2

### Fixed

- Infinite compile cycle when started in directory with .git

## 0.91.1

### Fixed

- IncludeSources were not included in `bb test`.

## 0.91.0

### Changed

- Default TypeScript version is 3.5.3

### Fixed

- Crash in special errors in tests in Jasmine 3.3
- Detect expression name with code like `export const name = b.styleDef()`

## 0.90.0

### Added

- Paths to files in console are now absolute

### Fixed

- Crash with parsing jasmine timeout stack
- Fix assets generation
- Sprites images should not be in dist

## 0.89.0

### Added

- Nice error message when import is missing

### Fixed

- sprite.ts generation
- fix regression from 0.88.0 with cache

## 0.88.0

### Fixed

- Missing index.html in `bb b -f 1`
- Cache now includes relative file names for assets and sprites
- Print global semantic errors and make build failed in such case

## 0.87.0

### Change In Default

- Jasmine 3.3 is now default version instead of old 2.99 version.

### Fixed

- Json import now works in IE11. Also supports arrays in root.

## 0.86.4

### Fixed

- Sourcemap search didn't work for last span at line

## 0.86.3

### Fixed

- Fixed bundler to ignore reexporting interface by name.
- Missing import should be error and not warning

## 0.86.2

### Fixed

- Semantic check missed IncludeSources. Also fixed one thread safe issue with displaing errors.

## 0.86.1

### Fixed

- Better error message when sprite is not image.
- using do while could throw exception

## 0.86.0

### Added

- masively speed up builds (type checking runs in parallel and is relatively slow), incremental builds even more...

### Temporary Removed

- detection of missing imports and spare dependencies

## 0.85.0

### Changed

- Print original path in console messages relative to common root (that's behaviour change, but fixes also regression on focusing compilation errors from 0.83.1). Open original path in VSCode.

## 0.84.0

## 0.83.2

### Added

- allowing to disable update of tsconfig.json

## 0.83.1

### Fixed

- Fixed focusing error in VSCode when common source root is not project root.

## 0.83.0

### Added

- allowing to override autodetection of localization from package.json. And define directory for translations.

### Fixed

- added workaround fix which should resolve pending status when loading resources in Chrome 75 running in Docker
- global help also lists all commands.

## 0.82.3

### Fixed

- Missing import from d.ts should be silently skipped.

## 0.82.2

### Fixed

- assets can now define target dir as empty without crash
- recompilation in interactive mode in some cases missed to include some modules to bundle

## 0.82.1

### Fixed

- Don't show missing dependency from files which are in node_module directory.

## 0.82.0

### Added

- Package.json bobril section `testDirectories` can define array of relative paths to search test files

### Fixed

- Minor crashes

## 0.81.1

### Fixed

- Crash when exists d.ts file together with ts(x) file.

## 0.81.0

### Changed

- Default TypeScript version is 3.4.5

### Added

- b.asset now forces external resource handling when parameter is prefixed by `resource:` like:

```ts
console.log(b.asset("resource:./src/file.js")); // depending on mode prints "src/file.js" or "b.js"
```

- b.asset("node_modules/x") now also search parent node_modules directories

### Fixed

- Asset resolving now correctly relative to common build directory and not to project directory

## 0.80.0

### Added

- Support for importing css files.
- Support for esm modules. (means importing monaco-editor "works")

### Fixed

- Allow to use template strings.

## 0.79.2

### Fixed

- Allow to read @types higher than is project root.

## 0.79.1

### Fixed

- Regression in importing d.ts with js.
- Don't print unused dependencies when build has errors.

## 0.79.0

### Changed

- Free memory after each interactive build.

## 0.78.2

### Fixed

- Relative module imports from 0.78.0 truly works with more complex project.

## 0.78.1

### Fixed

- Relative module imports from 0.78.0 works with more complex project.

## 0.78.0

### Added

- Allow to disable Error -10 which forbids relative imports into modules.

## 0.77.0

### Changed

- Default TypeScript version is 3.4.1

### Fixed

- Added DisableFatalOnOOM = true to ChakraCore settings.

## 0.76.0

### Added

- BBCACHEDIR environmental variable allows to override .bbcache directory placement.
- Errors in tests between them are now correctly displayed and reported.

### Changed

- Test sources are now sorted (imported in same order in test.html on all platforms).

### Fixed

- Don't crash at Quill

## 0.75.0

### Improved

- Updated to .NetCore 2.2. Updated all dependencies.

## 0.74.0

### Improved

- Decreased memory consumption of Build and Test command. Test command with -d parameter does not run tests just create output.
- Updated ChakraCore dependency.

### Fixed

- Module resolver now first search in project root and then continue with classic node resolver.

## 0.73.0

### Added

- Log more details about testing in verbose mode.

### Changed

- Module Resolver changed to be more similar to node.
- Modification Watcher should use less resources (Big projects on Mac should work now)

### Fixed

- Filter test parameter works better when empty.

## 0.72.2

### Fixed

- another attempt of const eval fix

## 0.72.1

### Fixed

- import { x } from "y", when x was reexported constant, failed to find that it is constant during compilation
- use jasmine.d.ts from project (forget to port from original bb). it also fixes showing jasmine.d.ts in findunused

## 0.72.0

### Changed

- Default TypeScript version is 3.3.3333

### Fixed

- Some bugs in findunused and assets features.

## 0.71.0

### Added

- special handling for node_modules path in new assets feature. Also make module as used when just assets are taken from it.

## 0.70.0

### Added

- findunused command which list all \*.ts files which are not part of project.
- Used Modules can now add assets to dist. In package.json bobril section you can define assets object which key is original asset name and value is name of asset in dist.

### Removed

- Useless blametest command.

## 0.69.0

### Added

- Command line option to filter tests by any regex in test command (contributed by https://github.com/scottis).

## 0.68.1

### Fixed

- Json modules support wrongly forbidden to name modules x.json.ts

## 0.68.0

### Changed

- Updated multiple dependencies to latest versions notably ChakraCore

### Fixed

- Workaround for too defensive checks in TypeScript 2742 error by hot patching TypeScript source

## 0.67.0

### Changed

- Default TypeScript is now 3.3.1

### Added

Highlight when default port is not available.

### Fixed

Bundling Json modules.

## 0.66.0

### Added

Implemented support for `resolveJsonModule` and it is enabled by default.

## 0.65.0

### Added

Allow to specify version of Jasmine want to use default is "2.99", but you can also set "3.3". In some future version 3.3 will be made default.

## 0.64.2

### Fixed

Removed leftover "debug" view from bb web.

## 0.64.1

### Fixed

Problem with strange compilation error after specific change.

## 0.64.0

### Improved

Completely redesigned web inside bobril-build. Contributed by https://github.com/0papen0, sponsored by Quadient.

## 0.63.2

### Fixed

Off by one error in previous changes.

## 0.63.1

### Fixed

Partially optimized speed regression in 0.63.0.
Changed not working Spec File Name propagation to Stack propagation and it costs "only" 2% of test speed.

## 0.63.0

### Fixed

Major problem in compilation cache.
Regression in translation commands from 0.62.0.

### Added

Allow to ignore import of obsolete module by useing comment `// BBIgnoreObsolete: modulea, moduleb`
Propagate Spec File Name to web.

## 0.62.3

### Fixed

Ouch. One more fix.

## 0.62.2

### Fixed

Regression from 0.62.

## 0.62.1

### Fixed

Improved parsing of changelogs in package upgrage.

## 0.62.0

### Added

Way to mark module as obsolete. See README for details.

### Fixed

BB b -u 1 now removes unused texts from translation files.

## 0.61.0

### Added, but possibly breaking changes

If both package-lock.json and yarn.lock exists, bobril-build will refuse to use yarn or npm. Which will prevent inconsistent installed packages.

Improved command line parser to stop on invalid or unknown parameter, only help should be shown.

## 0.60.0

### Added

Simplified development of bb and bb/test webs. New parameters allow to proxy request to another bobril-build. Both webs are also stored as zip in resources which allows to have variable number of files not just html and js. tools/WebUpdateUI automates builds and update zip files.

    tools/web> bb -p 10000
    sometestproject> bb --proxybb http://localhost:10000

### Fixed

Added missing --save parameter for npm version of bb package add command.

## 0.59.0

### Added

- Test command has now -d parameter which can define where to dump build files for testing

### Changed

- Default TypeScript is now 3.1.6

### Fixed

- detection of triple slash references (TypeScript is still somewhat buggy https://github.com/Microsoft/TypeScript/issues/26439)

## 0.58.0

### Changed

- Default TypeScript is now 3.1.3

## 0.57.0

### Changed

- By default all @types modules will be now referenced by TypeScript.
- Debugger port of headless browser changed from 9222 to 9223.

### Added

- blametest command for randomized searching for Headless chrome bugs.

### Fixed

- Detection of dependencies does not break build in second rebuild. It also does not show as missing dependency when it is in devDependencies. Automatically ignore @types/ dependencies.

## 0.56.0

### Added

- Detection of missing or superfluous dependencies in package.json. For temporary disable this feature use "ignoreDiagnostic": [-12, -13]

## 0.55.1

## 0.55.0

### Added

- ignoreDiagnostic array in bobril section that allows list warnings and errors which you want to ignore (not count and not show)

### Fixed

- Upgraded Javascript switcher and Chakracore, hoping it fixes problems on Linux
- Removed GarbageCollect call from 0.53.0 which caused StackOverflow on complex builds.

## 0.54.0

### Fixed

- Parsing of standalone command line parameters, which fixes bb package upgrade module

### Added

- Localize command line parameter to Interactive and Test commands
- Directory watcher is now never used in Docker or for noninteractive commands

## 0.53.0

### Added

- Lowered memory usage with just small slow down
- Allow to manually dispose JS Engine to free most of memory. Write "f" and Enter when bb running.
- warningsAsErrors package.json bobril section flag.
- All errors and warnings are printed to console too.

## 0.52.0

### Added

- Official Docker build
- Making it work in Docker
- package upgrade command also creates DEPSCHANGELOG.md

## 0.51.1

### Fixed

- improved error message to investigate TypeScript package download failure
- npm upgrade command now uses bullet proof directory delete.

## 0.51.0

### Added

- In package upgrade command print relevant CHANGELOG.md entries.

## 0.50.1

### Fixed

- Regression with crashing when Downloading TypeScript

## 0.50.0

### Added

- Added Npm next to Yarn support
- Added new package command line commands which autodetect what package manager you are using

## 0.49.0

### Added

- Translation Union, Subtract

## 0.48.0

### Added

- Translation Import and Export
- Variants: worker, serviceworker (different default libs and auto nohtml mode)

## 0.47.0

### Changed

- Default version of TypeScript is now 3.0.3.

### Added

- NoHtml mode for creating just self contained bundle.js.

## 0.46.1

### Fixed

- Exception during resolving constants in imports by name

## 0.46.0

### Added

- Support for bobril-g11n Delayed and Serializable messages
- Upgraded to ChakraCore 1.10.0

### Fixed

- Removed todo commands from command line help
- Invalid errors in .d.ts files not shown anymore

## 0.45.0

### Changed

- Default version of TypeScript is now 3.0.1.

## 0.44.1

### Fixed

- Parsing of relative noago parameter in g11n checker.

## 0.44.0

### Changed

- Default version of TypeScript is now 2.9.2.

## 0.43.0

### Added

- New warning and error for wrong usage of bobril-g11n.t function.

## 0.42.3

### Fixed

- Regression with compiler options. Sorry.

## 0.42.2

### Fixed

- Don't crash on directory without package.json.
- tsconfig compiler options enum names fixed.

## 0.42.1

### Fixed

- Minor cosmetic bugs.

## 0.42.0

### Added

- True version of TypeScript compiler shown in console.
- Example for auto dppx sprites.

### Fixed

- Error messages passed from JS to C# correctly again.

## 0.41.0

### Added

- Generated tsconfig.json files now contains IncludedSources from bobril section in package.json

## 0.40.0

### Added

- Implemented translation command addlang and removelang parameters.
- --port parameter for test command.
- WIP: Spritting creates bundle with higher quality.

## 0.39.0

### Added

- Allow to disable yarn to fail on creating links when inside Docker on Windows filesystem. See README for more details.
- During compiling detect errors in translation messages.

### Fixed

- Failure to start 3rd instance with enabled BuildCache

## 0.38.0

### Changed

- Default TypeScript version to 2.8.3

### Added

- Use TypeScript from project if it is in its dependencies and not overridden by tsVersion in project.json

## 0.37.1

### Fixed

- Don't crash on no tests...

## 0.37.0

### Added

- Generate test result even there are no tests.
- Support for semitransparent sprites.
- Exiting info in console.

## 0.36.0

### Fixed

- Regression of modification of exports was not taken in account from 0.35.4.

### Added

- Build-in bb-assets-generator-plugin for generating **assets.ts** and **sprites.ts** to src directory.

## 0.35.4

### Fixed

- Compilation failed when modifying file which is part of import cycle.

## 0.35.3

### Updated

- BTDB dependency to 13.0.0 nuget.

### Fixed

- Build does not fail after yarn installing modules.
- Crash when testing stack exception contained nested stack with some characters after closing parentheses.

## 0.35.2

### Fixed

- Webserver now works again. Had to revert to some older nugets for ASPNetCore/Kestrel.

## 0.35.1

## 0.35.0

### Updated

- UglifyJs to latest sources.

### Fixed

- Returned back custom version of ChakraCore because 1.8.3 still does not have fixes we need.
- Fixed dependencies for ImageSharp. Which also sometimes crashed bbcore.

## 0.34.1

### Fixed

- Spritting didn't worked due to incompatibility of nugets. Now whole project works on .NetCore 2.1 Preview 2. It means you need Visual Studio 15.7.0 Preview for development of bbcore.

## 0.34.0

### Added

- Upgraded to ChakraCore 1.8.3
- Upgraded to .NetCore 2.1.0 Preview 2 nugets.
- [Windows specific feature] Notifications about build and test. (Contributed by https://github.com/JanVargovsky)
- Added parameter --bindToAny to allow listen for external computers.

### Fixed

- Crash when no dependencies in package.json.

## 0.33.1

### Fixed

- Another bug fixed in Watcher.

## 0.33.0

### Added

- Auto-updating tslint.json from bb-tslint-plugin when is in devDependencies.

### Fixed

- Watcher does not crash build when directory is deleted.

## 0.32.0

### Added

- Option for tests to generate hierarchical output XML. (Contributed by https://github.com/pstovik)

## 0.31.0

### Added

- IncludeSources feature mainly for including d.ts files into compilation. See TestProjects/IncludeSources/package.json for example.

## 0.30.0

### Fixed

- Prevent build cache poisoning in case of error in any file not just one storing to cache.

## 0.29.0

### Added

- Interactive modes now supports --versionDir parameter from build command.

### Changed

- AllowJs is false in tsconfig.json, even through it is still true in build.

### Fixed

- Regression with failing tests after recompilation. Do not include virtual d.ts files to list of files to automatically compile.
- JUnit xml correctly specify utf-8 encoding.

## 0.28.0

### Improved

- Bundling speed due to fix in uglify-es.

### Changed

- Default regex for test sources now includes also spec.d.ts files allowing easily extend build in jasmine matchers.

### Fixed

- BuildCache with JavaScript source without d.ts crashed.

## 0.27.3

### Fixed

- Extremely slow build compression with a lot of unused constants. And even improved compression size!
- Build exception with empty ts file.

## 0.27.2

### Fixed

- Hosting source code in case of sandboxes projects. In `TestProjects/Sandbox/piskoviste` there is example of setup how to enable debugging from VS Code.

## 0.27.1

### Fixed

- Translations in some cases are null instead of default language fallback.

## 0.27.0

### Fixed

- Wrong source maps when js source ended with new line.

### Changed

- build -u 1 command now just update translations, but does not output any build files making it much faster.

## 0.26.0

### Fixed

- Crash of minification with very complex files by using custom build of ChakraCore.dll (only for Windows)
- Again rewritten file watching.

## 0.25.0

### Fixed

- g.t("x",null,"hint") is now detected as without params.
- Upgraded to Chakra Core 1.8.2
- Fixed parsing of translation files
- And many more ...

## 0.24.0

### Fixed

- Disabling dependencies update from package.json didn't worked.
- More then doubled stack size for ChakraCore by forking JavaScriptEngineSwitcher.ChakraCore preventing stack overflows for complex TypeScript code.

## 0.23.0

### Removed

- Not using IL Linker anymore, it too buggy.

## 0.22.0

### Fixed

- Build Cache now really speed up compilation.

### Added

- Auto patching of TypeScript 2.8.1 to make it possible to use. Still left TS 2.7.2 as default, due to new errors occurring in large code bases.

## 0.21.0

### Improved

- TypeScript parsing AST cache is now enabled only for d.ts files. Decreasing needed memory.

### Implemented

- If module missing types search in node_modules/@types/name/index.d.ts

## 0.20.8

### Fixed

- Tests in sandbox mode works now even when they don't reference files outside of project.

## 0.20.7

### Fixed

- Interactive mode missed assets after small change in code.

## 0.20.6

### Fixed

- Allow to start again multiple instances by not sharing build cache.
- Test command now adds names in b.styleDefs to have identical behavior as old bobril-build.

### Improved

- import lazy load script now inspired by Webpack (supports timeout, allows retry on failure, does not memory leak)
- html output made smaller.
- speed of resolving sourcemaps

## 0.19.0

### Added

- Eval const node works across d.ts -> ts transition.
- Reenabled build cache.

### Fixed

- Crash when package.json does not have compiler options.

## 0.18.1

### Fixed

- Bundling should ignore filename casing errors

## 0.18.0

### Fixed

- Off by one error in SourceMap resolver
- Source code could be js(x) and not just ts(x)

## 0.17.2

## 0.17.1

## 0.17.0

### Fixed

- First parameter of b.sprite does not need to be constant.
- Temporary disabling build cache to workaround problem with const evaluation across modules.

## 0.16.0

### Added

- Creating tsconfig.json
- Showing error if first parameter of b.asset or b.sprite is not constant.
- Fixed consistency of bobril-build specific diagnostic messages and made list of them in README

## 0.15.0

### Added

- OSX version

## 0.14.1

### Fixed

- Added missing TS compiler options (strictFunctionTypes, strictPropertyInitialization)

## 0.14.0

### Fixed

- Added missing support in slow bundle for js assets.

## 0.13.0

### Added

- Build cache which speed up cold build. Currently just modules without dependencies.

## 0.12.0

### Added

- Livereload

### Changed default

- Default version of Typescript set to 2.7.2

### Fixed

- Fixed crash on build with enabled spritting, but without any sprite. Now for real :-)

## 0.11.0

### Added

- Allow to enable spritting in interactive and test mode.

### Fixed

- Fixed crash on test command without defined -o
- Fixed crash on build with enabled spritting, but without any sprite.

## 0.10.0

### Added

- Build and test commands finished.
- Support for "sandbox" type of projects.
- Support for sprite generation.

## 0.9.2

### Fixed

- Workaround for crashing bug in TypeScript 2.7.1

## 0.9.1

### Fixed

- @types resolving does not crash build.

## 0.9.0

### Modified

- Rewritten DiskCache and Watching

### Added

- Allow override TypeScript version

## 0.8.0

### Modified

- Uses TypeScript 2.7.1

## 0.7.2

### Fixed

- Lock due to yarn had strange std output.

## 0.7.1

### Fixed

- Javascript assets are correctly placed at start of fast bundle. Full bundle is left broken for this to be fixed later.
- Quick fix for compilation endless cycle due to yarn touching package.json

## 0.7.0

### Added

- Updating dependencies like original bobril-build. It uses just yarn.

### Fixed

- Choosing of free port to listen to.

## 0.6.1

### Fixed

- When asset path starts with node_modules/ it should be relative to project root.

## 0.6.0

### Added

- Error reporting of missing binary dependencies.

### Fixed

- Testing crash when throwing string from test method. You should throws Error objects instead, because there is no way to get stack trace from it.
- Tried to make starting webserver on free port more resilient to strange problems.

## 0.5.0

### Added

- Some part of build command. Needs a lot of work.

### Fixed

- Killing of Chrome even more reliable, but upgrade of bobril-build-core is needed.

## 0.4.2

### Fixed

- Removed all dynamic staff so code runs after ILLinker
- Killing of Chrome on Ctrl+C more reliable

## 0.4.1

### Fixed

- Exit by Ctrl+C improved
- Missing resources after rebuild
- Example files list after rebuild

## 0.4.0

### Added

- Parsing commandline and running commands implemented. Interactive command is now true default command, so all its parameters works without specifying it too.
- Listening port in iteractive mode selectable from command line.
- New --verbose parameter for interactive commands (it logs all first chance exceptions). Shortened output without --verbose.

### Fixed

- Crashes when started in directory without package.json. Automatically compiling index.ts(x)

## 0.3.1

### Fixed

- Js file is added to compilation when locally importing its d.ts file. When recompiling too.

## 0.3.0

### Added

- Js file is added to compilation when locally importing its d.ts file.

## 0.2.0

### Added

- Support for bobril.additionalResourcesDirectory
- Added workaround for clients requesting files with wrong casing
- New console message about compilation starting

## 0.1.0

### Added

- Linux version!
- Logging file count in memory after compilation.

### Fixed

- Optimized speed of searching in SourceMaps
- Fixed missing en-us.js

## 0.0.1

### Added

- Changelog
- First version released through GitHub releases.

## 0.0.0
