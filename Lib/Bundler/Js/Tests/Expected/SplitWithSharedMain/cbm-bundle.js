var __bbb={};!function(e) {
    "use strict";
    var n = function(n, o) {
        var t = __bbb[o];
        return t !== e ? t instanceof Promise ? t : Promise.resolve(t) : __bbb[o] = new Promise(function(e, t) {
            var r = document.createElement("script");
            r.type = "text/javascript", r.charset = "utf-8", r.onload = function() {
                e(__bbb[o]);
            }, r.onerror = function(e) {
                t("Failed to load " + n);
            }, r.src = n, document.head.appendChild(r);
        });
    };
    function o() {
        console.log("shared");
    }
    o(), n("lib.js", "a").then(function(e) {
        console.log(e.hello());
    }), __bbb.b = o;
}();