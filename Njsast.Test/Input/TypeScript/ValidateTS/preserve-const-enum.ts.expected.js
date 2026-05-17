"use strict";
exports.Color = void 0;
exports.getColor = getColor;

var Color;

(function(Color) {
    Color[Color["Default"] = 0] = "Default";
    Color[Color["Error"] = 1] = "Error";
    Color[Color["Success"] = 2] = "Success";
})(Color || (exports.Color = Color = {}));

function getColor(color) {
    return color === Color.Error ? "error" : "default";
}
