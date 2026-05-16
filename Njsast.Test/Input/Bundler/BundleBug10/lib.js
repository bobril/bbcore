"use strict";
var _a;
exports.A = void 0;
const lib2_1 = require("./lib2");
const kError = Symbol("kError");
const kNext = Symbol("kNext");
class A {
    constructor() {
        this[_a] = "B";
    }
    [(_a = lib2_1.kType, kError)]() {
        throw new Error();
    }
    [kNext]() {
        console.log("next");
    }
}
exports.A = A;
