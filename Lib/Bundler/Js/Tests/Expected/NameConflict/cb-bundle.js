!function(undefined) {
    "use strict";
    function fn(a_lib, b) {
        return a_lib + b;
    }
    function fn_index(a_index2, b) {
        return a_index2 - b;
    }
    var a_index = 2;
    console.log(fn(fn_index(a_index, 1), a_index));
}();