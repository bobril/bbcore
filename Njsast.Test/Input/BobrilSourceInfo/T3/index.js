"use strict";
var b = require("bobril");
var g = require("bobril-g11n");
b.init(function () { return (b.createElement(g.T, { hint: "Leave things like '{1}' on appropriate places", p1: b.now() - 100000, p2: 42 },
    "Normal",
    " ",
    b.createElement("b", { style: { fontSize: 5 } },
        "Bold ",
        g.t("{p1, time, relativepast}"),
        " ",
        b.createElement("i", null, "and italic"),
        " and back to just bold"),
    " ",
    "backslash \\ and number ",
    g.t("{p2}"))); });
//# sourceMappingURL=index.js.map