"use strict";
function liveReloadWaiter() {
    var xhr = new XMLHttpRequest();
    xhr.open("GET", "./bb/api/livereload/##Idx##", true);
    xhr.onabort = function () {
        liveReloadWaiter();
    };
    xhr.onreadystatechange = function () {
        if (xhr.readyState === 4) {
            if (xhr.status < 200 || xhr.status >= 300) {
                liveReloadWaiter();
            }
            else {
                window.location.reload(true);
            }
        }
    };
    xhr.send();
}
liveReloadWaiter();
