var __bbb = {};

(undefined => {
    var __import, __export_$;
    __import = function(url, prop) {
        var bbb, res;
        bbb = __bbb;
        res = bbb[prop];
        if (res !== undefined) {
            if (res instanceof Promise) return res;
            return Promise.resolve(res);
        }
        res = new Promise(function(r, e) {
            var script, timeout;
            script = document.createElement("script");
            timeout = setTimeout(handle, 12e4);
            function handle() {
                script.onload = script.onerror = undefined;
                clearTimeout(timeout);
                if (bbb[prop] === res) {
                    bbb[prop] = undefined;
                    e(new Error("Fail to load " + url));
                } else r(bbb[prop]);
            }
            script.charset = "utf-8";
            script.onload = script.onerror = handle;
            script.src = url;
            document.head.appendChild(script);
        });
        return bbb[prop] = res;
    };
    function shared() {
        console.log("shared");
    }
    function unused() {
        return "unused";
    }
    __export_$ = {
        shared,
        unused
    };
    shared();
    __import("cb-lib.js", "a").then(function(lib) {
        console.log(lib.hello());
    });
    __bbb.b = __export_$;
})();

