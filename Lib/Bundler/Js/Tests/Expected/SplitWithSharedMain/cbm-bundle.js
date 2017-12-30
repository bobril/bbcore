var __bbb={};!function() {
    "use strict";
    var o = function(o, e) {
        var n = __bbb[e];
        return void 0 !== n ? n instanceof Promise ? n : Promise.resolve(n) : __bbb[e] = new Promise(function(n, t) {
            var r = document.createElement("script");
            r.type = "text/javascript", r.charset = "utf-8", r.onload = function() {
                n(__bbb[e]);
            }, r.onerror = function(e) {
                t("Failed to load " + o);
            }, r.src = o, document.head.appendChild(r);
        });
    };
    function e() {
        console.log("shared");
    }
    e(), o("lib.js", "a").then(function(o) {
        console.log(o.hello());
    }), __bbb.b = e;
}();