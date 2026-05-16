export function isNumber(val) {
    return typeof val == "number";
}

export function isString(val) {
    return typeof val == "string";
}

export function isBoolean(val) {
    return typeof val == "boolean";
}

export function isFunction(val) {
    return typeof val == "function";
}

export function isObject(val) {
    return typeof val === "object";
}

export const isArray = Array.isArray;

