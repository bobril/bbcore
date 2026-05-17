"use strict";
exports.exportedAction = exports.importedDefault = void 0;
const source_1 = __importDefault(require("./source"));

Object.defineProperty(exports, "importedDefault", {
    enumerable: true,
    get: function() {
        return source_1.default;
    },
    set: function(v) {
        exports.importedDefault = source_1.default = v;
    }
});

exports.exportedAction = createAction();

exports.default = exports.exportedAction;
