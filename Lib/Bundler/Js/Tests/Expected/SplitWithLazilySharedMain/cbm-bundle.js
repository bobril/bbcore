var __bbb={};!function(n) {
    "use strict";
    var e = function(e, o) {
        var t = __bbb[o];
        return t !== n ? t instanceof Promise ? t : Promise.resolve(t) : __bbb[o] = new Promise(function(n, t) {
            var r = document.createElement("script");
            r.type = "text/javascript", r.charset = "utf-8", r.onload = function() {
                n(__bbb[o]);
            }, r.onerror = function(n) {
                t("Failed to load " + e);
            }, r.src = e, document.head.appendChild(r);
        });
    };
    function o() {
        console.log("shared");
    }
    function t() {
        return "unused";
    }
    o(), e("lib.js", "a").then(function(n) {
        console.log(n.hello());
    }), __bbb.b = {
        shared: o,
        unused: t
    };
}();