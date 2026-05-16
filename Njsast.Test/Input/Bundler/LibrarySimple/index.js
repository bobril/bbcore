"use strict";
exports.getDescriptor = exports.setGlobals = exports.getGlobals = void 0;
const getGlobals = () => globals;
exports.getGlobals = getGlobals;
const setGlobals = (a) => {
    globals = {
        isSortingDisabled: a,
    };
};
exports.setGlobals = setGlobals;
let globals = {
    isSortingDisabled: false,
};
function getDescriptor() {
    return { asd: "df" };
}
exports.getDescriptor = getDescriptor;
