!function(n) {
    "use strict";
    function t() {
        return new Image();
    }
    new (function() {
        function n() {
            console.log("constructed");
        }
        return n;
    }())(), t();
}();