"use strict";
Object.defineProperty(exports, "__esModule", {
    value: true
});
const state_lib_1 = require("state-lib");
const cursor_1 = require("./cursor");
exports.default = createComponent({
    render(ctx, me) {
        const state = state_lib_1.getState(cursor_1.Cursor);
        me.value = state.value;
    }
});
