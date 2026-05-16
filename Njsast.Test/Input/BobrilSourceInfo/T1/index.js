"use strict";
var b = require("bobril");
var g = require("bobril-g11n");
b.init(function () { return (b.createElement(g.T, { p1: "param1" },
    "Before",
    b.createElement("hr", null),
    g.t("{p1}"))); });
//# sourceMappingURL=index.js.map