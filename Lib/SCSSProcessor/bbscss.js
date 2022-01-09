"use strict";
class URL {
    constructor(url, base) {
        if (base == undefined)
            base = "";
        if (base instanceof URL)
            base = base.href;
        this._url = bb.join(base, url);
    }
    get href() {
        return this._url;
    }
    set href(v) {
        this._url = v;
    }
    get origin() {
        return "";
    }
    get protocol() {
        return "file:";
    }
    set protocol(_v) {
        throw new Error("not implemented");
    }
    get username() {
        return "";
    }
    set username(_v) { }
    get password() {
        return "";
    }
    set password(_v) { }
    get host() {
        return "";
    }
    set host(_v) {
        throw new Error("not implemented");
    }
    get hostname() {
        return "";
    }
    set hostname(_v) {
        throw new Error("not implemented");
    }
    get port() {
        return "";
    }
    set port(_v) {
        throw new Error("not implemented");
    }
    get pathname() {
        throw new Error("not implemented");
    }
    set pathname(_v) {
        throw new Error("not implemented");
    }
    get search() {
        return "";
    }
    set search(_v) { }
    get searchParams() {
        throw new Error("not implemented");
    }
    get hash() {
        return "";
    }
    set hash(_v) { }
    toJSON() {
        return this.href;
    }
}
var exports = {};
function bbCompileScss(source, from) {
    bb.log(JSON.stringify(Object.keys(exports)));
    try {
        bb.finish(exports.compileString(source, {
            url: new URL(from),
            alertAscii: false,
            alertColor: false,
            quietDeps: false,
            style: "compressed",
            logger: {},
            importer: {
                canonicalize(url, _options) {
                    return new URL(url);
                },
                load(canonicalUrl) {
                    return { contents: bb.load(canonicalUrl.toString()), syntax: "scss" };
                },
            },
        }).css);
    }
    catch (e) {
        bb.fail(e.message);
    }
}
function bbInit() {
    exports.load({});
}
function require(s) {
    if (s == "util") {
        return { inspect: { custom: Symbol() } };
    }
    bb.log(s);
    return {};
}
var global = globalThis;
global.URL = URL;
var process = { env: {} };
