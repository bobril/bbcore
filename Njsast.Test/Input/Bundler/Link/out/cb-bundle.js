(() => {
    function link_m(name) {
        return name;
    }
    function doit() {
        var link = link_m("hello");
        console.log(link);
    }
    doit();
})();

