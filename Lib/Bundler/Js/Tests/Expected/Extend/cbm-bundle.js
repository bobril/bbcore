!function(o) {
    "use strict";
    var t = Object.setPrototypeOf || {
        __proto__: []
    } instanceof Array && function(o, t) {
        o.__proto__ = t;
    } || function(o, t) {
        for (var n in t) t.hasOwnProperty(n) && (o[n] = t[n]);
    }, n = function(o, n) {
        function e() {
            this.constructor = o;
        }
        t(o, n), o.prototype = null === n ? Object.create(n) : (e.prototype = n.prototype, 
        new e());
    }, e = function() {
        function o() {}
        return o.prototype.hello = function() {
            console.log("Base");
        }, o;
    }(), r = e;
    new (function(o) {
        function t() {
            return null !== o && o.apply(this, arguments) || this;
        }
        return n(t, o), t.prototype.hello = function() {
            console.log("Main");
        }, t;
    }(r))().hello();
}();