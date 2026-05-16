"use strict";
var b = require("bobril");
var g = require("bobril-g11n");
b.init(function () {
    var param = {
        p1: "Visit page"
    };
    var param2 = {
        p2: "Bobril"
    };
    return (b.createElement(g.T, __assign({ hint: "hint" }, param, param2),
        g.t("{p1}"),
        " of ",
        b.createElement("a", { href: "https://bobril.com" }, g.t("{p2}")),
        " ",
        b.createElement("img", { src: b.asset("./logo.png") })));
});
//# sourceMappingURL=index.js.map