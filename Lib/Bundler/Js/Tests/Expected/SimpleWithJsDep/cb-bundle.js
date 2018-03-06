console.log("I am dependency"), function(undefined) {
    "use strict";
    function hello() {
        return "Hello";
    }
    console.log(hello());
}();