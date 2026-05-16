(() => {
    var o = {};
    (function(o) {
        o[o["A"] = 0] = "A";
        o[o["B"] = 1] = "B";
    })(o);
    console.log(0);
    console.log(o[1]);
    console.log("No");
})();

