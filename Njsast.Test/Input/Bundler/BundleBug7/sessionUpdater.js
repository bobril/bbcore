"use strict";
exports.start = void 0;
var sessionTimer_1 = require("./sessionTimer");
var updateSessionInterval = 60 * 1000;
var start = function () {
    if (!sessionTimer_1.sessionTimer) {
        (0, sessionTimer_1.setSessionTimer)(setInterval(function () { }, updateSessionInterval));
    }
};
exports.start = start;
