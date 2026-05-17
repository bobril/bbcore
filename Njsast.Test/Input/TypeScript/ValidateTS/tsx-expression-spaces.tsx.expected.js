"use strict";
exports.render = render;
const b = __importStar(require("bobril"));

function render(label, value, suffix) {
    return b.createElement("div", null, label, " ", value, " ", suffix);
}
