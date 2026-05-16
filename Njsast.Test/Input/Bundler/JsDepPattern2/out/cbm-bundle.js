var window, phoenix;

if (typeof module !== "undefined" && module.exports) {
    window = window || {};
    phoenix = phoenix || {};
    phoenix.json = phoenix.json || {};
    module.exports = phoenix.json;
}

(function(o, n) {
    "use strict";
    window.phoenix = window.phoenix || {};
    phoenix.json = phoenix.json || {};
    phoenix.json.X = !1;
    o = phoenix.json;
    o.doIt = function() {
        console.log("Ok");
    };
})();

(() => {
    function o() {
        phoenix.json.doIt();
    }
    o();
})();

