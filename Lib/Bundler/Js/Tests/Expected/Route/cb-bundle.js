!function(undefined) {
    "use strict";
    function route(param) {
        return param;
    }
    function link(name) {
        return name;
    }
    var __export_page = "OK";
    route("KO");
    function doit() {
        var link_index = link("hello");
        console.log(link_index);
    }
    doit(), console.log(__export_page);
}();