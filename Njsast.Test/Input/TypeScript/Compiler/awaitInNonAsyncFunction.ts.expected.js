"use strict";
// @strict: false
// @target: esnext
// https://github.com/Microsoft/TypeScript/issues/26586
Object.defineProperty(exports, "__esModule", { value: true });
exports.exportedFunc = exportedFunc;
function normalFunc(p) {
    for await (const _ of [])
        ;
    return await p;
}
function exportedFunc(p) {
    for await (const _ of [])
        ;
    return await p;
}
const functionExpression = function (p) {
    for await (const _ of [])
        ;
    await p;
};
const arrowFunc = (p) => {
    for await (const _ of [])
        ;
    return await p;
};
function* generatorFunc(p) {
    for await (const _ of [])
        ;
    yield await p;
}
class clazz {
    constructor(p) {
        for await (const _ of [])
            ;
        await p;
    }
    method(p) {
        for await (const _ of [])
            ;
        await p;
    }
}
for await (const _ of [])
    ;
await null;
