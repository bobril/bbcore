"use strict";
var a = require("./lib");
var ns;
(function (ns) {
    function fun() {
        a.fun();
    }
    ns.fun = fun;
})(ns = exports.ns || (exports.ns = {}));
