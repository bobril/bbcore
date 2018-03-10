# bbcore

Rewrite of bobril-build to .Net Core. Mainly for speed reasons. Massive work in progress.

# How to start

    yarn global add bobril-build-core

    bb2

# How to override used version

In `package.json` create `bobril` section and set `bbVersion` to specific version you need. By setting `tsVersion` you can override used TypeScript for compilation.

    "bobril": {
        "bbVersion": "0.9.0",
        "tsVersion": "2.7.1"
    }

# List of bobril-build specific warnings and errors

| Number | Severity | Message                                                         |
| ------ | -------- | --------------------------------------------------------------- |
| -1     | Warn     | Local import has wrong casing                                   |
| -2     | Warn     | Module import has wrong casing                                  |
| -3     | Error    | Missing dependency                                              |
| -4     | Error    | First parameter of b.sprite must be resolved as constant string |
| -5     | Error    | First parameter of b.asset must be resolved as constant string  |
| -6     | Error    | b.sprite cannot have more than 6 parameters                     |
