var __import = function(url, prop) {
    var bbb = __bbb;
    var res = bbb[prop];
    if (res !== undefined) {
        if (res instanceof Promise) return res;
        return Promise.resolve(res);
    }
    res = new Promise(function(r, e) {
        var script = document.createElement("script");
        var timeout = setTimeout(handle, 120000);
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
    return (bbb[prop] = res);
};
