"use strict";
var _a;
exports.setPosition = void 0;
exports.apply = apply;

function apply(ctx, box) {
    exports.setPosition(ctx, {
        x: box.left,
        y: box.top
    });
}

_a = register()("position"), exports.setPosition = _a.set;
