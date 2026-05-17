"use strict";
Object.defineProperty(exports, "__esModule", {
    value: true
});
exports.update = update;
function update(module, updated) {
    module.viewData.x = updated.values.position.x;
}
