!function(o) {
    "use strict";
    var n, f, c, l, e, t, r, u;
    function s() {
        return {
            f1: function() {
                return "a";
            },
            f2: function() {
                return "b";
            },
            f3: function() {
                return "c";
            }
        };
    }
    n = (r = s()).f1, f = r.f2, c = r.f3, l = (u = s()).f1, e = u.f2, t = u.f3, console.log(n()), 
    console.log(f()), console.log(c()), console.log(l()), console.log(e()), console.log(t());
}();