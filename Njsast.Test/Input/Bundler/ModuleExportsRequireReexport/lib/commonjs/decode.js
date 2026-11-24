"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.EntityDecoder = exports.DecodingMode = void 0;
var DecodingMode;
(function (DecodingMode) {
    DecodingMode[DecodingMode["Legacy"] = 0] = "Legacy";
    DecodingMode[DecodingMode["Strict"] = 1] = "Strict";
})(DecodingMode || (exports.DecodingMode = DecodingMode = {}));
class EntityDecoder {
    constructor(mode) {
        this.mode = mode;
    }
    decode(input) {
        return input + ":" + this.mode;
    }
}
exports.EntityDecoder = EntityDecoder;
