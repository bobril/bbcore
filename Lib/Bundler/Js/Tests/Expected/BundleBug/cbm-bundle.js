!function(n) {
    "use strict";
    var t = n;
    function c(n) {
        t = n;
    }
    c(function() {}), t && t();
}();