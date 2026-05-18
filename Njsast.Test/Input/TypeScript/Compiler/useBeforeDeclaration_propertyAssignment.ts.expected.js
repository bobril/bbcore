"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.C = void 0;
// @target: ES6
class C {
    constructor() {
        this.a = { b: this.b, ...this.c, [this.b]: `${this.c}` };
        this.b = 0;
        this.c = { c: this.b };
    }
}
exports.C = C;
class D {
    static { this.A = class extends D.B {
        [D.D]() { } // should be an error
    }; }
    static { this.B = class {
    }; }
    static { this.C = {
        [D.D]: 1,
        ...{ get [D.D]() { return 0; } } // should be an error
    }; }
    static { this.D = ''; }
}
