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
    return (g.t("{p1} of {1}{p2}{/1} {2/}", __assign({ 1:function(__ch__){return b.createElement("a", { href: "https://bobril.com" }, __ch__)}, 2:function(){return b.createElement("img", { src: b.asset("./logo.png") })} }, param, param2)));
});
//# sourceMappingURL=index.js.map
