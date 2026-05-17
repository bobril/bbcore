"use strict";
exports.BatchCommands = void 0;

class BatchCommands {
    constructor(errorCallback) {
        this.errorCallback = errorCallback;
        this.commands = [];
    }
}

exports.BatchCommands = BatchCommands;
