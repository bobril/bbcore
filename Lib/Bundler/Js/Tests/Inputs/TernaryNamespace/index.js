"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var liba = require("./liba");
var libb = require("./libb");
var lib = Math.random() > 0.5 ? liba : libb;
console.log(lib.fn(1, 2));
