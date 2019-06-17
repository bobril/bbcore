# bbcore

Rewrite of bobril-build to .Net Core. Mainly for speed reasons. It now replaced original JS version.

# How to start

    yarn global add bobril-build

    bb

# What to do when bb failing to start for first time because Github rate limit

Github by default has limit of 60 anonymous requests per hour from one IP. So if it fails you have 2 options, wait or provide token to authenticate as yourself. First read [https://help.github.com/articles/creating-a-personal-access-token-for-the-command-line/] how to create your token. Token can have just read only rights, you don't need to select any scopes. Then you can either store it into your user profile directory in .github/token.txt file or set environment variable GITHUB_TOKEN.

# How to override used version

By default prerelease versions are not used.

In `package.json` create `bobril` section and set `bbVersion` to specific version you need. By setting `tsVersion` you can override used TypeScript for compilation. By setting `jasmineVersion` you can override Jasmine version only allowed values are "2.99" (default) and "3.3".

    "bobril": {
        "bbVersion": "0.9.0",
        "tsVersion": "2.7.1",
        "jasmineVersion: "2.99"
    }

By setting `BBVERSION` enviroment variable you can define default version (including prerelease). If you will start `bb2` instead of `bb`, then `BBVERSION` override what is in `package.json`

# How to override where bb store its caches

bb stores its cache in `user_home/.bbcache` directory. In Docker it uses `/bbcache`. You can override it be defining `BBCACHEDIR` environmental variable.

# How to use Docker version

## Run it

    docker run -it --rm -v %cd%:/project -v directory_for_persistent_cache:/bbcache -p 8080:8080 bobril/build

## What it contains

- Bobril-build
- Chrome
- Nodejs
- Npm
- Yarn

## Build it on your own

    docker build . -t bobril/build --build-arg VERSION=x.y.z
    docker tag bobril/build bobril/build:x.y.z
    docker push bobril/build:x.y.z

## Look inside

    docker history bobril/build
    docker run -it --rm --entrypoint bash bobril/build

# List of bobril-build specific warnings and errors

| Number | Severity | Message                                                                                        |
| ------ | -------- | ---------------------------------------------------------------------------------------------- |
| -1     | Warn     | Local import has wrong casing                                                                  |
| -2     | Warn     | Module import has wrong casing                                                                 |
| -3     | Error    | Missing dependency                                                                             |
| -5     | Error    | First parameter of b.asset must be resolved as constant string                                 |
| -6     | Error    | b.sprite cannot have more than 6 parameters                                                    |
| -7     | Warn     | Problem with translation message                                                               |
| -8     | Error    | Translation message must be compile time resolvable constant string, use f instead if intended |
| -9     | Error    | Hint message must be compile time resolvable constant string                                   |
| -11    | Warn     | Fixing local import with two slashes                                                           |
| -12    | Warn     | Importing module without being in package.json as dependency                                   |
| -13    | Warn     | Unused dependency in package.json                                                              |
| -14    | Warn     | Importing obsolete module: reason                                                              |
| -15    | Warn     | Cannot resolve import                                                                          |

# Package.json - bobril section features

## Update of tsconfig.json

Build generates tsconfig.json by default. You can disable this feature by:

    "bobril": {
        "tsconfigUpdate": false
    }

## Override localization

By default localization is detected from exitence of dependency bobril-g11n. You can override it:

    "bobril": {
        "localize": true
    }

## Override directory with translations

    "bobril": {
        "pathToTranslations": "translations/path/like/this"
    }

It is relative to project. Default is "translations".

## Where to find test sources

By default it finds all tests in project directory (it always skips `node_modules`). By defining this, you can limit or add additional directories where to search.

    "bobril": {
        "testDirectories": [ "spec" ]
    }

## Warnings As Errors

    "bobril": {
        "warningsAsErrors": true
    }

## Ignore some Warnings and Errors

    "bobril": {
        "ignoreDiagnostic": [ -1, -8 ]
    }

## How to enable generating of **sprites.ts** from all pngs in assets directory

    "bobril": {
        "plugins": {
            "bb-assets-generator-plugin": {
                "generateSpritesFile": true
            }
        }
    }

## How to mark module as obsolete

    "bobril": {
        "obsolete": "Reason why is obsolete and what to use instead"
    }

Use this comment in source code with import to ignore this specific import (must be before last import in source file)

    // BBIgnoreObsolete: modulea, moduleb

## How to add additional assets

    "bobril": {
        "assets": {
            "original/source/path/name.ext": "distName.ext"
        }
    }

# Environmental variables

## Disable yarn creating links

Docker on Windows filesystem has limitation in creating links. To workaround this issue create environment variable `BBCoreNoLinks` with not empty value so bbcore will add `--no-bin-links` parameter to yarn command line.
