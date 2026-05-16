"use strict";
function inc() {
    console.log("Luck");
}
function exported() {
    console.log("exp");
}
exports.exported = exported;
var expr = Math.random() > 0.5 ? "A" : (inc(), "B");
while (true) {
    break;
}
if (Math.random() > 0.5 && Math.random() < 0.5) {
    console.log("combined conditions");
}
console.log(expr);
//# sourceMappingURL=index.js.map