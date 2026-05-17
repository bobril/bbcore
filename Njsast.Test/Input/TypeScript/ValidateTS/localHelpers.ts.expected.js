"use strict";
exports.hOP = exports.is = void 0;

exports.noop = noop;

exports.newHashObj = newHashObj;

exports.assert = assert;

exports.newMap = newMap;

exports.createTextNode = createTextNode;

function noop() {
    return undefined;
}

function newHashObj() {
    return Object.create(null);
}

exports.is = Object.is;

exports.hOP = Object.prototype.hasOwnProperty;

function assert(shouldBeTrue, messageIfFalse) {
    if (DEBUG && !shouldBeTrue) throw Error(messageIfFalse || "assertion failed");
}

function newMap() {
    return new Map();
}

function createTextNode(content) {
    return document.createTextNode(content);
}

