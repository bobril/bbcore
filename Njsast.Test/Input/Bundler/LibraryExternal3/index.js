"use strict";
exports.getDescriptor = void 0;
// @ts-ignore
var External_1 = require("External");
// @ts-ignore
var External_2 = require("External");
// @ts-ignore
var External2_1 = require("External2");
function getDescriptor() {
    return { a: External_1.default, b: External_2.ex2, c: External_1.default.ex3, d: External2_1.ex22 };
}
exports.getDescriptor = getDescriptor;
