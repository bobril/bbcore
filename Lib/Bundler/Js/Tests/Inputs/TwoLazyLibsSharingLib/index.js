"use strict";
Promise.resolve().then(function () { return require("./lib"); }).then(function (lib) {
    console.log(lib.hello());
});
Promise.resolve().then(function () { return require("./lib2"); }).then(function (lib) {
    console.log(lib.world());
});
