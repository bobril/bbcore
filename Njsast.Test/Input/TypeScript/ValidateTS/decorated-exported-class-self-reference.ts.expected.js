"use strict";
var Store_1;

exports.Store = void 0;
const b = __importStar(require("bobril"));

let Store = class Store {
    static {
        Store_1 = this;
    }
    static {
        this.maxValue = 1;
    }
    zoomIn() {
        return Store_1.maxValue;
    }
};

exports.Store = Store;

exports.Store = Store = Store_1 = __decorate([ b.bind ], Store);
