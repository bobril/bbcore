"use strict";
var b = require("bobril");
var g = require("bobril-g11n");
b.init(function () { return (g.t("Normal {1}Bold {p1, time, relativepast} {2}and italic{/2} and back to just bold{/1} backslash \\\\ and number {p2}", { p1: b.now() - 100000, p2: 42, 1:function(__ch__){return b.createElement("b", { style: { fontSize: 5 } },
        __ch__)}, 2:function(__ch__){return b.createElement("i", null, __ch__)} })); });
//# sourceMappingURL=index.js.map
