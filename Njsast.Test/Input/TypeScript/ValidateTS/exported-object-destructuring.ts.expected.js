"use strict";
var _a;
exports.isData = exports.setData = exports.getData = exports.key = void 0;
const dnd_1 = require("./dnd");

exports.key = "example/key";

_a = dnd_1.register()(exports.key), exports.getData = _a.get, exports.setData = _a.set, exports.isData = _a.is;
