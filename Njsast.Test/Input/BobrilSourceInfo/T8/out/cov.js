var __c0v = new Uint32Array(0);

globalThis.__c0v = __c0v;

function __c0vS(i) {
    __c0v[i]++;
}

function __c0vC(r, i) {
    __c0v[i + (r ? 1 : 0)]++;
    return r;
}

var g = require("bobril-g11n");

console.log(g.t(`Hello {p1}`, {
    p1: "World"
}));

//# sourceMappingURL=cov.js.map
