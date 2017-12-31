!function(undefined) {
    "use strict";
    var __export_fn1, __export_fn2, __export_fn3, __export_fn4, __export_fn5, __export_fn6;
    function reg() {
        return {
            f1: function() {
                return "a";
            },
            f2: function() {
                return "b";
            },
            f3: function() {
                return "c";
            }
        };
    }
    __export_fn1 = (_a = reg()).f1, __export_fn2 = _a.f2, __export_fn3 = _a.f3, __export_fn4 = (_b = reg()).f1, 
    __export_fn5 = _b.f2, __export_fn6 = _b.f3;
    var _a, _b;
    console.log(__export_fn1()), console.log(__export_fn2()), console.log(__export_fn3()), 
    console.log(__export_fn4()), console.log(__export_fn5()), console.log(__export_fn6());
}();