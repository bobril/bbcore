"use strict";
Object.defineProperty(exports, "__esModule", {
    value: true
});
exports.check = void 0;
const check = value => {
    return value ? {
        ok: true
    } : {
        ok: false,
        names: []
    };
};
exports.check = check;
