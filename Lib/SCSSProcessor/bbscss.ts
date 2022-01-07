var exports = {};

declare const bb: IBB;

interface IBB {
    urlReplace(url: string, from: string): string;
    load(url: string, from: string): string;
    finish(result: string): void;
    fail(result: string): void;
    log(text: string): void;
}

function processCss(source: string, from: string, callback: (url: string, from: string) => string): PromiseLike<any> {
    return postcss([
        postcssUrl({
            url: (asset: { url: string }, dir: { from: string }) => {
                if (asset.url.startsWith("data:")) return asset.url;
                return callback(asset.url, dir.from);
            },
        }),
    ]).process(source, { from });
}

function concatenateCssAndMinify(
    inputs: { source: string; from: string }[],
    callback: (url: string, from: string) => string
): Promise<any> {
    return Promise.all<any>(
        inputs.map((i) => {
            return processCss(i.source, i.from, callback);
        })
    ).then((results) => {
        let r = results[0].root;
        for (let i = 1; i < results.length; i++) {
            r = r.append(results[i].root);
        }
        return postcss([
            cssnano({
                safe: true,
            }),
        ]).process(r.toResult());
    });
}

function bbProcessScss(source: string, from: string) {
    exports
        .processCss(source, from, (url, from) => bb.urlReplace(url, from))
        .then(
            (v) => bb.finish(v.css),
            (e: Error) => bb.fail(e.message + " " + e.stack)
        );
}

function bbConcatAndMinify(inputs: string) {
    var inp = JSON.parse(inputs);
    concatenateCssAndMinify(inp, (url, from) => bb.urlReplace(url, from)).then(
        (v) => bb.finish(v.css),
        (e: Error) => bb.fail(e.message + " " + e.stack)
    );
}

function bbInit() {
    exports.load({});
}
function require(s: string): any {
    bb.log(s);
    return {};
}
var global = globalThis;
