"use strict";
var R = function (name, fn) {
    R.m.set(name.toLowerCase(), { fn: fn, exports: undefined });
};
R.t = this;
R.m = new Map();
R.r = function (name, parent) {
    var p = name;
    if (p[0] === ".") {
        var parts = parent ? parent.split("/") : [];
        parts.push("..");
        parts = parts.concat(p.split("/"));
        var newParts = [];
        for (var i = 0, l = parts.length; i < l; i++) {
            var part = parts[i];
            if (!part || part === ".")
                continue;
            if (part === "..")
                newParts.pop();
            else
                newParts.push(part);
        }
        p = newParts.join("/");
    }
    else {
        var parts = name.split("/");
        if (parts.length <= 1) {
            p = R.map[name.toLowerCase()];
        }
        else {
            p = parts[0];
            if ((parts[0] = R.map[p.toLowerCase() + "/"]) == null) {
                throw new Error("Directory for module " + p + " in " + (parent || "/") + " not registered");
            }
            p = parts.join("/");
        }
    }
    var lp = p.toLowerCase();
    var m = R.m.get(lp);
    if (m == null && /\.js$/.test(lp)) {
        m = R.m.get(lp.substr(0, lp.length - 3));
    }
    if (m == null)
        throw new Error("Module " + name + " in " + (parent || "/") + " not registered");
    if (m.exports !== undefined)
        return m.exports;
    m.exports = {};
    m.fn.call(R.t, function (name) { return R.r(name, p); }, m, m.exports, R.t);
    return m.exports;
};
