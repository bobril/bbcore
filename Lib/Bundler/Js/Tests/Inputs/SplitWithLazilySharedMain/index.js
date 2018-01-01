"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var shared = require("./shared");
shared.shared();
Promise.resolve().then(function () { return require("./lib"); }).then(function (lib) {
    console.log(lib.hello());
});
