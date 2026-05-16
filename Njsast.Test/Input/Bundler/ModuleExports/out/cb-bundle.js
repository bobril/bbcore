(() => {
    var global, exports, exports_wrapper, exports_lib;
    global = window;
    exports = {
        doIt: function(p) {
            console.log(p);
        },
        dontIt: function() {
            var window = "KO";
            global.console.log(window);
        }
    };
    exports_wrapper = function(param) {
        Object.keys(param).forEach(function(name) {
            var orig = param[name];
            param[name] = function(p) {
                orig(name + ":" + p);
            };
        });
        return param;
    };
    exports_lib = exports_wrapper(exports);
    "test" in window && setTimeout(window.test, 1);
    exports_lib.doIt("Ok");
})();

