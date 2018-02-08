# Changelog

## [unreleased]

## 0.9.2

### Fixed

* Workaround for crashing bug in TypeScript 2.7.1

## 0.9.1

### Fixed

* @types resolving does not crash build.

## 0.9.0

### Modified

* Rewritten DiskCache and Watching

### Added

* Allow override TypeScript version

## 0.8.0

### Modified

* Uses TypeScript 2.7.1

## 0.7.2

### Fixed

* Lock due to yarn had strange std output.

## 0.7.1

### Fixed

* Javascript assets are correctly placed at start of fast bundle. Full bundle is left broken for this to be fixed later.
* Quick fix for compilation endless cycle due to yarn touching package.json

## 0.7.0

### Added

* Updating dependencies like original bobril-build. It uses just yarn.

### Fixed

* Choosing of free port to listen to.

## 0.6.1

### Fixed

* When asset path starts with node_modules/ it should be relative to project root.

## 0.6.0

### Added

* Error reporting of missing binary dependencies.

### Fixed

* Testing crash when throwing string from test method. You should throws Error objects instead, because there is no way to get stack trace from it.
* Tried to make starting webserver on free port more resilient to strange problems.

## 0.5.0

### Added

* Some part of build command. Needs a lot of work.

### Fixed

* Killing of Chrome even more reliable, but upgrade of bobril-build-core is needed.

## 0.4.2

### Fixed

* Removed all dynamic staff so code runs after ILLinker
* Killing of Chrome on Ctrl+C more reliable

## 0.4.1

### Fixed

* Exit by Ctrl+C improved
* Missing resources after rebuild
* Example files list after rebuild

## 0.4.0

### Added

* Parsing commandline and running commands implemented. Interactive command is now true default command, so all its parameters works without specifying it too.
* Listening port in iteractive mode selectable from command line.
* New --verbose parameter for interactive commands (it logs all first chance exceptions). Shortened output without --verbose.

### Fixed

* Crashes when started in directory without package.json. Automatically compiling index.ts(x)

## 0.3.1

### Fixed

* Js file is added to compilation when locally importing its d.ts file. When recompiling too.

## 0.3.0

### Added

* Js file is added to compilation when locally importing its d.ts file.

## 0.2.0

### Added

* Support for bobril.additionalResourcesDirectory
* Added workaround for clients requesting files with wrong casing
* New console message about compilation starting

## 0.1.0

### Added

* Linux version!
* Logging file count in memory after compilation.

### Fixed

* Optimized speed of searching in SourceMaps
* Fixed missing en-us.js

## 0.0.1

### Added

* Changelog
* First version released through GitHub releases.

## 0.0.0
