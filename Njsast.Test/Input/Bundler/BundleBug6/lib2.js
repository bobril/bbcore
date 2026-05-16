"use strict";
exports.test = exports.setArray2 = exports.isArray = void 0;
var lib3_1 = require("./lib3");
var lib3_2 = require("./lib3");
Object.defineProperty(exports, "isArray", { enumerable: true, get: function () { return lib3_2.isArray; } });
var isArray2 = lib3_1.isArray;
function setArray2(value) {
    isArray2 = value;
}
exports.setArray2 = setArray2;
function test() {
    console.log((0, lib3_1.isArray)([]));
    console.log(isArray2([]));
}
exports.test = test;
