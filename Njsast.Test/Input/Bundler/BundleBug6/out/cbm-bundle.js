(() => {
    var r, a;
    r = Array.isArray;
    a = r;
    function _(r) {
        a = r;
    }
    function s() {
        console.log(r([]));
        console.log(a([]));
    }
    _(function(a) {
        return r(a);
    });
    console.log(r([]));
    s();
})();

