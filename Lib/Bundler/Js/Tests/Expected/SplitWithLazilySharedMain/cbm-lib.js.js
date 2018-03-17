!function(e) {
    "use strict";
    (function(o, r) {
        var n = __bbb, t = n[r];
        return t !== e ? t instanceof Promise ? t : Promise.resolve(t) : (t = new Promise(function(i, c) {
            var u = document.createElement("script"), a = setTimeout(l, 12e4);
            function l() {
                u.onload = u.onerror = e, clearTimeout(a), n[r] === t ? (n[r] = e, c(Error("Fail to load " + o))) : i(n[r]);
            }
            u.charset = "utf-8", u.onload = u.onerror = l, u.src = o, document.head.appendChild(u);
        }), n[r] = t);
    })(e, "b").then(function(e) {
        return e.shared();
    });
    function o() {
        return "Hello";
    }
    __bbb.a = {
        hello: o
    };
}();