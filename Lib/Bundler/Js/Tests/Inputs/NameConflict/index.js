"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var lib_1 = require("./lib");
function fn(a, b) {
    return a - b;
}
var a = 2;
console.log(lib_1.fn(fn(a, 1), a));
