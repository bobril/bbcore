var __bbb={};!function(a) {
    "use strict";
    var e = function(u, c) {
        var i = __bbb, s = i[c];
        return s !== a ? s instanceof Promise ? s : Promise.resolve(s) : (s = new Promise(function(e, o) {
            var n = document.createElement("script"), r = setTimeout(t, 12e4);
            function t() {
                n.onload = n.onerror = a, clearTimeout(r), i[c] === s ? (i[c] = a, o(Error("Fail to load " + u))) : e(i[c]);
            }
            n.charset = "utf-8", n.onload = n.onerror = t, n.src = u, document.head.appendChild(n);
        }), i[c] = s);
    };
    function o() {
        console.log("shared");
    }
    function n() {
        return "unused";
    }
    o(), e("lib.js", "a").then(function(e) {
        console.log(e.hello());
    }), __bbb.b = {
        shared: o,
        unused: n
    };
}();