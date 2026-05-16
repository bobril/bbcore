"use strict";
exports.addOne = void 0;
var lib2_1 = require("./lib2");
function addOne(v) {
    return (0, lib2_1.bar)(1, v);
}
exports.addOne = addOne;
console.log((0, lib2_1.bar)(1, 2));
exports.default = lib2_1.bar;
