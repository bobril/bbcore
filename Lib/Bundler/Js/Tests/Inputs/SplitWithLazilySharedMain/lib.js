"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
Promise.resolve().then(function () { return require("./shared"); }).then(function (shared) { return shared.shared(); });
function hello() {
    return "Hello";
}
exports.hello = hello;
