(() => {
    var exports_bobril, exports_lib2;
    function Url(a) {
        console.log("function Url:" + a);
    }
    Url.hello = function() {
        console.log("Hello");
    };
    exports_bobril = Url;
    exports_lib2 = {
        test: function() {
            console.log(new exports_bobril(1));
        }
    };
    exports_lib2.test();
    function callURL() {
        URL.createObjectURL("");
    }
    callURL();
})();

