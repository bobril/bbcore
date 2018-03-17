var __bbb={};!function(undefined) {
    "use strict";
    var __import = function(url, prop) {
        var bbb = __bbb, res = bbb[prop];
        return res !== undefined ? res instanceof Promise ? res : Promise.resolve(res) : (res = new Promise(function(r, e) {
            var script = document.createElement("script"), timeout = setTimeout(handle, 12e4);
            function handle() {
                script.onload = script.onerror = undefined, clearTimeout(timeout), bbb[prop] === res ? (bbb[prop] = undefined, 
                e(Error("Fail to load " + url))) : r(bbb[prop]);
            }
            script.charset = "utf-8", script.onload = script.onerror = handle, script.src = url, 
            document.head.appendChild(script);
        }), bbb[prop] = res);
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