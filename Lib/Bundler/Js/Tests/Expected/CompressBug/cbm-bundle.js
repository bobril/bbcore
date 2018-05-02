!function(n) {
    "use strict";
    var e = /([A-Z])/g, o = /^ms-/;
    function t(r) {
        return "cssFloat" === r ? "float" : r.replace(e, "-$1").toLowerCase().replace(o, "-ms-");
    }
    function r(r) {
        var e = "";
        for (var o in r) {
            var a = r[o];
            a !== n && (e += t(o) + ":" + ("" === a ? '""' : a) + ";");
        }
        return e = e.slice(0, -1);
    }
    console.log(r({
        a: 1,
        b: 2
    }));
}();