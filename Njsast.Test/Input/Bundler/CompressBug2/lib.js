"use strict";
exports.now = Date.now;
function update(time) {
    exports.now();
}
function init() {
    exports.now();
}
exports.init = init;
