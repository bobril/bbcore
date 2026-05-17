"use strict";
Object.defineProperty(exports, "__esModule", {
    value: true
});
exports.value = void 0;
const state_lib_1 = require("state-lib");
const cursor_1 = require("./cursor");
exports.value = state_lib_1.getState(cursor_1.Cursor).value;
