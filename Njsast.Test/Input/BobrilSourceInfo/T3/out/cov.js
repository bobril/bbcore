"use strict";
var __c0v = new Uint32Array(2);

globalThis.__c0v = __c0v;

function __c0vS(i) {
    __c0v[i]++;
}

function __c0vC(r, i) {
    __c0v[i + (r ? 1 : 0)]++;
    return r;
}

var b = require("bobril");

var g = require("bobril-g11n");

__c0vS(0);

b.init(function() {
    __c0vS(1);
    return b.createElement(g.T, {
        hint: "Leave things like '{1}' on appropriate places",
        p1: b.now() - 1e5,
        p2: 42
    }, "Normal", " ", b.createElement("b", {
        style: {
            fontSize: 5
        }
    }, "Bold ", g.t("{p1, time, relativepast}"), " ", b.createElement("i", null, "and italic"), " and back to just bold"), " ", "backslash \\ and number ", g.t("{p2}"));
});

//# sourceMappingURL=cov.js.map
