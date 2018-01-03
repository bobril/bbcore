var __bbb={};!function(undefined) {
    "use strict";
    var __import = function(url, prop) {
        var res = __bbb[prop];
        return res !== undefined ? res instanceof Promise ? res : Promise.resolve(res) : __bbb[prop] = new Promise(function(r, e) {
            var script = document.createElement("script");
            script.type = "text/javascript", script.charset = "utf-8", script.onload = function() {
                r(__bbb[prop]);
            }, script.onerror = function(_ev) {
                e("Failed to load " + url);
            }, script.src = url, document.head.appendChild(script);
        });
    };
    __import("shared.js", "a").then(function() {
        return __import("lib.js", "b");
    }).then(function(lib) {
        console.log(lib.hello());
    }), __import("shared.js", "a").then(function() {
        return __import("lib2.js", "c");
    }).then(function(lib) {
        console.log(lib.world());
    });
}();