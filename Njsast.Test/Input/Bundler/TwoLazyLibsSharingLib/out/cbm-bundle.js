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
        n = new Promise(function(i, b) {
            var s, c;
            s = document.createElement("script");
            c = setTimeout(p, 12e4);
            function p() {
                s.onload = s.onerror = r;
                clearTimeout(c);
                if (t[o] === n) {
                    t[o] = r;
                    b(new Error("Fail to load " + e));
                } else i(t[o]);
            }
            s.charset = "utf-8";
            s.onload = s.onerror = p;
            s.src = e;
            document.head.appendChild(s);
        });
        return t[o] = n;
    };
    e("cbm-shared.js", "a").then(function() {
        return e("cbm-lib.js", "b");
    }).then(function(r) {
        console.log(r.hello());
    });
    e("cbm-shared.js", "a").then(function() {
        return e("cbm-lib2.js", "c");
    }).then(function(r) {
        console.log(r.world());
    });
})();

