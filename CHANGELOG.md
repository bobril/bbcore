# Changelog

## [unreleased]

### Fixed

*   Regresion of modification of exports was not taken in account from 0.35.4.

### Added

*   Build-in bb-assets-generator-plugin for generating **assets.ts** and **sprites.ts** to src directory.

## 0.35.4

### Fixed

*   Compilation failed when modifying file which is part of import cycle.

## 0.35.3

### Updated

*   BTDB dependency to 13.0.0 nuget.

### Fixed

*   Build does not fail after yarn installing modules.
*   Crash when testing stack exception contained nested stack with some characters after closing parentheses.

## 0.35.2

### Fixed

*   Webserver now works again. Had to revert to some older nugets for ASPNetCore/Kestrel.

## 0.35.1

## 0.35.0

### Updated

*   UglifyJs to latest sources.

### Fixed

*   Returned back custom version of ChakraCore because 1.8.3 still does not have fixes we need.
*   Fixed dependencies for ImageSharp. Which also sometimes crashed bbcore.

## 0.34.1

### Fixed

*   Spritting didn't worked due to incompatibility of nugets. Now whole project works on .NetCore 2.1 Preview 2. It means you need Visual Studio 15.7.0 Preview for development of bbcore.

## 0.34.0

### Added

*   Upgraded to ChakraCore 1.8.3
*   Upgraded to .NetCore 2.1.0 Preview 2 nugets.
*   [Windows specific feature] Notifications about build and test. (Contributed by https://github.com/JanVargovsky)
*   Added parameter --bindToAny to allow listen for external computers.

### Fixed

*   Crash when no dependencies in package.json.

## 0.33.1

### Fixed

*   Another bug fixed in Watcher.

## 0.33.0

### Added

*   Autoupdating tslint.json from bb-tslint-plugin when is in devDependencies.

### Fixed

*   Watcher does not crash build when directory is deleted.

## 0.32.0

### Added

*   Option for tests to generate hierarchical output XML. (Contributed by https://github.com/pstovik)

## 0.31.0

### Added

*   IncludeSources feature mainly for including d.ts files into compilation. See TestProjects/IncludeSources/package.json for example.

## 0.30.0

### Fixed

*   Prevent build cache poisoning in case of error in any file not just one storing to cache.

## 0.29.0

### Added

*   Interactive modes now supports --versionDir parameter from build command.

### Changed

*   AllowJs is false in tsconfig.json, even through it is still true in build.

### Fixed

*   Regression with failing tests after recompilation. Do not include virtual d.ts files to list of files to automatically compile.
*   JUnit xml correctly specify utf-8 encoding.

## 0.28.0

### Improved

*   Bundling speed due to fix in uglify-es.

### Changed

*   Default regex for test sources now includes also spec.d.ts files allowing easily extend build in jasmine matchers.

### Fixed

*   BuildCache with JavaScript source without d.ts crashed.

## 0.27.3

### Fixed

*   Extreamly slow build compression with a lot of unused constants. And even improved compression size!
*   Build exeption with empty ts file.

## 0.27.2

### Fixed

*   Hosting source code in case of sandboxes projects. In `TestProjects/Sandbox/piskoviste` there is example of setup how to enable debugging from VS Code.

## 0.27.1

### Fixed

*   Translations in some cases are null instead of default language fallback.

## 0.27.0

### Fixed

*   Wrong source maps when js source ended with new line.

### Changed

*   build -u 1 command now just update translations, but does not output any build files making it much faster.

## 0.26.0

### Fixed

*   Crash of minification with very complex files by using custom build of ChakraCore.dll (only for Windows)
*   Again rewritten file watching.

## 0.25.0

### Fixed

*   g.t("x",null,"hint") is now detected as without params.
*   Upgraded to Chakra Core 1.8.2
*   Fixed parsing of translation files
*   And many more ...

## 0.24.0

### Fixed

*   Disabling dependencies update from package.json didn't worked.
*   More then doubled stack size for ChakraCore by forking JavaScriptEngineSwitcher.ChakraCore preventing stack overflows for complex TypeScript code.

## 0.23.0

### Removed

*   Not using IL Linker anymore, it too buggy.

## 0.22.0

### Fixed

*   Build Cache now really speed up compilation.

### Added

*   Auto patching of TypeScript 2.8.1 to make it possible to use. Still left TS 2.7.2 as default, due to new errors occurring in large code bases.

## 0.21.0

### Improved

*   TypeScript parsing AST cache is now enabled only for d.ts files. Decreasing needed memory.

### Implemented

*   If module missing types search in node_modules/@types/name/index.d.ts

## 0.20.8

### Fixed

*   Tests in sandbox mode works now even when they don't reference files outside of project.

## 0.20.7

### Fixed

*   Interactive mode missed assets after small change in code.

## 0.20.6

### Fixed

*   Allow to start again multiple instances by not sharing build cache.
*   Test command now adds names in b.styleDefs to have identical behavior as old bobril-build.

### Improved

*   import lazy load script now inspired by Webpack (supports timeout, allows retry on failure, does not memory leak)
*   html output made smaller.
*   speed of resolving sourcemaps

## 0.19.0

### Added

*   Eval const node works across d.ts -> ts transition.
*   Reenabled build cache.

### Fixed

*   Crash when package.json does not have compiler options.

## 0.18.1

### Fixed

*   Bundling should ignore filename casing errors

## 0.18.0

### Fixed

*   Off by one error in SourceMap resolver
*   Source code could be js(x) and not just ts(x)

## 0.17.2

## 0.17.1

## 0.17.0

### Fixed

*   First parameter of b.sprite does not need to be constant.
*   Temporary disabling build cache to workaround problem with const evaluation across modules.

## 0.16.0

### Added

*   Creating tsconfig.json
*   Showing error if first parameter of b.asset or b.sprite is not constant.
*   Fixed consistency of bobril-build specific diagnostic messages and made list of them in README

## 0.15.0

### Added

*   OSX version

## 0.14.1

### Fixed

*   Added missing TS compiler options (strictFunctionTypes, strictPropertyInitialization)

## 0.14.0

### Fixed

*   Added missing support in slow bundle for js assets.

## 0.13.0

### Added

*   Build cache which speed up cold build. Currently just modules without dependencies.

## 0.12.0

### Added

*   Livereload

### Changed default

*   Default version of Typescript set to 2.7.2

### Fixed

*   Fixed crash on build with enabled spritting, but without any sprite. Now for real :-)

## 0.11.0

### Added

*   Allow to enable spritting in interactive and test mode.

### Fixed

*   Fixed crash on test command without defined -o
*   Fixed crash on build with enabled spritting, but without any sprite.

## 0.10.0

### Added

*   Build and test commands finished.
*   Support for "sandbox" type of projects.
*   Support for sprite generation.

## 0.9.2

### Fixed

*   Workaround for crashing bug in TypeScript 2.7.1

## 0.9.1

### Fixed

*   @types resolving does not crash build.

## 0.9.0

### Modified

*   Rewritten DiskCache and Watching

### Added

*   Allow override TypeScript version

## 0.8.0

### Modified

*   Uses TypeScript 2.7.1

## 0.7.2

### Fixed

*   Lock due to yarn had strange std output.

## 0.7.1

### Fixed

*   Javascript assets are correctly placed at start of fast bundle. Full bundle is left broken for this to be fixed later.
*   Quick fix for compilation endless cycle due to yarn touching package.json

## 0.7.0

### Added

*   Updating dependencies like original bobril-build. It uses just yarn.

### Fixed

*   Choosing of free port to listen to.

## 0.6.1

### Fixed

*   When asset path starts with node_modules/ it should be relative to project root.

## 0.6.0

### Added

*   Error reporting of missing binary dependencies.

### Fixed

*   Testing crash when throwing string from test method. You should throws Error objects instead, because there is no way to get stack trace from it.
*   Tried to make starting webserver on free port more resilient to strange problems.

## 0.5.0

### Added

*   Some part of build command. Needs a lot of work.

### Fixed

*   Killing of Chrome even more reliable, but upgrade of bobril-build-core is needed.

## 0.4.2

### Fixed

*   Removed all dynamic staff so code runs after ILLinker
*   Killing of Chrome on Ctrl+C more reliable

## 0.4.1

### Fixed

*   Exit by Ctrl+C improved
*   Missing resources after rebuild
*   Example files list after rebuild

## 0.4.0

### Added

*   Parsing commandline and running commands implemented. Interactive command is now true default command, so all its parameters works without specifying it too.
*   Listening port in iteractive mode selectable from command line.
*   New --verbose parameter for interactive commands (it logs all first chance exceptions). Shortened output without --verbose.

### Fixed

*   Crashes when started in directory without package.json. Automatically compiling index.ts(x)

## 0.3.1

### Fixed

*   Js file is added to compilation when locally importing its d.ts file. When recompiling too.

## 0.3.0

### Added

*   Js file is added to compilation when locally importing its d.ts file.

## 0.2.0

### Added

*   Support for bobril.additionalResourcesDirectory
*   Added workaround for clients requesting files with wrong casing
*   New console message about compilation starting

## 0.1.0

### Added

*   Linux version!
*   Logging file count in memory after compilation.

### Fixed

*   Optimized speed of searching in SourceMaps
*   Fixed missing en-us.js

## 0.0.1

### Added

*   Changelog
*   First version released through GitHub releases.

## 0.0.0
