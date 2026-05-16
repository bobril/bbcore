"use strict";
exports.getDescriptor = void 0;
// @ts-ignore
var ex = require("External");
function getDescriptor() {
    return { asd: ex.value };
}
exports.getDescriptor = getDescriptor;
