(() => {
    var e, t, n;
    e = function() {
        function e() {}
        e.prototype.clear = function() {
            t = new e();
        };
        return e;
    }();
    t = new e();
    n = new e();
    n.clear();
    t.clear();
})();

