"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.second = exports.first = exports.set = exports.get = exports.is = void 0;
function make(name) {
    return { is: (x) => x > 1, get: () => name.length, set: (v) => (name = v) };
}
({ is: exports.is, get: exports.get, set: exports.set } = make("programList/thumbnailView/move"));
[exports.first, exports.second] = [10, 20];
