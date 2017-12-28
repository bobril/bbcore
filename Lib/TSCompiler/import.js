var __import = function (url, prop) {
    var res = __bbb[prop];
    if (res !== undefined) {
        if (res instanceof Promise) return res;
        return Promise.resolve(res);
    }
    return __bbb[prop] = new Promise(function (r, e) {
        var script = document.createElement('script');
        script.type = 'text/javascript';
        script.charset = 'utf-8';
        script.onload = function () {
            r(__bbb[prop]);
        };
        script.onerror = function (_ev) {
            e('Failed to load ' + url);
        };
        script.src = url;
        document.head.appendChild(script);
    });
}
