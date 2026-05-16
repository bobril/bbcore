(() => {
    var a_index = 2;
    function fn(a, b) {
        return a + b;
    }
    function fn_index(a, b) {
        return a - b;
    }
    console.log(fn(fn_index(a_index, 1), a_index));
})();

