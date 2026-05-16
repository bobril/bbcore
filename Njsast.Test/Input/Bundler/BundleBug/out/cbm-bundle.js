(d => {
    var _;
    _ = d;
    function e(d) {
        _ = d;
    }
    e(function() {});
    _ && _();
})();

