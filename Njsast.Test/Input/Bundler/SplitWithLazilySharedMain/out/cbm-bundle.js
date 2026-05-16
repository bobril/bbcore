var __bbb = {};

(r => {
    var e, o;
    e = function(e, o) {
        var n, t;
        n = __bbb;
        t = n[o];
        if (t !== r) {
            if (t instanceof Promise) return t;
            return Promise.resolve(t);
        }
        t = new Promise(function(i, s) {
            var b, u;
            b = document.createElement("script");
            u = setTimeout(p, 12e4);
            function p() {
                b.onload = b.onerror = r;
                clearTimeout(u);
                if (n[o] === t) {
                    n[o] = r;
                    s(new Error("Fail to load " + e));
                } else i(n[o]);
            }
            b.charset = "utf-8";
            b.onload = b.onerror = p;
            b.src = e;
            document.head.appendChild(b);
        });
        return n[o] = t;
    };
    function n() {
        console.log("shared");
    }
    function t() {
        return "unused";
    }
    o = {
        shared: n,
        unused: t
    };
    n();
    e("cbm-lib.js", "a").then(function(r) {
        console.log(r.hello());
    });
    __bbb.b = o;
})();

