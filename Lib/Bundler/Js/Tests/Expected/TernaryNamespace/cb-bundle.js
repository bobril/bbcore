!function(undefined) {
    "use strict";
    function fn(a, b) {
        return a + b;
    }
    function fn_libb(a, b) {
        return a - b;
    }
    var lib = .5 < Math.random() ? {
        fn: fn
    } : {
        fn: fn_libb
    };
    console.log(lib.fn(1, 2));
}();