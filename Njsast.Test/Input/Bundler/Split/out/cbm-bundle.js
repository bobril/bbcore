var __bbb = {};

(r => {
    var e;
    e = function(e, o) {
        var t, i;
        t = __bbb;
        i = t[o];
        if (i !== r) {
            if (i instanceof Promise) return i;
            return Promise.resolve(i);
        }
        i = new Promise(function(n, b) {
            var s, p;
            s = document.createElement("script");
            p = setTimeout(c, 12e4);
            function c() {
                s.onload = s.onerror = r;
                clearTimeout(p);
                if (t[o] === i) {
                    t[o] = r;
                    b(new Error("Fail to load " + e));
                } else n(t[o]);
            }
            s.charset = "utf-8";
            s.onload = s.onerror = c;
            s.src = e;
            document.head.appendChild(s);
        });
        return t[o] = i;
    };
    e("cbm-lib.js", "a").then(function(r) {
        console.log(r.hello());
    });
})();

