"use strict";
var R = function(name, fn) {
    R.m[name.toLowerCase()] = { fn: fn, exports: undefined };
};
R.t = this;
R.m = Object.create(null);
R.r = function(name, parent) {
    var p = R.map[name.toLowerCase()];
    if (p === undefined) p = name;
    if (p[0] === ".") {
        var parts = parent ? parent.split("/") : [];
        parts.push("..");
        parts = parts.concat(p.split("/"));
        var newParts = [];
        for (var i = 0, l = parts.length; i < l; i++) {
            var part = parts[i];
            if (!part || part === ".") continue;
            if (part === "..") newParts.pop();
            else newParts.push(part);
        }
        p = newParts.join("/");
    }
    var lp = p.toLowerCase();
    var m = R.m[lp];
    if (m == null && /\.js$/.test(lp)) {
        m = R.m[lp.substr(0, lp.length-3)];
    }
    if (m == null) throw new Error("Module " + name + " in " + (parent || "/") + " not registered");
    if (m.exports !== undefined) return m.exports;
    m.exports = {};
    m.fn.call(
        R.t,
        function(name) {
            return R.r(name, p);
        },
        m,
        m.exports,
        R.t
    );
    return m.exports;
};
