(function () {
    var a = console.log;

    var b = a;

    (function () {
        var a = window.location;
        b(a);
    })();
})();
