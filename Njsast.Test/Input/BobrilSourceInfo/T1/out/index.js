"use strict";
var b = require("bobril");
var g = require("bobril-g11n");
b.init(function () { return (g.t("Before{1/}{p1}", { p1: "param1", 1:function(){return b.createElement("hr", null)} })); });
//# sourceMappingURL=index.js.map
