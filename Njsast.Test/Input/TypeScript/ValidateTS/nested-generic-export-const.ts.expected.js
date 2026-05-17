"use strict";
exports.thisCaster = exports.createDialog = exports.context = exports.appCursor = void 0;

exports.loadLazy = loadLazy;

exports.createOptions = createOptions;

exports.createNestedOptions = createNestedOptions;

exports.getUrl = getUrl;

exports.toProperty = toProperty;

exports.castThis = castThis;

const state_1 = require("state");

exports.appCursor = {
    key: state_1.root.key
};

function loadLazy(factory) {
    return factory;
}

function createOptions(data) {
    return {
        onChange: (hourValue, minuteValue) => {
            applyChange(hourValue, minuteValue, data.onChange);
        },
        value: data.value || 0
    };
}

function createNestedOptions(data) {
    return {
        nested: data.enabled ? {
            onChange: (hourValue, minuteValue) => {
                applyChange(hourValue, minuteValue, data.onChange);
            },
            value: data.value || 0
        } : undefined
    };
}

exports.context = createContext(undefined);

function getUrl(path) {
    return path;
}

const createDialog = (state, names) => {
    return {
        state,
        names
    };
};

exports.createDialog = createDialog;

function toProperty(getCurrentValue, updateCallback) {
    return newValue => {
        if (!newValue) {
            return getCurrentValue();
        }
        updateCallback(newValue);
        return newValue;
    };
}

function castThis(value) {
    return value;
}

exports.thisCaster = {
    cast() {
        return this;
    }
};
