(() => {
    var t = {}, _;
    (function(t) {
        t[t["Start"] = 0] = "Start";
        t[t["Stop"] = 1] = "Stop";
    })(t);
    function p() {
        console.log(1);
    }
    _ = {
        Opts: t,
        ok: p
    };
    _ != null && p();
})();

