"use strict";
var shared = require("./shared");
shared.shared();
Promise.resolve().then(function () { return require("./lib"); }).then(function (lib) {
    console.log(lib.hello());
});
