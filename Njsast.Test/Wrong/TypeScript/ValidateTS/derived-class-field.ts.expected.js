"use strict";
exports.Derived = void 0;

class Base {}

class Derived extends Base {
    constructor() {
        super(...arguments);
        this.id = "value";
    }
}

exports.Derived = Derived;

