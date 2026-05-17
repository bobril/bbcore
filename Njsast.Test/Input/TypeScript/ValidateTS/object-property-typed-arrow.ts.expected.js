"use strict";
exports.createCleanup = createCleanup;

function createCleanup(cbs) {
    return {
        add: cb => {
            cbs().push(cb);
            return () => cb();
        }
    };
}
