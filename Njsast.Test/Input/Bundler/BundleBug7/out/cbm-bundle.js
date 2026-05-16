(e => {
    var s = 60 * 1e3, n, t, i;
    function o(e) {
        i = e;
    }
    n = function() {
        i || o(setInterval(function() {}, s));
    };
    t = function() {
        if (i) {
            clearInterval(i);
            o(e);
        }
    };
    n();
    console.log("working");
    t();
})();

