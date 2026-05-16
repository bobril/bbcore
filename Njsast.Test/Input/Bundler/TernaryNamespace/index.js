"use strict";
var liba = require("./liba");
var libb = require("./libb");
var lib = Math.random() > 0.5 ? liba : libb;
console.log(lib.fn(1, 2));
