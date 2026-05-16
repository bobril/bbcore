console.log("I am dependency");

(() => {
    function hello() {
        return "Hello";
    }
    console.log(hello());
})();

