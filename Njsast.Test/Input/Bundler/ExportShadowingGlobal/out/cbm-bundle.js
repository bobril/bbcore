(() => {
    var e;
    function a() {
        return new Image();
    }
    e = function() {
        function e() {
            console.log("constructed");
        }
        return e;
    }();
    URL.createObjectURL("");
    new e();
    a();
})();

