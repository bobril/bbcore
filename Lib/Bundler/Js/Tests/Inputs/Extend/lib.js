"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var Base = /** @class */ (function () {
    function Base() {
    }
    Base.prototype.hello = function () { console.log("Base"); };
    return Base;
}());
exports.Base = Base;
var Derived = /** @class */ (function (_super) {
    __extends(Derived, _super);
    function Derived() {
        return _super !== null && _super.apply(this, arguments) || this;
    }
    Derived.prototype.hello = function () { console.log("Derived"); };
    return Derived;
}(Base));
exports.Derived = Derived;
