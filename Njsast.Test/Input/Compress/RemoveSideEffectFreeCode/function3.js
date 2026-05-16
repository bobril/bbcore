"use strict";

function used() {
    console.log("Ok");
}

{
    function used() {
        console.log("Nested");
    }
    used();
}

used();
