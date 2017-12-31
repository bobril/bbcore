"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
function functionUsingEval() {
    eval('return 1');
}
function longname(parameter) {
    return parameter + functionUsingEval();
}
exports.longname = longname;
