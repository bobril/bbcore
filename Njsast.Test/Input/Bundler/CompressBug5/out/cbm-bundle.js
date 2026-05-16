(() => {
    var e;
    function r(e) {
        console.log(e);
    }
    e = r;
    (function(e) {
        e.onExtraToken = (e => {});
    })(r = e || (e = {}));
    e("test");
})();

