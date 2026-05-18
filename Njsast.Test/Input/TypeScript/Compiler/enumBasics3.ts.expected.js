"use strict";
// @target: es2015
// @strict: false
var M;
(function (M) {
    let N;
    (function (N) {
        let E1;
        (function (E1) {
            E1[E1["a"] = 1] = "a";
            E1[E1["b"] = E1.a.a] = "b";
        })(E1 = N.E1 || (N.E1 = {}));
    })(N = M.N || (M.N = {}));
})(M || (M = {}));
(function (M) {
    let N;
    (function (N) {
        let E2;
        (function (E2) {
            E2[E2["b"] = 1] = "b";
            E2[E2["c"] = M.N.E1.a.a] = "c";
        })(E2 = N.E2 || (N.E2 = {}));
    })(N = M.N || (M.N = {}));
})(M || (M = {}));
