type ModuleFun = (
    this: typeof globalThis,
    require: (name: string) => any,
    module: { exports: any },
    exports: any,
    global: typeof globalThis
) => void;
interface IR {
    (name: string, fn: ModuleFun): void;
    t: typeof globalThis;
    m: Map<string, { fn?: ModuleFun; exports: any }>;
    r(name: string, parent: string): any;
    map?: { [name: string]: string };
}
const R: IR = function(name: string, fnOrJson: ModuleFun | any) {
    R.m.set(
        name.toLowerCase(),
        typeof fnOrJson == "function" ? { fn: fnOrJson, exports: undefined } : { exports: fnOrJson }
    );
};
R.t = this;
R.m = new Map();
R.r = function(name: string, parent: string) {
    let p = name;
    if (p[0] === ".") {
        let parts = parent ? parent.split("/") : [];
        parts.push("..");
        parts = parts.concat(p.split("/"));
        const newParts = [];
        for (var i = 0, l = parts.length; i < l; i++) {
            var part = parts[i];
            if (!part || part === ".") continue;
            if (part === "..") newParts.pop();
            else newParts.push(part);
        }
        p = newParts.join("/");
        if ((lp = R.map![p.toLowerCase()])) p = lp;
    } else {
        let parts = name.split("/");
        if (parts.length >= 2) {
            if (parts[0].charCodeAt(0) == 64) {
                parts[0] = parts[0] + "/" + parts[1];
                parts.splice(1, 1);
            }
        }
        if (parts.length <= 1) {
            p = R.map![name.toLowerCase()];
        } else {
            p = parts[0];
            if ((parts[0] = R.map![p.toLowerCase() + "/"]) == null) {
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
    if (m == null) throw new Error("Module " + name + " in " + (parent || "/") + " not registered");
    if (m.exports !== undefined) return m.exports;
    m.exports = {};
    m.fn!.call(R.t, (name: string) => R.r(name, p), m, m.exports, R.t);
    if ((typeof m.exports === "function" || typeof m.exports === "object") && !("default" in m.exports)) {
        try {
            Object.defineProperty(m.exports, "default", { value: m.exports, enumerable: false });
        } catch {}
    }
    return m.exports;
};
