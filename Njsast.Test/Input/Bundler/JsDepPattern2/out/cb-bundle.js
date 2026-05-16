var window, phoenix;

if (typeof module !== "undefined" && module.exports) {
    window = window || {};
    phoenix = phoenix || {};
    phoenix.json = phoenix.json || {};
    module.exports = phoenix.json;
}

(function(p, s) {
    "use strict";
    window.phoenix = window.phoenix || {};
    phoenix.json = phoenix.json || {};
    phoenix.json.X = !1;
    p = phoenix.json;
    p.doIt = function() {
        console.log("Ok");
    };
})();

(() => {
    function hello() {
        phoenix.json.doIt();
    }
    hello();
})();

