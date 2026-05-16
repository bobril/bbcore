"use strict";
var __c0v = new Uint32Array(4);

globalThis.__c0v = __c0v;

function __c0vS(i) {
    __c0v[i]++;
}

function __c0vC(r, i) {
    __c0v[i + (r ? 1 : 0)]++;
    return r;
}

var _a;

exports.s = void 0;

var b = require("bobril");

__c0vS(0);

var k = "k";

exports.s = b.styleDef({
    color: "blue"
}, (__c0vS(1), _a = {}, __c0vS(2), _a[".".concat(k)] = {
    color: "red"
}, __c0vS(3), _a), "name");

//# sourceMappingURL=cov.js.map
