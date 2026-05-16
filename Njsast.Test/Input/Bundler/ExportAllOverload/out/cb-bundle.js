(() => {
    function fn() {
        return "OK1";
    }
    function fn_lib2() {
        fn();
        return "OK2";
    }
    console.log(fn_lib2());
})();

