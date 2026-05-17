"use strict";
exports.style2 = exports.style1 = void 0;

const b = __importStar(require("bobril"));

exports.style1 = b.styleDef({
    color: "blue"
});

exports.style2 = b.styleDefEx(exports.style1, {
    background: "red"
});

