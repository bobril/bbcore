!function(l) {
    "use strict";
    function e() {
        return "Hello";
    }
    (function(i, c) {
        var u = __bbb, a = u[c];
        return a !== l ? a instanceof Promise ? a : Promise.resolve(a) : (a = new Promise(function(e, o) {
            var r = document.createElement("script"), n = setTimeout(t, 12e4);
            function t() {
                r.onload = r.onerror = l, clearTimeout(n), u[c] === a ? (u[c] = l, o(Error("Fail to load " + i))) : e(u[c]);
            }
            r.charset = "utf-8", r.onload = r.onerror = t, r.src = i, document.head.appendChild(r);
        }), u[c] = a);
    })(l, "b").then(function(e) {
        return e.shared();
    }), __bbb.a = {
        hello: e
    };
}();