var __bbb = {};

(r => {
    var e;
    e = function(e, o) {
        var t, n;
        t = __bbb;
        n = t[o];
        if (n !== r) {
            if (n instanceof Promise) return n;
            return Promise.resolve(n);
        }
        n = new Promise(function(i, s) {
            var b, p;
            b = document.createElement("script");
            p = setTimeout(c, 12e4);
            function c() {
                b.onload = b.onerror = r;
                clearTimeout(p);
                if (t[o] === n) {
                    t[o] = r;
                    s(new Error("Fail to load " + e));
                } else i(t[o]);
            }
            b.charset = "utf-8";
            b.onload = b.onerror = c;
            b.src = e;
            document.head.appendChild(b);
        });
        return t[o] = n;
    };
    function o() {
        console.log("shared");
    }
    o();
    e("cbm-lib.js", "a").then(function(r) {
        console.log(r.hello());
    });
    __bbb.b = o;
})();

