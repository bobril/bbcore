(() => {
    var __export_$, __export_$_libb, lib;
    function fn(a, b) {
        return a + b;
    }
    __export_$ = {
        fn
    };
    function fn_libb(a, b) {
        return a - b;
    }
    __export_$_libb = {
        fn: fn_libb
    };
    lib = Math.random() > .5 ? __export_$ : __export_$_libb;
    console.log(lib.fn(1, 2));
})();

