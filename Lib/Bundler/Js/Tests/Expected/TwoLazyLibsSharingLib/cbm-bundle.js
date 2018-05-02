var __bbb={};!function(l) {
    "use strict";
    var e = function(c, i) {
        var s = __bbb, a = s[i];
        return a !== l ? a instanceof Promise ? a : Promise.resolve(a) : (a = new Promise(function(e, n) {
            var o = document.createElement("script"), r = setTimeout(t, 12e4);
            function t() {
                o.onload = o.onerror = l, clearTimeout(r), s[i] === a ? (s[i] = l, n(Error("Fail to load " + c))) : e(s[i]);
            }
            o.charset = "utf-8", o.onload = o.onerror = t, o.src = c, document.head.appendChild(o);
        }), s[i] = a);
    };
    e("shared.js", "a").then(function() {
        return e("lib.js", "b");
    }).then(function(e) {
        console.log(e.hello());
    }), e("shared.js", "a").then(function() {
        return e("lib2.js", "c");
    }).then(function(e) {
        console.log(e.world());
    });
}();