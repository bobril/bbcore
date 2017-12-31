"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
function reg() {
    return { f1: function () { return "a"; }, f2: function () { return "b"; }, f3: function () { return "c"; } };
}
exports.reg = reg;
exports.fn1 = (_a = reg(), _a.f1), exports.fn2 = _a.f2, exports.fn3 = _a.f3;
exports.fn4 = (_b = reg(), _b.f1), exports.fn5 = _b.f2, exports.fn6 = _b.f3;
var _a, _b;
