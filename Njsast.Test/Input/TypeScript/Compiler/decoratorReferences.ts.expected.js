"use strict";
let C = class C {
    method(x, y) { } // <-- decorator y should be resolved at the class declaration, not the parameter.
};
__decorate([
    y(null) // <-- y should resolve to the function declaration, not the parameter; T should resolve to the type parameter of the class
    ,
    __param(0, y)
], C.prototype, "method", null);
C = __decorate([
    y(1, () => C) // <-- T should be resolved to the type alias, not the type parameter of the class; C should resolve to the class
], C);
