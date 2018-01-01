!function(undefined) {
    "use strict";
    (function(url, prop) {
        var res = __bbb[prop];
        return res !== undefined ? res instanceof Promise ? res : Promise.resolve(res) : __bbb[prop] = new Promise(function(r, e) {
            var script = document.createElement("script");
            script.type = "text/javascript", script.charset = "utf-8", script.onload = function() {
                r(__bbb[prop]);
            }, script.onerror = function(_ev) {
                e("Failed to load " + url);
            }, script.src = url, document.head.appendChild(script);
        });
    })(undefined, "b").then(function(shared) {
        return shared.shared();
    });
    function hello() {
        return "Hello";
    }
    __bbb.a = {
        hello: hello
    };
}();