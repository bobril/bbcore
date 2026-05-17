"use strict";
exports.CustomError = void 0;

class CustomError extends Error {
    constructor(message, code) {
        super(message);
        this.message = message;
        this.code = code;
        this.name = "CustomError";
    }
}

exports.CustomError = CustomError;
