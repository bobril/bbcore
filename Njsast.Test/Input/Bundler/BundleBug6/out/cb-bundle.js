(() => {
    var __export_isArray, isArray2;
    __export_isArray = Array.isArray;
    isArray2 = __export_isArray;
    function setArray2(value) {
        isArray2 = value;
    }
    function test() {
        console.log(__export_isArray([]));
        console.log(isArray2([]));
    }
    setArray2(function(a) {
        return __export_isArray(a);
    });
    console.log(__export_isArray([]));
    test();
})();

