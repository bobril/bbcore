!function(o) {
    "use strict";
    var n, f, c, l, e, r;
    function t() {
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
    n = (u = t()).f1, f = u.f2, c = u.f3, l = (s = t()).f1, e = s.f2, r = s.f3;
    var u, s;
    console.log(n()), console.log(f()), console.log(c()), console.log(l()), console.log(e()), 
    console.log(r());
}();