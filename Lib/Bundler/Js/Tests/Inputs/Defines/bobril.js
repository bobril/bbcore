"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
// PureFuncs: assert
function assert(shouldBeTrue, messageIfFalse) {
    if (DEBUG && !shouldBeTrue)
        throw Error(messageIfFalse || "assertion failed");
}
exports.assert = assert;
