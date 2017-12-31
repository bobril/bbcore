!function(r) {
    "use strict";
    var e = /([A-Z])/g, o = /^ms-/;
    function a(r) {
        return "cssFloat" === r ? "float" : r.replace(e, "-$1").toLowerCase().replace(o, "-ms-");
    }
    function n(e) {
        var o = "";
        for (var n in e) {
            var t = e[n];
            t !== r && (o += a(n) + ":" + ("" === t ? '""' : t) + ";");
        }
        return o = o.slice(0, -1);
    }
    console.log(n({
        a: 1,
        b: 2
    }));
}();