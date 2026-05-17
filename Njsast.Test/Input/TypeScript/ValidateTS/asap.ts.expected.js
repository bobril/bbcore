"use strict";
exports.asap = void 0;

exports.asap = (() => {
    var callbacks = [];
    function executeCallbacks() {
        var cbList = callbacks;
        callbacks = [];
        for (var i = 0, len = cbList.length; i < len; i++) {
            cbList[i]();
        }
    }
    return callback => {
        callbacks.push(callback);
        if (callbacks.length === 1) {
            Promise.resolve().then(executeCallbacks);
        }
    };
})();

