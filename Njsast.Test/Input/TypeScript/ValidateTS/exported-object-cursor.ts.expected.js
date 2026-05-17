"use strict";
exports.WithParameterProperties = exports.rootCursor = void 0;
const rootKey = "app.root";

exports.rootCursor = {
    key: rootKey
};

class WithParameterProperties {
    constructor(getValue, createKey) {
        this.getValue = getValue;
        this.createKey = createKey;
        this.registrations = new Set();
    }
}

exports.WithParameterProperties = WithParameterProperties;
