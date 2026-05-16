"use strict";
exports.stop = void 0;
var sessionTimer_1 = require("./sessionTimer");
var stop = function () {
    if (sessionTimer_1.sessionTimer) {
        clearInterval(sessionTimer_1.sessionTimer);
        (0, sessionTimer_1.setSessionTimer)(undefined);
    }
};
exports.stop = stop;
