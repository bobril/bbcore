"use strict";
function liveReloadWaiter() {
    var _this = this;
    var xhr = new window.XMLHttpRequest();
    xhr.open("GET", "./bb/api/livereload/##Idx##", true);
    xhr.onabort = function () {
        _this.close();
        liveReloadWaiter();
    };
    xhr.onreadystatechange = function () {
        if (xhr.readyState === 4) {
            if (xhr.status < 200 || xhr.status >= 300) {
                _this.close();
                liveReloadWaiter();
            }
            else {
                _this.close();
                window.location.reload(true);
                return;
            }
        }
    };
    xhr.send();
}
liveReloadWaiter();
