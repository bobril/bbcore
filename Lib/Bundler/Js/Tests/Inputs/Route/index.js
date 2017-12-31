"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var b = require("./bobril");
var lib = require("./lib");
function doit() {
    var link = b.link("hello");
    console.log(link);
}
doit();
console.log(lib.page);
