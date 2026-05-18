"use strict";
// @module: commonjs
// @target: es2015
//@declaration: true
Object.defineProperty(exports, "__esModule", { value: true });
exports.a = exports.WithData = exports.dataSomething = void 0;
exports.dataSomething = `data-${something}`;
class WithData {
    [exports.dataSomething]() {
        return "something";
    }
}
exports.WithData = WithData;
exports.a = (new WithData())["ahahahaahah"]();
