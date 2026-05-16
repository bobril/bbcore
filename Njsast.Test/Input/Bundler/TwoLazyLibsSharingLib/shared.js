"use strict";
function shared() {
    console.log("shared");
}
exports.shared = shared;
function unused() {
    return "unused";
}
exports.unused = unused;
