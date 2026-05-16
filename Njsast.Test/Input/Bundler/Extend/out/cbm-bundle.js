(() => {
    var n, t, e, o;
    n = Object.setPrototypeOf || {
        __proto__: []
    } instanceof Array && function(n, t) {
        n.__proto__ = t;
    } || function(n, t) {
        var e;
        for (e in t) t.hasOwnProperty(e) && (n[e] = t[e]);
    };
    t = function(t, e) {
        n(t, e);
        function o() {
            this.constructor = t;
        }
        t.prototype = e === null ? Object.create(e) : (o.prototype = e.prototype, new o());
    };
    e = function() {
        function n() {}
        n.prototype.hello = function() {
            console.log("Base");
        };
        return n;
    }();
    o = function(n) {
        t(e, n);
        function e() {
            return n !== null && n.apply(this, arguments) || this;
        }
        e.prototype.hello = function() {
            console.log("Main");
        };
        return e;
    }(e);
    new o().hello();
})();

