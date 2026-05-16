"use strict";
var shared = require("./shared");
var allshared = shared;
allshared.shared();
function hello() {
    return "Hello";
}
exports.hello = hello;
