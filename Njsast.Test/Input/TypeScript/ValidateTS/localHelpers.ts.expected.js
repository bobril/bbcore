export function noop() {
    return undefined;
}

export function newHashObj() {
    return Object.create(null);
}

export const is = Object.is;

export const hOP = Object.prototype.hasOwnProperty;

export function assert(shouldBeTrue, messageIfFalse) {
    if (DEBUG && !shouldBeTrue) throw Error(messageIfFalse || "assertion failed");
}

export function newMap() {
    return new Map();
}

export function createTextNode(content) {
    return document.createTextNode(content);
}

