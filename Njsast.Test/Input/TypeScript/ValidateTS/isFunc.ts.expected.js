"use strict";
exports.isArray = void 0;

exports.isNumber = isNumber;

exports.isString = isString;

exports.isBoolean = isBoolean;

exports.isFunction = isFunction;

exports.isObject = isObject;

function isNumber(val) {
    return typeof val == "number";
}

function isString(val) {
    return typeof val == "string";
}

function isBoolean(val) {
    return typeof val == "boolean";
}

function isFunction(val) {
    return typeof val == "function";
}

function isObject(val) {
    return typeof val === "object";
}

exports.isArray = Array.isArray;

