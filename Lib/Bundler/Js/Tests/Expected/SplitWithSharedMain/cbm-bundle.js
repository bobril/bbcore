var __bbb={};!function(s) {
    "use strict";
    var o = function(c, i) {
        var a = __bbb, l = a[i];
        return l !== s ? l instanceof Promise ? l : Promise.resolve(l) : (l = new Promise(function(o, e) {
            var n = document.createElement("script"), r = setTimeout(t, 12e4);
            function t() {
                n.onload = n.onerror = s, clearTimeout(r), a[i] === l ? (a[i] = s, e(Error("Fail to load " + c))) : o(a[i]);
            }
            n.charset = "utf-8", n.onload = n.onerror = t, n.src = c, document.head.appendChild(n);
        }), a[i] = l);
    };
    function e() {
        console.log("shared");
    }
    e(), o("lib.js", "a").then(function(o) {
        console.log(o.hello());
    }), __bbb.b = e;
}();