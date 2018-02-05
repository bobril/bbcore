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
