"use strict";
exports.selected = exports.DisplayMode = void 0;
const external_lib_1 = require("external-lib");

Object.defineProperty(exports, "DisplayMode", {
    enumerable: true,
    get: function() {
        return external_lib_1.DisplayMode;
    },
    set: function(v) {
        exports.DisplayMode = external_lib_1.DisplayMode = v;
    }
});

exports.selected = external_lib_1.DisplayMode.Compact;
