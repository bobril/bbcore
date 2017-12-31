!function(undefined) {
    "use strict";
    function link(name) {
        return name;
    }
    function doit() {
        var link_index = link("hello");
        console.log(link_index);
    }
    doit();
}();