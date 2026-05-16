"use strict";
// PureFuncs: assert
function assert(shouldBeTrue, messageIfFalse) {
    if (DEBUG && !shouldBeTrue)
        throw Error(messageIfFalse || "assertion failed");
}
exports.assert = assert;
