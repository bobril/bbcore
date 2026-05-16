(() => {
    var __export_ns = {};
    function fun_m() {
        console.log("fun");
    }
    (function(ns) {
        function fun() {
            fun_m();
        }
        ns.fun = fun;
    })(__export_ns);
    __export_ns.fun();
})();

