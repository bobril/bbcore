!function(o) {
    "use strict";
    var e = Object.setPrototypeOf || {
        __proto__: []
    } instanceof Array && function(o, t) {
        o.__proto__ = t;
    } || function(o, t) {
        for (var n in t) t.hasOwnProperty(n) && (o[n] = t[n]);
    }, n = function(o, t) {
        function n() {
            this.constructor = o;
        }
        e(o, t), o.prototype = null === t ? Object.create(t) : (n.prototype = t.prototype, 
        new n());
    }, t = function() {
        function o() {}
        return o.prototype.hello = function() {
            console.log("Base");
        }, o;
    }(), r = t;
    new (function(o) {
        function t() {
            return null !== o && o.apply(this, arguments) || this;
        }
        return n(t, o), t.prototype.hello = function() {
            console.log("Main");
        }, t;
    }(r))().hello();
}();