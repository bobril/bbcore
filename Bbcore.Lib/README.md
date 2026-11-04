# Bbcore.Lib

Library form of [bbcore](https://github.com/bobril/bbcore), the build tool (`bb`) for
[Bobril](https://github.com/bobril/bobril) applications written in TypeScript. It transpiles, typechecks and bundles
TypeScript into JavaScript and can also run whole `bb` commands from your .NET code.

## Build TypeScript into one JavaScript bundle

Sources are read through `IFsAbstraction`, so they can live in memory (`InMemoryFs`) or on disk
(`NativeFsAbstraction`).

```csharp
using Bbcore.Lib;
using Lib.DiskCache;

var files = new InMemoryFs();
files.WriteAllUtf8("/tmp/index.ts", "let a: string = 'hello'; console.log(a);");

var bbcore = new BbcoreLibrary();
if (bbcore.RunBuild(files, "5.2.2", "/tmp", out var javaScript, out var messages))
    Console.WriteLine(javaScript);
else
    Console.Error.WriteLine(messages);
```

The overload with `out string? sourceMap` also returns the source map of the bundle.

## Run bb commands

`Run` takes the same command line arguments as the `bb` executable and returns its exit code:

```csharp
using Lib.Utils.Logger;

var exitCode = await new BbcoreLibrary().Run(["b"], new ConsoleLogger());
```

## Documentation

Configuration (`package.json` `bobril` section, `.bbrc`), commands and changes between versions are described in the
[bbcore repository](https://github.com/bobril/bbcore) and its
[changelog](https://github.com/bobril/bbcore/blob/master/CHANGELOG.md).

## License

MIT
