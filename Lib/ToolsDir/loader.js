"use strict";
const R = function (name, fnOrJson) {
    R.m.set(name.toLowerCase(), typeof fnOrJson == "function" ? { fn: fnOrJson, exports: undefined } : { exports: fnOrJson });
};
globalThis.R = R;
R.t = this;
R.m = new Map();
R.r = function (name, parent) {
    let p = name;
    if (p[0] === ".") {
        let parts = parent ? parent.split("/") : [];
        parts.push("..");
        parts = parts.concat(p.split("/"));
        const newParts = [];
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
        if ((lp = R.map[p.toLowerCase()]))
            p = lp;
    }
    else {
        let parts = name.split("/");
        if (parts.length >= 2) {
            if (parts[0].charCodeAt(0) == 64) {
                parts[0] = parts[0] + "/" + parts[1];
                parts.splice(1, 1);
            }
        }
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
    var lp2;
    if (m == null && (lp2 = lp.replace(/\.[mc]?[jt]sx?$/, "")) != lp) {
        m = R.m.get(lp2);
    }
    if (m == null) {
        m = R.m.get(lp + "/index");
        if (m != null)
            p = p + "/index";
    }
    if (m == null)
        throw new Error("Module " + name + " in " + (parent || "/") + " not registered");
    if (m.exports !== undefined)
        return m.exports;
    m.exports = {};
    m.fn.call(R.t, (name) => R.r(name, p), m, m.exports, R.t);
    if ((typeof m.exports === "function" || typeof m.exports === "object") && !("default" in m.exports)) {
        try {
            Object.defineProperty(m.exports, "default", { value: m.exports, enumerable: false });
        }
        catch (_a) {
        }
    }
    return m.exports;
};
